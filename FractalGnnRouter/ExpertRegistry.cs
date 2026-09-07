using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FractalGnnRouter;

/// <summary>
/// Simulates a 64-element embedding inline array (C# 12+) to hold MoE expert embeddings
/// contiguously in an unmanaged context without resorting to heap arrays.
/// </summary>
[InlineArray(64)]
public struct ExpertEmbeddings
{
    // The underlying element type
    private float _element;
}

/// <summary>
/// Houses active Expert IDs and their semantic embeddings in an unmanaged struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ExpertRegistry
{
    public int ActiveCount;

    // Use C# 12 inline array for embeddings
    public ExpertEmbeddings Embeddings;

    // Use a fixed array for Expert IDs. We could also use an InlineArray here,
    // but fixed ushort is fine in an unsafe context. For modern C#, another InlineArray is safer.
    // We'll use fixed here to demonstrate unmanaged structure alignment.
    public unsafe fixed ushort ExpertIds[64];

    public unsafe void InitializeRandom(int count)
    {
        if (count > 64) count = 64;
        ActiveCount = count;

        for (int i = 0; i < count; i++)
        {
            ExpertIds[i] = (ushort)i;
            // Access the inline array
            Embeddings[i] = i * 0.1f; // Initial randomized semantic embedding value
        }
    }
}
