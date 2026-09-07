using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Fractal.Client
{
    /// <summary>
    /// Example C# console application demonstrating how to consume the Fractal-BLT SSE endpoint.
    /// </summary>
    public class Program
    {
        private const string ApiUrl = "http://localhost:5000/v1/chat/completions";

        public static async Task Main()
        {
            var payload = new
            {
                model = "fractal-moe-64x",
                messages = new[] { new { role = "user", content = "Compute" } },
                stream = true
            };

            string jsonString = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl) { Content = content };
            request.Headers.Add("Accept", "text/event-stream");

            Console.WriteLine($"[+] Connecting to Fractal-BLT NativeAOT Runtime at {ApiUrl}...");

            try
            {
                // Send the request, specifying that we want to read the response headers immediately.
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                Console.WriteLine("[+] SSE Stream Established. Listening for GPU logits:\n");

                using var responseStream = await response.Content.ReadAsStreamAsync();
                using var streamReader = new StreamReader(responseStream);

                while (!streamReader.EndOfStream)
                {
                    string? line = await streamReader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.StartsWith("data: "))
                    {
                        string data = line.Substring("data: ".Length).Trim();
                        
                        if (data == "[DONE]")
                        {
                            Console.WriteLine("\n\n[+] Stream Complete.");
                            break;
                        }

                        try
                        {
                            // A real implementation should use System.Text.Json source generators for zero allocation parsing.
                            using var doc = JsonDocument.Parse(data);
                            string? chunk = doc.RootElement
                                .GetProperty("choices")[0]
                                .GetProperty("delta")
                                .GetProperty("content").GetString();

                            Console.Write(chunk);
                        }
                        catch (Exception)
                        {
                            Console.WriteLine($"\n[!] Malformed chunk: {data}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[!] Connection failed: {ex.Message}");
                Console.WriteLine("    Ensure FractalServe is running (`dotnet run --project FractalServe`).");
            }
        }
    }
}
