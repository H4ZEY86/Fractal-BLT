using System;

namespace FractalBltEncoder;

/// <summary>
/// A high-performance, dynamic byte-level patching engine designed to group
/// raw UTF-8 bytes into variable-length latent patches based on cross-entropy thresholds.
/// </summary>
public static class BltEncoder
{
    /// <summary>
    /// Evaluates a span of raw bytes and dynamically segments them into boundaries.
    /// Strictly O(N), lock-free, zero-allocation over the hot path.
    /// </summary>
    /// <param name="input">The raw UTF-8 byte stream.</param>
    /// <param name="entropyThreshold">The cumulative or peak threshold that triggers a patch seal.</param>
    /// <param name="outputBuffer">A pre-allocated buffer for resulting boundaries.</param>
    /// <param name="scorer">The entropy scorer engine.</param>
    /// <param name="maxPatchLength">The absolute maximum length of a patch before a forced seal.</param>
    /// <typeparam name="TScorer">The scorer type, constrained to struct to eliminate virtual dispatch via devirtualization.</typeparam>
    /// <returns>The number of boundaries written.</returns>
    public static int Patchify<TScorer>(
        ReadOnlySpan<byte> input, 
        float entropyThreshold, 
        Span<PatchBoundary> outputBuffer, 
        ref TScorer scorer,
        int maxPatchLength = 32) where TScorer : struct, IEntropyScorer
    {
        int patchCount = 0;
        int currentPatchStart = 0;
        float currentPeakEntropy = 0f;

        for (int i = 0; i < input.Length; i++)
        {
            byte b = input[i];
            
            // The history spans from the start of the current patch to the current byte.
            ReadOnlySpan<byte> history = input.Slice(currentPatchStart, i - currentPatchStart);
            
            float entropy = scorer.ScoreNextByte(b, history);
            
            if (entropy > currentPeakEntropy)
            {
                currentPeakEntropy = entropy;
            }

            int currentLength = i - currentPatchStart + 1;

            // Trigger a patch seal if threshold is reached OR max length is hit.
            // Also force a seal on the final byte of the input.
            if (currentPeakEntropy >= entropyThreshold || currentLength >= maxPatchLength || i == input.Length - 1)
            {
                if (patchCount >= outputBuffer.Length)
                {
                    throw new InvalidOperationException("Output buffer capacity exhausted before completing the patching loop.");
                }

                outputBuffer[patchCount++] = new PatchBoundary(
                    startOffset: currentPatchStart,
                    length: currentLength,
                    peakEntropy: currentPeakEntropy
                );

                // Reset for the next patch
                currentPatchStart = i + 1;
                currentPeakEntropy = 0f;
            }
        }

        return patchCount;
    }
}
