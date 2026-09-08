using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.IO.MemoryMappedFiles;

namespace FractalStreamer;

/// <summary>
/// Implements zero-allocation tensor reading using System.IO.RandomAccess 
/// and page-aligned unmanaged memory buffers.
/// </summary>
public unsafe class TensorReader : ITensorStreamer
{
    private void* _buffer;
    private long _bufferCapacity;
    private SafeFileHandle? _handle;
    private MemoryMappedFile? _mmf;
    private string? _currentFilePath;

    /// <summary>
    /// Reads a chunk of a safetensors file directly into a caller‑provided destination.
    /// Fast path: aligned offset -> unbuffered read directly into destination.
    /// Slow path: unaligned offset -> short-lived aligned staging buffer, copy, free.
    /// </summary>
    public void ReadInto(string filePath, long byteOffset, int byteSize, void* destination)
    {
        if (byteOffset < 0 || byteSize <= 0)
            throw new ArgumentOutOfRangeException("Offset and size must be positive.");
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        if (_currentFilePath != filePath || _handle == null || _handle.IsInvalid)
        {
            _handle?.Dispose();
            _mmf?.Dispose();
            _handle = UnbufferedFile.OpenUnbuffered(filePath);
            _mmf = MemoryMappedFile.CreateFromFile(_handle, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
            _currentFilePath = filePath;
        }

        long fileLength = RandomAccess.GetLength(_handle);
        if (byteOffset + byteSize > fileLength)
            throw new ArgumentOutOfRangeException(nameof(byteSize), "Requested range exceeds file length.");

        int pageSize = Environment.SystemPageSize;
        bool isAligned = (byteOffset % pageSize) == 0 && (byteSize % pageSize) == 0;

        if (isAligned)
        {
            // Fast path: direct unbuffered read into provided destination (which should also be aligned in production)
            Span<byte> targetSpan = new Span<byte>(destination, byteSize);
            int bytesRead = RandomAccess.Read(_handle, targetSpan, byteOffset);
            if (bytesRead != byteSize)
                throw new IOException($"Expected to read {byteSize} bytes, but read {bytesRead}.");
        }
        else
        {
            // Slow path: Staging buffer for unaligned offset
            long alignedOffset = byteOffset - (byteOffset % pageSize);
            int offsetDiff = (int)(byteOffset - alignedOffset);
            int alignedSize = (byteSize + offsetDiff + pageSize - 1) & ~(pageSize - 1);

            void* stagingBuffer = NativeMemory.AlignedAlloc((nuint)alignedSize, (nuint)pageSize);
            try
            {
                Span<byte> stagingSpan = new Span<byte>(stagingBuffer, alignedSize);
                int bytesRead = RandomAccess.Read(_handle, stagingSpan, alignedOffset);
                if (bytesRead < byteSize + offsetDiff)
                    throw new IOException($"Staging read failed to read enough bytes.");

                // Copy from staging buffer to destination
                Span<byte> destSpan = new Span<byte>(destination, byteSize);
                stagingSpan.Slice(offsetDiff, byteSize).CopyTo(destSpan);
            }
            finally
            {
                NativeMemory.AlignedFree(stagingBuffer);
            }
        }
    }

    /// <summary>
    /// Legacy overload retained for managed callers – reads into a Span<byte>.
    /// </summary>
    [Obsolete("Use ReadInto with a pre-allocated pointer for true zero-copy.")]
    public Span<byte> MapTensorChunk(string filePath, long byteOffset, long byteSize)
    {
        if (byteOffset < 0 || byteSize <= 0)
            throw new ArgumentOutOfRangeException("Offset and size must be positive.");

        if (_currentFilePath != filePath || _handle == null || _handle.IsInvalid)
        {
            _handle?.Dispose();
            _mmf?.Dispose();
            _handle = UnbufferedFile.OpenUnbuffered(filePath);
            _mmf = MemoryMappedFile.CreateFromFile(_handle, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
            _currentFilePath = filePath;
        }

        EnsureBufferCapacity(byteSize);

        // System.IO.RandomAccess provides highly optimized, direct read APIs
        // Since the handle is unbuffered, the buffer MUST be aligned and size MUST typically be a multiple of sector size.
        // We read exactly what is requested. (Note: Unbuffered reads usually require sector-aligned offsets and lengths,
        // so in a real-world scenario we might need to over-read and slice the span).
        
        long fileLength = RandomAccess.GetLength(_handle);
        if (byteOffset + byteSize > fileLength)
            throw new ArgumentOutOfRangeException(nameof(byteSize), "Requested range exceeds file length.");

        Span<byte> targetSpan = new Span<byte>(_buffer, (int)byteSize);
        
        // Blocking read directly into the native pointer
        // Unbuffered reads must be page-aligned in both memory and file offset
        if (byteOffset % Environment.SystemPageSize != 0)
        {
            throw new InvalidOperationException($"Zero-copy NVMe reads require file offsets to be aligned to {Environment.SystemPageSize} bytes. Unaligned offset: {byteOffset}");
        }

        int bytesRead = RandomAccess.Read(_handle, targetSpan, byteOffset);
        
        if (bytesRead != byteSize)
        {
            throw new IOException($"Expected to read {byteSize} bytes, but read {bytesRead}.");
        }

        return targetSpan;
    }

    public unsafe void* MapTensorChunkDirect(string filePath, long byteOffset, long byteSize, out IDisposable handle)
    {
        if (byteOffset < 0 || byteSize <= 0)
            throw new ArgumentOutOfRangeException("Offset and size must be positive.");

        if (_currentFilePath != filePath || _mmf == null)
        {
            _handle?.Dispose();
            _mmf?.Dispose();
            _handle = UnbufferedFile.OpenUnbuffered(filePath);
            _mmf = MemoryMappedFile.CreateFromFile(_handle, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
            _currentFilePath = filePath;
        }

        // Create a view accessor for the requested chunk
        var accessor = _mmf.CreateViewAccessor(byteOffset, byteSize, MemoryMappedFileAccess.Read);
        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        
        // We must return a disposable object that releases the pointer and disposes the accessor
        handle = new MmfViewHandle(accessor, ptr);
        return ptr + accessor.PointerOffset;
    }

    private class MmfViewHandle : IDisposable
    {
        private MemoryMappedViewAccessor _accessor;
        private unsafe byte* _ptr;

        public unsafe MmfViewHandle(MemoryMappedViewAccessor accessor, byte* ptr)
        {
            _accessor = accessor;
            _ptr = ptr;
        }

        public unsafe void Dispose()
        {
            if (_ptr != null)
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                _ptr = null;
            }
            _accessor?.Dispose();
        }
    }

    private void EnsureBufferCapacity(long requiredSize)
    {
        if (_buffer != null && _bufferCapacity >= requiredSize)
            return;

        if (_buffer != null)
        {
            NativeMemory.AlignedFree(_buffer);
            _buffer = null;
        }

        int pageSize = Environment.SystemPageSize;
        
        // Allocate a page-aligned buffer suitable for unbuffered I/O
        // Round up to nearest alignment boundary
        long allocSize = (requiredSize + pageSize - 1) & ~(pageSize - 1);
        _buffer = NativeMemory.AlignedAlloc((nuint)allocSize, (nuint)pageSize);
        _bufferCapacity = allocSize;
    }

    public void Dispose()
    {
        if (_buffer != null)
        {
            NativeMemory.AlignedFree(_buffer);
            _buffer = null;
            _bufferCapacity = 0;
        }
        
        _mmf?.Dispose();
        _mmf = null;
        _handle?.Dispose();
        _handle = null;
        _currentFilePath = null;
    }

    /// <summary>
    /// Diagnostically verifies that a SafeTensors file contains data offsets perfectly 
    /// aligned to the SystemPageSize, satisfying strict zero-copy DMA requirements.
    /// </summary>
    public static void VerifySafetensorsAlignment(string path, bool strict = false)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> lengthBuffer = stackalloc byte[8];
        fs.ReadExactly(lengthBuffer);
        long headerLength = BitConverter.ToInt64(lengthBuffer);

        byte[] headerBytes = new byte[headerLength];
        fs.ReadExactly(headerBytes);

        long pageSize = Environment.SystemPageSize;
        int totalTensors = 0;
        int alignedTensors = 0;

        using var jsonDoc = System.Text.Json.JsonDocument.Parse(headerBytes);
        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
        {
            if (prop.Name == "__metadata__") continue;
            
            totalTensors++;
            var dataOffsets = prop.Value.GetProperty("data_offsets");
            long startOffset = dataOffsets[0].GetInt64();
            long absoluteStart = 8 + headerLength + startOffset;
            
            if (absoluteStart % pageSize == 0)
            {
                alignedTensors++;
            }
            else if (strict)
            {
                throw new InvalidOperationException($"Strict alignment failed! Tensor '{prop.Name}' absolute start offset ({absoluteStart}) is not aligned to the {pageSize}-byte page boundary.");
            }
        }

        Console.WriteLine($"Alignment Stats for '{path}': {alignedTensors}/{totalTensors} tensors are page-aligned ({pageSize} bytes).");
    }
}
