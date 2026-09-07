using System;

namespace FractalServe;

public static class PtxKernels
{
    // A simple Matrix-Vector Multiplication (SGEMV) PTX Kernel.
    // Computes: Y = W * X
    // W is a (rows x cols) matrix in row-major order.
    // X is a (cols) vector.
    // Y is a (rows) vector.
    // Each thread computes one element of Y.
    public const string Sgemv = @"
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

    // row = blockIdx.x * blockDim.x + threadIdx.x
    mov.u32 %r3, %ctaid.x;
    mov.u32 %r4, %ntid.x;
    mov.u32 %r5, %tid.x;
    mad.lo.s32 %r6, %r3, %r4, %r5; // %r6 = row

    setp.ge.u32 %p, %r6, %r1;
    @%p bra DONE;

    mov.f32 %f1, 0f00000000; // sum = 0.0
    mov.u32 %r7, 0; // col = 0

LOOP:
    setp.ge.u32 %p, %r7, %r2;
    @%p bra WRITE_OUT;

    // Load X[col]
    mul.wide.u32 %rd4, %r7, 4;
    add.s64 %rd5, %rd2, %rd4;
    ld.global.f32 %f2, [%rd5];

    // Load W[row * cols + col]
    mad.lo.s32 %r8, %r6, %r2, %r7;
    mul.wide.u32 %rd6, %r8, 4;
    add.s64 %rd7, %rd1, %rd6;
    ld.global.f32 %f3, [%rd7];

    fma.rn.f32 %f1, %f3, %f2, %f1;

    add.s32 %r7, %r7, 1;
    bra LOOP;

WRITE_OUT:
    // Store Y[row]
    mul.wide.u32 %rd8, %r6, 4;
    add.s64 %rd9, %rd3, %rd8;
    st.global.f32 [%rd9], %f1;

DONE:
    ret;
}
";
}
