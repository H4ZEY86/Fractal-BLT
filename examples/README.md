# FRACTAL-BLT // Code Examples

The `examples/` directory contains sample client code demonstrating how to stream Server-Sent Events (SSE) from the `FractalServe` API endpoint using various languages.

## Available Examples

### 1. `client.py` (Python)
A lightweight Python script using the `requests` library to stream completions from the `http://localhost:5000/v1/chat/completions` endpoint.

**Usage:**
```bash
python examples/client.py
```

### 2. `Client.cs` (C# / .NET)
A fully asynchronous C# console application using `HttpClient` to process the Server-Sent Events stream from the Fractal-BLT API in real-time.

**Usage:**
```bash
dotnet run --project examples/Client.cs
```

---
*Return to the [Terminal](../docs/index.html).*
