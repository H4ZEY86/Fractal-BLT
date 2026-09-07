# FRACTAL-BLT // Code Examples

The examples below demonstrate how to connect to the physical logits stream (Server-Sent Events) from the `FractalServe` API endpoint using various languages.

## 1. Python (`requests` + `sseclient-py`)

A lightweight script to stream completions from the `http://localhost:5000/v1/chat/completions` endpoint.

```python
#!/usr/bin/env python3
import json
import requests
import sseclient
import sys

API_URL = "http://localhost:5000/v1/chat/completions"

def stream_fractal_blt():
    payload = {
        "model": "fractal-moe-64x",
        "messages": [{"role": "user", "content": "Compute"}],
        "stream": True
    }
    
    headers = {
        "Accept": "text/event-stream",
        "Content-Type": "application/json"
    }

    print(f"[+] Connecting to Fractal-BLT NativeAOT Runtime at {API_URL}...")
    
    try:
        response = requests.post(API_URL, json=payload, headers=headers, stream=True)
        response.raise_for_status()
        
        client = sseclient.SSEClient(response)
        
        print("[+] SSE Stream Established. Listening for GPU logits:\n")
        
        for event in client.events():
            if event.data == "[DONE]":
                print("\n\n[+] Stream Complete.")
                break
                
            try:
                data = json.loads(event.data)
                content = data["choices"][0]["delta"]["content"]
                sys.stdout.write(content)
                sys.stdout.flush()
                
            except (json.JSONDecodeError, KeyError) as e:
                print(f"\n[!] Malformed chunk: {event.data}")
                
    except requests.exceptions.RequestException as e:
        print(f"\n[!] Connection failed: {e}")

if __name__ == "__main__":
    stream_fractal_blt()
```

## 2. C# .NET (`HttpClient`)

A fully asynchronous C# console application using `HttpClient` to process the Server-Sent Events stream from the Fractal-BLT API in real-time.

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Fractal.Client
{
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
            }
        }
    }
}
```
