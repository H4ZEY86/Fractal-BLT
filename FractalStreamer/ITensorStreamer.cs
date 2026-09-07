using System;

namespace FractalStreamer;

/// <summary>
/// Defines the foundational contract for the NVMe-to-GPU tensor streaming layer.
/// Implementations must guarantee zero-allocation in the hot path.
/// </summary>
public interface ITensorStreamer : IDisposable
{
    /// <summary>
    /// Reads a chunk of a safetensors file directly into a caller‑provided destination.
    /// </summary>
    /// <param name="filePath">Path to the safetensors file.</param>
    /// <param name="byteOffset">Byte offset within the file to start reading from.</param>
    /// <param name="byteSize">Number of bytes to read.</param>
    /// <param name="destination">Pointer to the destination memory.</param>
    unsafe void ReadInto(string filePath, long byteOffset, int byteSize, void* destination);

    /// <summary>
    /// Legacy overload retained for managed callers – reads into a Span<byte>.
    /// </summary>
    [Obsolete("Use ReadInto with a pre-allocated pointer for true zero-copy.")]
    unsafe Span<byte> MapTensorChunk(string filePath, long byteOffset, long byteSize);
}
