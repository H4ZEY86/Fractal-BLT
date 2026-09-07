using System;
using System.IO;
using System.Runtime.InteropServices;
using FractalStreamer;
using Xunit;

namespace FractalStreamer.Tests;

public class TensorReaderTests : IDisposable
{
    private readonly string _tempFilePath;
    private readonly int _pageSize;

    public TensorReaderTests()
    {
        _pageSize = Environment.SystemPageSize;
        _tempFilePath = Path.GetTempFileName();
        
        // Write 3 pages of data (e.g. 12 KB)
        byte[] data = new byte[_pageSize * 3];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }
        File.WriteAllBytes(_tempFilePath, data);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Fact]
    public unsafe void ReadInto_AlignedOffset_ReadsSuccessfully()
    {
        using var reader = new TensorReader();
        int readSize = 100;
        long offset = _pageSize; // Aligned
        void* buffer = NativeMemory.AlignedAlloc((nuint)readSize, (nuint)_pageSize);
        
        try
        {
            reader.ReadInto(_tempFilePath, offset, readSize, buffer);
            Span<byte> result = new Span<byte>(buffer, readSize);
            
            for (int i = 0; i < readSize; i++)
            {
                Assert.Equal((byte)((offset + i) % 256), result[i]);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }

    [Fact]
    public unsafe void ReadInto_UnalignedOffset_UsesStagingBufferAndReadsSuccessfully()
    {
        using var reader = new TensorReader();
        int readSize = 100;
        long offset = _pageSize + 42; // Unaligned
        void* buffer = NativeMemory.AlignedAlloc((nuint)readSize, (nuint)_pageSize);
        
        try
        {
            reader.ReadInto(_tempFilePath, offset, readSize, buffer);
            Span<byte> result = new Span<byte>(buffer, readSize);
            
            for (int i = 0; i < readSize; i++)
            {
                Assert.Equal((byte)((offset + i) % 256), result[i]);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(buffer);
        }
    }
}
