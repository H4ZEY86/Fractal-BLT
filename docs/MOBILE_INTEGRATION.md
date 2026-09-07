# Mobile Integration Guide (iOS & Android)

Fractal-BLT acts as a localized, hardware-accelerated backend server. It exposes an **OpenAI-compatible Server-Sent Events (SSE)** endpoint. This means that you do not need to port or compile the Fractal-BLT C#/CUDA engine for mobile devices. 

Instead, you run Fractal-BLT on a powerful host machine (Windows/Linux) equipped with NVMe and NVIDIA GPUs, and your mobile apps connect to it over the local network (or a public tunnel) to stream physical logits in real-time.

---

## iOS (Swift) Integration

Since the API is a standard SSE stream, you can use Swift's native `URLSession` to read the bytes as they arrive from the GPU.

```swift
import Foundation

class FractalClient: NSObject, URLSessionDataDelegate {
    
    // Replace with your Fractal-BLT server IP
    let serverUrl = URL(string: "http://192.168.1.50:5000/v1/chat/completions")!
    
    func streamCompletions(prompt: String) {
        var request = URLRequest(url: serverUrl)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        
        let payload: [String: Any] = [
            "model": "fractal-moe",
            "messages": [["role": "user", "content": prompt]],
            "stream": true
        ]
        
        request.httpBody = try? JSONSerialization.data(withJSONObject: payload)
        
        // Use a background session to handle streaming bytes
        let session = URLSession(configuration: .default, delegate: self, delegateQueue: nil)
        let task = session.dataTask(with: request)
        task.resume()
    }
    
    // URLSession delegate method to capture incoming physical logits as they arrive
    func urlSession(_ session: URLSession, dataTask: URLSessionDataTask, didReceive data: Data) {
        if let string = String(data: data, encoding: .utf8) {
            let lines = string.components(separatedBy: "\n\n")
            for line in lines where line.hasPrefix("data: ") {
                let jsonStr = line.dropFirst(6)
                if jsonStr == "[DONE]" {
                    print("\nStream finished.")
                    return
                }
                // Decode standard OpenAI chunk format here
                print(jsonStr) 
            }
        }
    }
}
```

---

## Android (Kotlin) Integration

On Android, we recommend using [OkHttp](https://square.github.io/okhttp/) with its official Server-Sent Events extension to effortlessly handle the chunked tensor output.

### 1. Add Dependencies (`build.gradle`)
```gradle
implementation "com.squareup.okhttp3:okhttp:4.12.0"
implementation "com.squareup.okhttp3:okhttp-sse:4.12.0"
```

### 2. Stream Data
```kotlin
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.sse.EventSource
import okhttp3.sse.EventSourceListener
import okhttp3.sse.EventSources

class FractalClient {
    private val client = OkHttpClient()
    
    fun streamCompletions(prompt: String) {
        // Replace with your Fractal-BLT server IP
        val request = Request.Builder()
            .url("http://192.168.1.50:5000/v1/chat/completions")
            .post("""
                {
                    "model": "fractal-moe",
                    "messages": [{"role": "user", "content": "$prompt"}],
                    "stream": true
                }
            """.trimIndent().toRequestBody("application/json".toMediaType()))
            .build()
            
        val eventSourceFactory = EventSources.createFactory(client)
        
        eventSourceFactory.newEventSource(request, object : EventSourceListener() {
            override fun onEvent(eventSource: EventSource, id: String?, type: String?, data: String) {
                if (data == "[DONE]") {
                    println("\nStream finished.")
                    return
                }
                // Parse standard OpenAI chunk format here
                println("Chunk received: $data")
            }
            
            override fun onFailure(eventSource: EventSource, t: Throwable?, response: Response?) {
                t?.printStackTrace()
            }
        })
    }
}
```

## Configuring the Server for Mobile

By default, the Fractal-BLT server binds to `http://localhost:5000`, which means it is only accessible from the machine it is running on.

To allow mobile devices on your network to connect, you must configure the Kestrel web server to bind to `0.0.0.0` (all network interfaces).

**Run the server using:**
```bash
dotnet run --urls "http://0.0.0.0:5000"
```
Or, if running the compiled executable:
```bash
./FractalServe --urls "http://0.0.0.0:5000"
```

*(Note: Ensure your host machine's firewall allows inbound TCP connections on port 5000).*
