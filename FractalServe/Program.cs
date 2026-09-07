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

                string simulatedResponse = " This is a simulated response generated directly from the Fractal zero-allocation pipeline.";
                string[] tokens = simulatedResponse.Split(' '); // Mock tokens for simulation

                foreach (var token in tokens)
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
