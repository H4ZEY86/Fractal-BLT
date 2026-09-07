using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using FractalStreamer;

namespace FractalBridge;

/// <summary>
/// Decouples NVMe disk reads and GPU memory copies using System.Threading.Channels.
/// </summary>
public class AsyncPipelineCoordinator
{
    private readonly MoEStagingBuffer _stagingBuffer;
    private readonly ITensorStreamer _streamer;
    private readonly Channel<MoEBlock> _gpuQueue;
    private readonly IntPtr _cudaStream;
    
    public AsyncPipelineCoordinator(MoEStagingBuffer stagingBuffer, ITensorStreamer streamer)
    {
        _stagingBuffer = stagingBuffer;
        _streamer = streamer;
        
        // Initialize an unbuffered channel to hand off blocks instantly
        _gpuQueue = Channel.CreateUnbounded<MoEBlock>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        // Initialize CUDA context and stream (assuming device 0 for scaffolding)
        CudaNative.cuInit(0);
        CudaNative.cuDeviceGet(out int device, 0);
        CudaNative.cuCtxCreate(out IntPtr ctx, 0, device);
        CudaNative.cuStreamCreate(out _cudaStream, 0);
    }

    /// <summary>
    /// Disk I/O loop: Reads chunks from NVMe into Empty blocks and queues them.
    /// </summary>
    public async Task StartDiskThreadAsync(string safetensorsPath, long[] offsets, long[] sizes)
    {
        int blockIndex = 0;
        for (int i = 0; i < offsets.Length; i++)
        {
            var block = _stagingBuffer.Blocks[blockIndex];
            
            // Spin-wait or lock-free wait for the block to become Empty
            while (block.State != BlockState.Empty)
            {
                await Task.Yield();
            }

            block.State = BlockState.Filling;
            
            // Map directly into the block's page-aligned unmanaged host memory
            // Note: TensorReader currently returns a span; in a true zero-copy pipeline, 
            // TensorReader would accept the void* to read directly into it.
            // For scaffolding, we assume the streamer populates the memory.
            unsafe
            {
                var span = _streamer.MapTensorChunk(safetensorsPath, offsets[i], sizes[i]);
            }
            
            // In a highly optimized flow, TensorReader reads DIRECTLY into block.HostPtr.
            // Assuming that happens here, we just set the valid bytes.
            block.ValidBytes = sizes[i];
            
            block.State = BlockState.ReadyForGPU;
            await _gpuQueue.Writer.WriteAsync(block);

            blockIndex = (blockIndex + 1) % _stagingBuffer.Blocks.Length;
        }
        
        _gpuQueue.Writer.Complete();
    }

    /// <summary>
    /// GPU loop: Receives Ready blocks, initiates HtoD async copies, and recycles blocks.
    /// </summary>
    public async Task StartGpuThreadAsync()
    {
        await foreach (var block in _gpuQueue.Reader.ReadAllAsync())
        {
            block.State = BlockState.Copying;

            unsafe
            {
                // Submit async HtoD memory copy on the dedicated stream
                CudaNative.cuMemcpyHtoDAsync(
                    block.DevicePtr,
                    (IntPtr)block.HostPtr,
                    (nuint)block.ValidBytes,
                    _cudaStream);
            }

            // Synchronize stream (blocks GPU thread, but not Disk thread)
            // In a fully async system, use cuStreamAddCallback to recycle blocks.
            CudaNative.cuStreamSynchronize(_cudaStream);

            // Recycle the block
            block.State = BlockState.Empty;
        }
    }
}
