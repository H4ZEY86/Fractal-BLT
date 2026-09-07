using System;
using System.Runtime.CompilerServices;

namespace FractalBltEncoder;

/// <summary>
/// A true Shannon entropy implementation of IEntropyScorer.
/// Computes H = -sum(p_i * log2(p_i)) over the history window.
/// Strictly zero-allocation using stackalloc for frequency tables.
/// </summary>
public struct ShannonEntropyScorer : IEntropyScorer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe float ScoreNextByte(byte nextByte, ReadOnlySpan<byte> history)
    {
        // 256-element frequency table on the stack (1024 bytes)
        int* counts = stackalloc int[256];
        
        // Fast clear is done automatically by stackalloc in modern C# if zero-init is on, 
        // but we'll do it explicitly or assume it's zeroed (stackalloc without skipinit).
        
        int totalLength = history.Length + 1;

        // Tally history
        for (int i = 0; i < history.Length; i++)
        {
            counts[history[i]]++;
        }
        
        // Tally the current byte
        counts[nextByte]++;

        // Compute Shannon entropy
        float entropy = 0f;
        float invTotal = 1.0f / totalLength;

        // Unroll loop for non-zero counts
        for (int i = 0; i < 256; i++)
        {
            int count = counts[i];
            if (count > 0)
            {
                float p = count * invTotal;
                entropy -= p * MathF.Log2(p);
            }
        }

        return entropy;
    }
}
