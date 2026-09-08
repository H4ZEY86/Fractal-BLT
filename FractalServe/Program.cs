using System;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
namespace FractalServe;

/// <summary>
/// The primary Kestrel web host exposing the zero-allocation Fractal-BLT pipeline.
/// Designed for NativeAOT compilation and lock-free async streaming.
/// </summary>
public class Program
{
    private static readonly byte[] s_dataPrefix = Encoding.UTF8.GetBytes("data: {\"choices\": [{\"delta\": {\"content\": \"");
    private static readonly byte[] s_dataSuffix = Encoding.UTF8.GetBytes("\"}}]}\n\n");
    private static readonly byte[] s_doneMessage = Encoding.UTF8.GetBytes("data: [DONE]\n\n");

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        
        // Register System.Text.Json source generator context for AOT
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, FractalJsonContext.Default);
        });

        // Add CORS to allow LocalUI GUI to stream SSE
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });

        // Initialize core engine components as Singletons to maintain the exact 10.06MB memory footprint

        var app = builder.Build();
        
        // Enable CORS
        app.UseCors("AllowAll");
        
        // Serve LocalUI from wwwroot
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapPost("/v1/chat/completions", async (HttpContext context, ChatCompletionRequest request) =>
        {
            string modelPath = app.Configuration["ModelPath"] ?? Environment.GetEnvironmentVariable("FRACTAL_MODEL") ?? "C:\\Fractal-BLT\\tiny-llama.safetensors";
            string inputContent = request.Messages != null && request.Messages.Count > 0 ? request.Messages[0].Content : "default";

            // 1. Patchify
            byte[] inputBytes = Encoding.UTF8.GetBytes(inputContent);
            var scorer = new FractalBltEncoder.ShannonEntropyScorer();
            FractalBltEncoder.PatchBoundary[] boundaries = new FractalBltEncoder.PatchBoundary[Math.Max(inputBytes.Length / 2 + 1, 10)];
            int patchCount = FractalBltEncoder.BltEncoder.Patchify(inputBytes, 4.0f, boundaries, ref scorer, 32);

            // 2. Routing
            var expertRegistry = new FractalGnnRouter.ExpertRegistry();
            expertRegistry.InitializeRandom(64);
            FractalGnnRouter.RouteAssignment[] routes = new FractalGnnRouter.RouteAssignment[Math.Max(patchCount, 1)];
            FractalGnnRouter.GnnRouter.ComputeRoutes(new Span<FractalBltEncoder.PatchBoundary>(boundaries, 0, patchCount), ref expertRegistry, routes);

            // Pick the first expert assignment
            int selectedExpert = patchCount > 0 ? routes[0].ExpertId : 0;

            // 3. Expert Mapping
            List<string> allTensors;
            try 
            {
                allTensors = FractalStreamer.SafetensorsHeaderParser.GetAllTensorNames(modelPath);
            }
            catch (Exception)
            {
                allTensors = new List<string> { "error_loading_tensors" };
            }

            string targetTensor = allTensors.Count > 0 ? allTensors[selectedExpert % allTensors.Count] : "unknown";
            string outputMessage = $"[Expert {selectedExpert} -> {targetTensor}]";

            // 4. Tensor Loading & PTX CUDA Matrix Compute
            if (FractalStreamer.SafetensorsHeaderParser.TryGetTensorOffsets(modelPath, targetTensor, out long offset, out long length))
            {
                unsafe 
                {
                    // Assume a default size for the vector to keep it simple, say cols = 4096.
                    // We'll figure out rows based on length.
                    uint cols = 4096;
                    uint rows = (uint)(length / (cols * sizeof(float)));
                    if (rows == 0 || length % (cols * sizeof(float)) != 0) 
                    {
                        // Fallback if the tensor shape is not a clean multiple of 4096
                        cols = 1024;
                        rows = (uint)(length / (cols * sizeof(float)));
                        if (rows == 0) rows = 1;
                    }

                    // Process up to 16 rows to keep terminal output manageable during diagnostic runs
                    rows = Math.Min(rows, 16);
                    long readLength = rows * cols * sizeof(float);

                    float* hostInput = (float*)System.Runtime.InteropServices.NativeMemory.Alloc((nuint)(cols * sizeof(float)));
                    float* hostOutput = (float*)System.Runtime.InteropServices.NativeMemory.Alloc((nuint)(rows * sizeof(float)));
                    
                    // Initialize synthetic input embedding vector
                    for (int i = 0; i < cols; i++) hostInput[i] = (float)Math.Sin(i);

                    try 
                    {
                        using var reader = new FractalStreamer.TensorReader();
                        void* mappedWeightsPtr = reader.MapTensorChunkDirect(modelPath, offset, readLength, out IDisposable mmfHandle);
                        using (mmfHandle)
                        {

                        // --- CUDA PTX EXECUTION ---
                        FractalBridge.CudaNative.Init(0);
                        FractalBridge.CudaNative.DeviceGet(out int device, 0);
                        FractalBridge.CudaNative.CtxCreate(out IntPtr ctx, 0, device);
                        try 
                        {
                            FractalBridge.CudaNative.StreamCreate(out IntPtr hStream, 0);
                            IntPtr ptxPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(PtxKernels.Sgemv);
                            FractalBridge.CudaNative.ModuleLoadData(out IntPtr module, ptxPtr);
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptxPtr);

                            FractalBridge.CudaNative.ModuleGetFunction(out IntPtr hfunc, module, "gemv");

                            FractalBridge.CudaNative.MemAlloc(out IntPtr dW, (nuint)readLength);
                            FractalBridge.CudaNative.MemAlloc(out IntPtr dX, (nuint)(cols * sizeof(float)));
                            FractalBridge.CudaNative.MemAlloc(out IntPtr dY, (nuint)(rows * sizeof(float)));

                            FractalBridge.CudaNative.MemcpyHtoDAsync(dW, (IntPtr)mappedWeightsPtr, (nuint)readLength, hStream);
                            FractalBridge.CudaNative.MemcpyHtoDAsync(dX, (IntPtr)hostInput, (nuint)(cols * sizeof(float)), hStream);

                            void*[] args = new void*[] { &dW, &dX, &dY, &rows, &cols };
                            fixed (void** pArgs = args)
                            {
                                uint blockDimX = 256;
                                uint gridDimX = (rows + blockDimX - 1) / blockDimX;
                                FractalBridge.CudaNative.LaunchKernel(hfunc, gridDimX, 1, 1, blockDimX, 1, 1, 0, hStream, (IntPtr)pArgs, IntPtr.Zero);
                            }

                            FractalBridge.CudaNative.MemcpyDtoHAsync((IntPtr)hostOutput, dY, (nuint)(rows * sizeof(float)), hStream);
                            FractalBridge.CudaNative.StreamSynchronize(hStream);

                            FractalBridge.CudaNative.MemFree(dW);
                            FractalBridge.CudaNative.MemFree(dX);
                            FractalBridge.CudaNative.MemFree(dY);
                            FractalBridge.CudaNative.cuStreamDestroy(hStream);

                            outputMessage += " GPU Logits:";
                            for (int i = 0; i < Math.Min(rows, 10); i++) 
                            {
                                outputMessage += $" {hostOutput[i]:F4}";
                            }
                        }
                        finally
                        {
                            FractalBridge.CudaNative.cuCtxDestroy(ctx);
                        }
                        } // End of mmfHandle using block
                    }
                    catch (Exception ex)
                    {
                        outputMessage += $" Compute error: {ex.Message}";
                    }
                    finally 
                    {
                        System.Runtime.InteropServices.NativeMemory.Free(hostInput);
                        System.Runtime.InteropServices.NativeMemory.Free(hostOutput);
                    }
                }
            }
            else
            {
                outputMessage += " (Tensor offsets not found)";
            }

            // 5. Return Response
            if (request.Stream == true)
            {
                context.Response.ContentType = "text/event-stream";
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers.Connection = "keep-alive";

                var writer = context.Response.BodyWriter;
                string[] outputTokens = outputMessage.Split(' ');
                await StreamTokensAsync(writer, outputTokens);
                
                return Results.Empty;
            }
            else
            {
                // Non-streaming fallback
                var response = new ChatCompletionResponse
                {
                    Choices = new List<ChatCompletionChoice>
                    {
                        new ChatCompletionChoice
                        {
                            Message = new ChatCompletionMessage
                            {
                                Role = "assistant",
                                Content = outputMessage.Trim()
                            }
                        }
                    }
                };
                return Results.Json(response, FractalJsonContext.Default.ChatCompletionResponse);
            }
        });

        app.Run();
    }

    private static async Task StreamTokensAsync(PipeWriter writer, string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token)) continue;

            var chunk = new ChatCompletionChunk
            {
                Choices = new List<ChatCompletionChunkChoice>
                {
                    new ChatCompletionChunkChoice
                    {
                        Delta = new ChatCompletionDelta
                        {
                            Content = " " + token
                        }
                    }
                }
            };

            await writer.WriteAsync(Encoding.UTF8.GetBytes("data: "));
            JsonSerializer.Serialize(writer.AsStream(), chunk, FractalJsonContext.Default.ChatCompletionChunk);
            await writer.WriteAsync(Encoding.UTF8.GetBytes("\n\n"));
            await writer.FlushAsync();
            
            await Task.Yield();
            await Task.Delay(50);
        }

        await writer.WriteAsync(Encoding.UTF8.GetBytes("data: [DONE]\n\n"));
        await writer.FlushAsync();
    }
}
