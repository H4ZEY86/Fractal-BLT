# FRACTAL-BLT

![.NET 10 NativeAOT](https://img.shields.io/badge/.NET_10-NativeAOT-00ffff?style=for-the-badge&logo=dotnet)
![Zero-Allocation](https://img.shields.io/badge/Architecture-Zero--Allocation-ff00ff?style=for-the-badge)
![License](https://img.shields.io/badge/License-Apache_2.0-ffd700?style=for-the-badge)
![NVMe-to-GPU](https://img.shields.io/badge/I%2FO-NVMe_to_GPU_DMA-00ffff?style=for-the-badge)
![RTX Ready](https://img.shields.io/badge/Hardware-RTX_5070_Ti_Ready-ff00ff?style=for-the-badge)

## ⚡ The Architectural TL;DR

**Fractal-BLT** is a state-of-the-art AI runtime built from the ground up in pure C# .NET 10 NativeAOT. It is designed to absolutely obliterate VRAM limitations by streaming massive Mixture-of-Experts (MoE) weights directly from your NVMe drive to the GPU over the PCIe bus, bypassing the OS page cache entirely.

The architecture is composed of four brutalist layers:
1. **The BLT Encoder:** Kills the static token dictionary. Uses a Byte Latent Transformer (BLT) dynamic patching engine to cross-entropy threshold raw UTF-8 bytes into variable-length latent patches in strictly O(N) time.
2. **The GNN Router:** A zero-allocation graph routing engine that projects latent patches into an unmanaged `[InlineArray]` expert registry, mapping variable-length sequences to 64 dedicated MoE experts.
3. **The NVMe Bridge:** Uses `FILE_FLAG_NO_BUFFERING` and `O_DIRECT` raw unbuffered reads pinned directly to host memory, synchronized with async `cuMemcpyHtoDAsync` Driver API calls to saturate the PCIe bus.
4. **The Kestrel API:** An ultra-lean Server-Sent Events (SSE) `/v1/chat/completions` endpoint that streams raw UTF-8 spans directly to the socket via Kestrel's `PipeWriter`—bypassing managed string allocations entirely.

---

## 🛠️ The Gauntlet: Setup & Execution

Fractal-BLT is deployed as a single, razor-thin NativeAOT binary stripped of all debugging symbols and runtime bloat.

### 1. Compile the Bare-Metal Executable
```bash
dotnet publish FractalCore/FractalCore.csproj -c Release -r linux-x64
```
*(Swap `linux-x64` for `win-x64` if benchmarking on Windows.)*

## Usage

### 1. Minimal Engine Execution (Diagnostic Stub)
For testing the zero-allocation routing and NVMe DMA streaming architecture:
1. Ensure the `FRACTAL_MODEL` environment variable is set to a valid `.safetensors` model file (e.g., `tiny-llama.safetensors`).
2. Boot the API: `dotnet run --project FractalServe -c Release`
3. Execute a completion request:
```bash
curl -X POST http://localhost:5000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model": "fractal-moe-64x", "messages": [{"role": "user", "content": "Hello world"}], "stream": true}'
```

> [!NOTE]
> The current token generation is in **Diagnostic Mode**. The HTTP endpoint will successfully evaluate the incoming text via `BltEncoder`, route the patches via `GnnRouter`, map the request to an expert tensor, read the physical tensor bytes directly from disk, and compute a checksum. The output SSE tokens will return simulated data (tensor name and checksum) rather than a true CUDA matrix-multiplied autoregressive sequence.

### 2. Standalone Verification Gauntlet.
```bash
./FractalCore/bin/Release/net10.0/linux-x64/publish/FractalCore
```

### 3. Ignite the API Server
Start the ultra-lean OpenAI-compatible Kestrel server.
```bash
dotnet publish FractalServe/FractalServe.csproj -c Release -r linux-x64
./FractalServe/bin/Release/net10.0/linux-x64/publish/FractalServe
```
Test the zero-allocation SSE stream:
```bash
curl -X POST http://localhost:5000/v1/chat/completions \
     -H "Content-Type: application/json" \
     -d '{"model": "fractal-moe-64x", "messages": [{"role": "user", "content": "Ignite sequence."}], "stream": true}'
```

---

## 🗄️ Alignment Policy

For true zero-copy O_DIRECT DMA from NVMe to GPU, file offsets must be aligned to the OS page size (typically 4096 bytes on Windows/Linux).
However, most real-world `.safetensors` files contain unaligned data fragments.

**Fractal-BLT employs a pragmatic alignment strategy:**
- **Fast Path:** If a tensor's byte offset and size are perfectly page-aligned, it uses direct zero-copy unbuffered I/O.
- **Fallback Path:** If unaligned, it seamlessly allocates a short-lived, page-aligned staging buffer in pinned memory, over-reads the necessary sector, copies the exact byte range to the destination, and frees the staging buffer.

You can verify the alignment of your `.safetensors` model using the CLI:
```bash
./FractalCore/bin/Release/net10.0/linux-x64/publish/FractalCore --verify ./model.safetensors [--strict]
```
If `--strict` is passed, the runtime will throw an exception if any unaligned tensors are detected.
