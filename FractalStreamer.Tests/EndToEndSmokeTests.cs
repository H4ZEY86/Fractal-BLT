using System;
using System.IO;
using System.Threading.Tasks;
using FractalBridge;
using Xunit;

namespace FractalStreamer.Tests;

public class EndToEndSmokeTests : IDisposable
{
    private readonly string _tempFilePath;
    private readonly int _pageSize;

    public EndToEndSmokeTests()
    {
        _pageSize = Environment.SystemPageSize;
        _tempFilePath = Path.GetTempFileName();
        
        // Write some dummy data for testing offsets
        byte[] data = new byte[_pageSize * 4];
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
    public async Task AsyncPipelineCoordinator_CpuPath_RunsSuccessfully()
    {
        long[] offsets = { _pageSize, _pageSize + 42 }; 
        long[] sizes = { 100, 100 };
        
        using var stagingBuffer = new MoEStagingBuffer(ringSize: 2, blockCapacity: _pageSize, useCuda: false);
        using var streamer = new TensorReader();
        using var coordinator = new AsyncPipelineCoordinator(stagingBuffer, streamer, channelCapacity: 2, useCuda: false);
        
        await coordinator.RunAsync(_tempFilePath, offsets, sizes);
        Assert.True(true);
    }

    [Fact]
    public async Task AsyncPipelineCoordinator_GpuPath_RunsSuccessfully()
    {
        IntPtr ctx = IntPtr.Zero;
        try
        {
            CudaNative.cuInit(0);
            CudaNative.cuDeviceGet(out int device, 0);
            CudaNative.cuCtxCreate(out ctx, 0, device);
        }
        catch (Exception)
        {
            Console.WriteLine("CUDA device not found. Skipping GPU test.");
            return;
        }

        try
        {
            long[] offsets = { _pageSize, _pageSize + 42 }; 
            long[] sizes = { 100, 100 };
            
            using var stagingBuffer = new MoEStagingBuffer(ringSize: 2, blockCapacity: _pageSize, useCuda: true);
            using var streamer = new TensorReader();
            using var coordinator = new AsyncPipelineCoordinator(stagingBuffer, streamer, channelCapacity: 2, useCuda: true);
            
            await coordinator.RunAsync(_tempFilePath, offsets, sizes);
            Assert.True(true);
        }
        finally
        {
            if (ctx != IntPtr.Zero)
            {
                CudaNative.cuCtxDestroy(ctx);
            }
        }
    }
}
