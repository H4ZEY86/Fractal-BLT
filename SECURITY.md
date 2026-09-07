# Security Policy

## Supported Versions
Only the latest master branch and the most recent major release (`v1.x.x`) receive security updates.

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Threat Model & Accepted Risks

Fractal-BLT is a high-performance bare-metal inference runtime. By design, it bypasses several traditional OS safety mechanisms to achieve zero-latency throughput:
1. **Unmanaged Memory Management**: We use `NativeMemory.Alloc` and raw pointers extensively. Buffer overflows in user-supplied weights are possible if `.safetensors` headers are maliciously crafted.
2. **Raw PTX Execution**: We pass JIT-compiled PTX strings directly into the NVIDIA Driver.
3. **Direct NVMe DMA**: Bypassing the OS page cache means the application has direct, unbuffered disk access.

**Do NOT run FractalServe on an exposed public network without a reverse proxy (like NGINX) handling TLS and authentication.**

## Reporting a Vulnerability

If you discover a memory safety escape that allows Remote Code Execution (RCE) via a standard `/v1/chat/completions` REST payload, please do NOT create a public issue. 

Email the maintainers directly at `security@fractal-blt.internal` (Placeholder). We will acknowledge receipt within 48 hours.
