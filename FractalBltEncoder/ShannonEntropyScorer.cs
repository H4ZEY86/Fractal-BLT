using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FractalBltEncoder;

/// <summary>
/// A true Shannon entropy implementation of IEntropyScorer.
/// Computes H = -sum(p_i * log2(p_i)) over the history window.
/// Strictly zero-allocation using stackalloc for frequency tables.
/// Vectorized using Vector256 to quickly skip empty histogram blocks.
/// </summary>
public struct ShannonEntropyScorer : IEntropyScorer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe float ScoreNextByte(byte nextByte, ReadOnlySpan<byte> history)
    {
        // 256-element frequency table on the stack (1024 bytes)
        int* counts = stackalloc int[256];
        
        int totalLength = history.Length + 1;

        // Tally history
        for (int i = 0; i < history.Length; i++)
        {
            counts[history[i]]++;
        }
        
        // Tally the current byte
        counts[nextByte]++;

        float entropy = 0f;
        float invTotal = 1.0f / totalLength;

        // Unroll loop for non-zero counts using Vector256
        int iBlock = 0;
        if (Avx2.IsSupported)
        {
            Vector256<int> vZero = Vector256<int>.Zero;
            for (; iBlock <= 256 - 8; iBlock += 8)
            {
                Vector256<int> vCounts = Avx.LoadVector256(counts + iBlock);
                
                // If all 8 counts are zero, skip this block
                if (Avx2.MoveMask(Avx2.CompareEqual(vCounts, vZero).AsByte()) == -1)
                {
                    continue;
                }

                // Otherwise, compute entropy for non-zero elements
                for (int j = 0; j < 8; j++)
                {
                    int count = counts[iBlock + j];
                    if (count > 0)
                    {
                        float p = count * invTotal;
                        entropy -= p * MathF.Log2(p);
                    }
                }
            }
        }

        // Remainder loop
        for (; iBlock < 256; iBlock++)
        {
            int count = counts[iBlock];
            if (count > 0)
            {
                float p = count * invTotal;
                entropy -= p * MathF.Log2(p);
            }
        }

        return entropy;
    }
}
