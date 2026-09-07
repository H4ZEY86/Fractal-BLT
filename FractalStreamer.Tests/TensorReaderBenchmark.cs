using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace FractalStreamer.Tests;

public class TensorReaderBenchmark : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempFilePath;
    private readonly int _pageSize;

    public TensorReaderBenchmark(ITestOutputHelper output)
    {
        _output = output;
        _pageSize = Environment.SystemPageSize;
        _tempFilePath = Path.GetTempFileName();
        
        // Write 10 MB of dummy data
        byte[] data = new byte[10 * 1024 * 1024];
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
    public unsafe void Benchmark_StagingVsAligned()
    {
        int iterations = 1000;
        int bytesToRead = _pageSize * 4;
        void* destPtr = NativeMemory.AlignedAlloc((nuint)bytesToRead, (nuint)_pageSize);

        try
        {
            using var reader = new TensorReader();

            // Warmup
            reader.ReadInto(_tempFilePath, _pageSize, bytesToRead, destPtr);
            reader.ReadInto(_tempFilePath, _pageSize + 42, bytesToRead, destPtr);

            // Aligned (Fast Path)
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                reader.ReadInto(_tempFilePath, _pageSize, bytesToRead, destPtr);
            }
            sw.Stop();
            double alignedMs = sw.Elapsed.TotalMilliseconds;

            // Unaligned (Staging Path)
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                reader.ReadInto(_tempFilePath, _pageSize + 42, bytesToRead, destPtr);
            }
            sw.Stop();
            double unalignedMs = sw.Elapsed.TotalMilliseconds;

            _output.WriteLine($"[Benchmark] {iterations} iterations of {bytesToRead} bytes.");
            _output.WriteLine($"[Benchmark] Aligned Path:   {alignedMs:F2} ms");
            _output.WriteLine($"[Benchmark] Unaligned Path: {unalignedMs:F2} ms");
            _output.WriteLine($"[Benchmark] Overhead:       {(unalignedMs / alignedMs):F2}x");

            // Since it's a diagnostic test, we just pass
            Assert.True(true);
        }
        finally
        {
            NativeMemory.AlignedFree(destPtr);
        }
    }
}
