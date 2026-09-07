using System;
using System.Text;
using System.IO;
using Xunit;
using FractalBltEncoder;
using FractalGnnRouter;
using FractalStreamer;
using FractalBridge;

namespace FractalStreamer.Tests;

public class FractalIntegrationTests
{
    // The PTX payload for our tests, identical to the one in FractalServe.
    private const string TestSgemv = @"
.version 6.0
.target sm_50
.address_size 64

.visible .entry gemv(
    .param .u64 W,
    .param .u64 X,
    .param .u64 Y,
    .param .u32 rows,
    .param .u32 cols
)
{
    .reg .u32 %r<10>;
    .reg .u64 %rd<10>;
    .reg .f32 %f<10>;
    .reg .pred %p;

    ld.param.u64 %rd1, [W];
    ld.param.u64 %rd2, [X];
    ld.param.u64 %rd3, [Y];
    ld.param.u32 %r1, [rows];
    ld.param.u32 %r2, [cols];

    mov.u32 %r3, %ctaid.x;
    mov.u32 %r4, %ntid.x;
    mov.u32 %r5, %tid.x;
    mad.lo.s32 %r6, %r3, %r4, %r5; 

    setp.ge.u32 %p, %r6, %r1;
    @%p bra DONE;

    mov.f32 %f1, 0f00000000;
    mov.u32 %r7, 0; 

LOOP:
    setp.ge.u32 %p, %r7, %r2;
    @%p bra WRITE_OUT;

    mul.wide.u32 %rd4, %r7, 4;
    add.s64 %rd5, %rd2, %rd4;
    ld.global.f32 %f2, [%rd5];

    mad.lo.s32 %r8, %r6, %r2, %r7;
    mul.wide.u32 %rd6, %r8, 4;
    add.s64 %rd7, %rd1, %rd6;
    ld.global.f32 %f3, [%rd7];

    fma.rn.f32 %f1, %f3, %f2, %f1;

    add.s32 %r7, %r7, 1;
    bra LOOP;

WRITE_OUT:
    mul.wide.u32 %rd8, %r6, 4;
    add.s64 %rd9, %rd3, %rd8;
    st.global.f32 [%rd9], %f1;

DONE:
    ret;
}
";

    [Fact]
    public void FullPipeline_WithHardwareCudaExecution()
    {
        // 0. Safety Check for CUDA Driver presence
        try 
        {
            CudaNative.Init(0);
            CudaNative.DeviceGet(out int testDevice, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("CUDA driver not available or initialization failed: " + ex.Message);
            return;
        }

        // 1. Data Preparation
        string sourceCode = @"
public class ComputeNode 
{ 
    public int Execute() 
    { 
        return 42; 
    } 
}";
        byte[] inputBytes = Encoding.UTF8.GetBytes(sourceCode);
        var scorer = new ShannonEntropyScorer();
        var boundaries = new PatchBoundary[inputBytes.Length / 2 + 1];

        // 2. Execute True Shannon Entropy Patcher
        int patchCount = BltEncoder.Patchify(inputBytes, 4.0f, boundaries, ref scorer, 32);
        Assert.True(patchCount > 0, "Patcher should generate at least one patch.");

        // 3. Setup Expert Registry
        var expertRegistry = new ExpertRegistry();
        expertRegistry.InitializeRandom(64);

        var routes = new RouteAssignment[patchCount];

        // 4. Execute GNN Router
        int routeCount = GnnRouter.ComputeRoutes(new ReadOnlySpan<PatchBoundary>(boundaries, 0, patchCount), ref expertRegistry, routes);
        Assert.Equal(patchCount, routeCount);

        // Take the first route's expert id
        ushort expertId = routes[0].ExpertId;

        // 5. Tensor Streaming and PTX Execution
        string testModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tiny-test-model.safetensors");
        CreateDummySafetensorsFile(testModelPath);

        try 
        {
            // Parse offsets
            Assert.True(SafetensorsHeaderParser.TryGetTensorOffsets(testModelPath, "lm_head.weight", out long offset, out long length));

            unsafe 
            {
                uint cols = 4096;
                uint rows = (uint)(length / (cols * sizeof(float)));
                if (rows == 0 || length % (cols * sizeof(float)) != 0) 
                {
                    cols = 1024;
                    rows = (uint)(length / (cols * sizeof(float)));
                    if (rows == 0) rows = 1;
                }

                rows = Math.Min(rows, 16); // Limit rows for testing
                long readLength = rows * cols * sizeof(float);

                void* hostWeights = System.Runtime.InteropServices.NativeMemory.Alloc((nuint)readLength);
                float* hostInput = (float*)System.Runtime.InteropServices.NativeMemory.Alloc((nuint)(cols * sizeof(float)));
                float* hostOutput = (float*)System.Runtime.InteropServices.NativeMemory.Alloc((nuint)(rows * sizeof(float)));

                for (int i = 0; i < cols; i++) hostInput[i] = 1.0f; // Simplified input

                try 
                {
                    // Stream from disk
                    using var reader = new TensorReader();
                    reader.ReadInto(testModelPath, offset, (int)readLength, hostWeights);

                    // Execute CUDA
                    CudaNative.DeviceGet(out int device, 0);
                    CudaNative.CtxCreate(out IntPtr ctx, 0, device);
                    try 
                    {
                        CudaNative.StreamCreate(out IntPtr hStream, 0);
                        IntPtr ptxPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(TestSgemv);
                        CudaNative.ModuleLoadData(out IntPtr module, ptxPtr);
                        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptxPtr);

                        CudaNative.ModuleGetFunction(out IntPtr hfunc, module, "gemv");

                        CudaNative.MemAlloc(out IntPtr dW, (nuint)readLength);
                        CudaNative.MemAlloc(out IntPtr dX, (nuint)(cols * sizeof(float)));
                        CudaNative.MemAlloc(out IntPtr dY, (nuint)(rows * sizeof(float)));

                        CudaNative.MemcpyHtoDAsync(dW, (IntPtr)hostWeights, (nuint)readLength, hStream);
                        CudaNative.MemcpyHtoDAsync(dX, (IntPtr)hostInput, (nuint)(cols * sizeof(float)), hStream);

                        void*[] args = new void*[] { &dW, &dX, &dY, &rows, &cols };
                        fixed (void** pArgs = args)
                        {
                            uint blockDimX = 256;
                            uint gridDimX = (rows + blockDimX - 1) / blockDimX;
                            CudaNative.LaunchKernel(hfunc, gridDimX, 1, 1, blockDimX, 1, 1, 0, hStream, (IntPtr)pArgs, IntPtr.Zero);
                        }

                        CudaNative.MemcpyDtoHAsync((IntPtr)hostOutput, dY, (nuint)(rows * sizeof(float)), hStream);
                        CudaNative.StreamSynchronize(hStream);

                        CudaNative.MemFree(dW);
                        CudaNative.MemFree(dX);
                        CudaNative.MemFree(dY);
                        CudaNative.cuStreamDestroy(hStream);
                        
                        // Verification: At least some math happened
                        Assert.NotEqual(0.0f, hostOutput[0]);
                    }
                    finally
                    {
                        CudaNative.cuCtxDestroy(ctx);
                    }
                }
                finally 
                {
                    System.Runtime.InteropServices.NativeMemory.Free(hostWeights);
                    System.Runtime.InteropServices.NativeMemory.Free(hostInput);
                    System.Runtime.InteropServices.NativeMemory.Free(hostOutput);
                }
            }
        }
        finally
        {
            if (File.Exists(testModelPath)) File.Delete(testModelPath);
        }
    }

    private void CreateDummySafetensorsFile(string path)
    {
        string header = "{\"lm_head.weight\":{\"dtype\":\"F32\",\"shape\":[16,1024],\"data_offsets\":[0,65536]},\"__metadata__\":{\"format\":\"pt\"}}";
        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        long headerLength = headerBytes.Length;

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        
        byte[] lengthBytes = BitConverter.GetBytes((long)headerLength);
        fs.Write(lengthBytes, 0, 8);
        fs.Write(headerBytes, 0, headerBytes.Length);

        // Write 65536 bytes of floats (16 * 1024 * 4)
        byte[] tensorData = new byte[65536];
        for (int i = 0; i < tensorData.Length; i++)
        {
            tensorData[i] = 1; // Populate with 1s so output isn't 0
        }
        fs.Write(tensorData, 0, tensorData.Length);
    }
}
