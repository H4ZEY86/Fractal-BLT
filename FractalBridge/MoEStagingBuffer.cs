using System;
using System.Runtime.InteropServices;

namespace FractalBridge;

public enum BlockState
{
    Empty,
    Filling,
    ReadyForGPU,
    Copying
}

public unsafe class MoEBlock : IDisposable
{
    public void* HostPtr { get; private set; }
    public IntPtr DevicePtr { get; private set; }
    public long Capacity { get; }
    public BlockState State { get; set; }
    public long ValidBytes { get; set; }

    public MoEBlock(long capacity)
    {
        Capacity = capacity;
        State = BlockState.Empty;
        ValidBytes = 0;

        int pageSize = Environment.SystemPageSize;
        long allocSize = (capacity + pageSize - 1) & ~(pageSize - 1);
        
        // Pre-allocate page-aligned unmanaged host memory
        HostPtr = NativeMemory.AlignedAlloc((nuint)allocSize, (nuint)pageSize);

        // Register host memory with CUDA to lock it in physical RAM for true async DMA
        CudaNative.Check(CudaNative.cuMemHostRegister((IntPtr)HostPtr, (nuint)allocSize, 0));

        // Pre-allocate device memory
        CudaNative.Check(CudaNative.cuMemAlloc(out IntPtr dPtr, (nuint)allocSize));
        DevicePtr = dPtr;
    }

    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            if (HostPtr != null)
            {
                CudaNative.cuMemHostUnregister((IntPtr)HostPtr);
                NativeMemory.AlignedFree(HostPtr);
                HostPtr = null;
            }
            if (DevicePtr != IntPtr.Zero)
            {
                CudaNative.cuMemFree(DevicePtr);
                DevicePtr = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Manages a staging ring of pre-allocated blocks for double/triple buffering.
/// </summary>
public class MoEStagingBuffer : IDisposable
{
    public MoEBlock[] Blocks { get; }

    public MoEStagingBuffer(int ringSize, long blockCapacity)
    {
        Blocks = new MoEBlock[ringSize];
        for (int i = 0; i < ringSize; i++)
        {
            Blocks[i] = new MoEBlock(blockCapacity);
        }
    }

    public void Dispose()
    {
        foreach (var block in Blocks)
        {
            block.Dispose();
        }
    }
}
