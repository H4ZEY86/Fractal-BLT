using System;

namespace FractalStreamer;

/// <summary>
/// Defines the foundational contract for the NVMe-to-GPU tensor streaming layer.
/// Implementations must guarantee zero-allocation in the hot path.
/// </summary>
public interface ITensorStreamer : IDisposable
{
    /// <summary>
    /// Maps a chunk of a safetensors file directly into unmanaged memory.
    /// </summary>
    /// <param name="filePath">Path to the safetensors file.</param>
    /// <param name="byteOffset">Byte offset within the file to start reading from.</param>
    /// <param name="byteSize">Number of bytes to map.</param>
    /// <returns>A span pointing to the loaded tensor chunk in unmanaged memory.</returns>
    unsafe Span<byte> MapTensorChunk(string filePath, long byteOffset, long byteSize);
}
