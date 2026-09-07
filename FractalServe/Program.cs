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

        // Initialize core engine components as Singletons to maintain the exact 10.06MB memory footprint
        // (Mocking the pipeline for the SSE stream)

        var app = builder.Build();

        app.MapPost("/v1/chat/completions", async (HttpContext context, ChatCompletionRequest request) =>
        {
            if (request.Stream == true)
            {
                context.Response.ContentType = "text/event-stream";
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers.Connection = "keep-alive";

                var writer = context.Response.BodyWriter;

                string modelPath = Environment.GetEnvironmentVariable("FRACTAL_MODEL") ?? "C:\\Fractal-BLT\\tiny-llama.safetensors";
                string inputContent = request.Messages != null && request.Messages.Count > 0 ? request.Messages[0].Content : "default";

                // 1. Patchify
                byte[] inputBytes = Encoding.UTF8.GetBytes(inputContent);
                var scorer = new FractalBltEncoder.FastNGramScorer();
                FractalBltEncoder.PatchBoundary[] boundaries = new FractalBltEncoder.PatchBoundary[Math.Max(inputBytes.Length / 2 + 1, 10)];
                int patchCount = FractalBltEncoder.BltEncoder.Patchify(inputBytes, 2.5f, boundaries, ref scorer, 32);

                // 2. Routing
                var expertRegistry = new FractalGnnRouter.ExpertRegistry();
                expertRegistry.InitializeMockData(64);
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
                catch (Exception ex)
                {
                    allTensors = new List<string> { "error_loading_tensors" };
                }

                string targetTensor = allTensors.Count > 0 ? allTensors[selectedExpert % allTensors.Count] : "unknown";
                string outputMessage = $"[Expert {selectedExpert} -> {targetTensor}]";

                // 4. Tensor Loading & Stub Compute
                if (FractalStreamer.SafetensorsHeaderParser.TryGetTensorOffsets(modelPath, targetTensor, out long offset, out long length))
                {
                    long readLength = Math.Min(length, 1024); // read up to 1KB for checksum
                    unsafe 
                    {
                        void* buffer = System.Runtime.InteropServices.NativeMemory.Alloc((nuint)readLength);
                        try 
                        {
                            using var reader = new FractalStreamer.TensorReader();
                            reader.ReadInto(modelPath, offset, (int)readLength, buffer);

                            float* floats = (float*)buffer;
                            int floatCount = (int)readLength / sizeof(float);
                            float sum = 0;
                            for (int i = 0; i < Math.Min(floatCount, 10); i++) 
                            {
                                sum += floats[i];
                            }
                            outputMessage += $" Checksum: {sum:F4}";
                        }
                        catch (Exception ex)
                        {
                            outputMessage += $" Read error: {ex.Message}";
                        }
                        finally 
                        {
                            System.Runtime.InteropServices.NativeMemory.Free(buffer);
                        }
                    }
                }
                else
                {
                    outputMessage += " (Tensor offsets not found)";
                }

                // 5. Output tokens
                string[] simulatedTokens = outputMessage.Split(' ');
                foreach (var token in simulatedTokens)
                {
                    if (string.IsNullOrEmpty(token)) continue;

                    // Write prefix
                    await writer.WriteAsync(s_dataPrefix);
                    
                    // Write token
                    byte[] tokenBytes = Encoding.UTF8.GetBytes(" " + token);
                    await writer.WriteAsync(tokenBytes);
                    
                    // Write suffix
                    await writer.WriteAsync(s_dataSuffix);
                    
                    await writer.FlushAsync();
                    
                    // Yield execution back to Kestrel's I/O loop
                    await Task.Yield();
                    await Task.Delay(50); // Simulate token generation delay
                }

                // Final SSE terminator
                await writer.WriteAsync(s_doneMessage);
                await writer.FlushAsync();
                
                return Results.Empty;
            }

            // Non-streaming fallback
            string responseText = "Fractal Pipeline Simulated Output.";
            var response = new ChatCompletionResponse
            {
                Choices = new List<ChatCompletionChoice>
                {
                    new ChatCompletionChoice
                    {
                        Message = new ChatCompletionMessage
                        {
                            Role = "assistant",
                            Content = responseText.Trim()
                        }
                    }
                }
            };
            return Results.Json(response, FractalJsonContext.Default.ChatCompletionResponse);
        });

        app.Run();
    }
}
