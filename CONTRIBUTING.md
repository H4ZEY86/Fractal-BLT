# Contributing to Fractal-BLT

Thank you for your interest in contributing to Fractal-BLT! This project is maintained by systems engineers who are obsessive about zero-allocation constraints, latency, and bare-metal performance.

## The Core Philosophy
1. **Zero Allocations on the Hot Path**: Pull requests that introduce `new` keyword allocations, boxing, or LINQ into `FractalCore` or `FractalBridge` will be rejected outright. Use `stackalloc`, `ReadOnlySpan<T>`, and unmanaged memory.
2. **NativeAOT Compatibility**: All code must compile cleanly ahead-of-time. No dynamic assembly loading, no JIT Emit, no `dynamic` keyword.
3. **Hardware Agnosticism (Within Reason)**: Our PTX kernels target NVIDIA hardware, but the memory architecture should remain clean enough to support ROCm or DirectML in the future.

## Development Setup
- Install the **.NET 10.0 SDK**.
- Ensure the `NativeAOT` workload is installed.
- Install the **NVIDIA CUDA Toolkit v12+**.

Run the integration tests before submitting:
```bash
dotnet test --verbosity normal
```

## Submitting a Pull Request
1. Open an Issue first if you intend to make a major architectural change.
2. Ensure your branch is rebased on `master`.
3. Provide benchmark data. If your PR modifies the pipeline, you must prove that patchification throughput (MB/s) and memory footprint (10.06 MB) have not degraded.
