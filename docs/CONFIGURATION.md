# Configuration Guide

Fractal-BLT is configured almost entirely through Environment Variables, adhering to the 12-factor app methodology. This avoids parsing overhead from `appsettings.json` during the NativeAOT startup sequence.

## Required Variables

### `FRACTAL_MODEL`
The absolute path to the `.safetensors` weight file. The engine uses this path to establish the unbuffered `FILE_FLAG_NO_BUFFERING` DMA stream.
- **Example (Windows)**: `C:\models\tiny-llama.safetensors`
- **Example (Linux/Docker)**: `/models/tiny-llama.safetensors`

## Server Configuration

By default, the Kestrel web server listens on `http://localhost:5000`.

### `ASPNETCORE_URLS`
Override the binding address and port.
- **Example (Expose to Network)**: `http://0.0.0.0:5000`
- **Example (Custom Port)**: `http://localhost:8080`

## Advanced Tuning (Planned)

The following parameters are currently hardcoded in the C# source to guarantee zero-allocation limits at compile time, but will be exposed as environment variables in `v1.2.0`:

- `FRACTAL_EXPERT_RING_SIZE` (Default: `64`): The total number of experts in the MoE registry.
- `FRACTAL_GNN_WINDOW` (Default: `8`): The receptive field for the 1-hop Graph Convolution. Increasing this requires a larger `stackalloc` adjacency matrix.
- `FRACTAL_ENTROPY_PEAK` (Default: `4.0f`): The Shannon entropy threshold ($H$) at which the `BltEncoder` determines a semantic boundary has been crossed and flushes a patch.
