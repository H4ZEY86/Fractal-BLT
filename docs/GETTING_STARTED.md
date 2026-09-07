# Getting Started with Fractal-BLT

This guide explains how to configure, compile, and run the Fractal-BLT zero-allocation pipeline on your workstation.

## Prerequisites

1. **Hardware**: An NVMe SSD (Gen4/Gen5 highly recommended) and an NVIDIA GPU (RTX 30XX/40XX/50XX series).
2. **OS**: Windows 11 (PowerShell) or WSL2 (Ubuntu 22.04+).
3. **Software**:
   - .NET 10.0 SDK with the `NativeAOT` workload installed.
   - NVIDIA CUDA Toolkit v12.x+.

## 1. Preparing Safetensors

Fractal-BLT utilizes unbuffered direct I/O (`FILE_FLAG_NO_BUFFERING`) which requires bypassing the OS page cache. Your model weights must be stored in the `.safetensors` format.

Ensure you have a `.safetensors` file available locally. For testing, you can download a minimal model (like TinyLlama) from Hugging Face:
```bash
# Example
wget https://huggingface.co/TinyLlama/TinyLlama-1.1B-Chat-v1.0/resolve/main/model.safetensors
```

## 2. Compilation

Clone the repository and compile the binaries ahead-of-time (AOT) to strip out the managed runtime and enforce zero GC pauses.

```bash
git clone https://github.com/H4ZEY86/Fractal-BLT.git
cd Fractal-BLT

# Publish the Kestrel server API
dotnet publish FractalServe/FractalServe.csproj -c Release -r win-x64 /p:PublishAot=true
```

## 3. Launching the API Server

You must provide the path to your `.safetensors` file via the `FRACTAL_MODEL` environment variable.

### Windows (PowerShell)
```powershell
$env:FRACTAL_MODEL="C:\models\model.safetensors"
.\FractalServe\bin\Release\net10.0\win-x64\publish\FractalServe.exe
```

### Linux / WSL
```bash
export FRACTAL_MODEL="/mnt/c/models/model.safetensors"
./FractalServe/bin/Release/net10.0/linux-x64/publish/FractalServe
```

## 4. Consuming the Stream

Fractal-BLT uses Server-Sent Events (SSE) to push zero-copy logits directly from GPU memory to the client.

```bash
curl -N -X POST http://localhost:5000/v1/chat/completions \
     -H "Content-Type: application/json" \
     -d '{"model": "fractal-moe", "messages": [{"role": "user", "content": "Compute"}], "stream": true}'
```

To integrate this programmatically, refer to the client scripts in the `examples/` directory of the repository.

## 5. macOS and Apple Silicon (CPU Fallback)

While Fractal-BLT is heavily optimized for Windows/Linux NVIDIA environments via direct PTX execution, **macOS users are not left out**. You can compile and run the engine using the self-contained `.zip` assets, but you will need to bypass the `nvcuda` P/Invoke hardware acceleration since NVIDIA drivers are unavailable on macOS.

### Implementing the CPU Fallback

To run the pipeline on an M-series Mac or an environment without an NVIDIA GPU, you can replace the CUDA JIT block in `FractalServe/Program.cs` with a direct unmanaged CPU math loop. 

Locate the `--- CUDA PTX EXECUTION ---` block in `Program.cs` and replace it with:

```csharp
// --- APPLE SILICON / CPU FALLBACK EXECUTION ---
// Compute SGEMV natively on the CPU: Y = W * X
for (uint r = 0; r < rows; r++)
{
    float sum = 0f;
    for (uint c = 0; c < cols; c++)
    {
        // hostWeights is row-major (rows x cols)
        float w = ((float*)hostWeights)[r * cols + c];
        float x = hostInput[c];
        sum += w * x;
    }
    hostOutput[r] = sum;
}

outputMessage += " CPU Logits:";
for (int i = 0; i < Math.Min(rows, 10); i++) 
{
    outputMessage += $" {hostOutput[i]:F4}";
}
```

This ensures you can still test the unmanaged I/O streaming, zero-allocation routing, and patchification logic locally on your MacBook!
