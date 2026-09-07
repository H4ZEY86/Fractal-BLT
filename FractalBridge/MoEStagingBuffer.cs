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
    private readonly bool _useCuda;

    public MoEBlock(long capacity, bool useCuda = true)
    {
        Capacity = capacity;
        State = BlockState.Empty;
        ValidBytes = 0;
        _useCuda = useCuda;

        int pageSize = Environment.SystemPageSize;
        long allocSize = (capacity + pageSize - 1) & ~(pageSize - 1);
        
        // Pre-allocate page-aligned unmanaged host memory
        HostPtr = NativeMemory.AlignedAlloc((nuint)allocSize, (nuint)pageSize);

        if (useCuda)
        {
            // Register host memory with CUDA to lock it in physical RAM for true async DMA
            CudaNative.Check(CudaNative.cuMemHostRegister((IntPtr)HostPtr, (nuint)allocSize, 0));

            // Pre-allocate device memory
            CudaNative.Check(CudaNative.cuMemAlloc(out IntPtr dPtr, (nuint)allocSize));
            DevicePtr = dPtr;
        }
    }

    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            if (HostPtr != null)
            {
                if (_useCuda)
                {
                    CudaNative.cuMemHostUnregister((IntPtr)HostPtr);
                }
                NativeMemory.AlignedFree(HostPtr);
                HostPtr = null;
            }
            if (DevicePtr != IntPtr.Zero && _useCuda)
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

    public MoEStagingBuffer(int ringSize, long blockCapacity, bool useCuda = true)
    {
        Blocks = new MoEBlock[ringSize];
        for (int i = 0; i < ringSize; i++)
        {
            Blocks[i] = new MoEBlock(blockCapacity, useCuda);
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
