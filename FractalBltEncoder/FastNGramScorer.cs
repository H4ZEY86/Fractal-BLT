using System;

namespace FractalBltEncoder;

/// <summary>
/// A fast, mock implementation of IEntropyScorer simulating an n-gram model.
/// Assigns low entropy to common ASCII and high entropy to rare/non-ASCII bytes.
/// </summary>
public struct FastNGramScorer : IEntropyScorer
{
    public float ScoreNextByte(byte nextByte, ReadOnlySpan<byte> history)
    {
        // Mock logic: 
        // 1. Lowercase ASCII letters and spaces are highly predictable (low entropy).
        // 2. Uppercase ASCII, numbers, and common punctuation have medium entropy.
        // 3. Control characters or non-ASCII (UTF-8 multi-byte) are high entropy.

        if (nextByte == 32 || (nextByte >= 97 && nextByte <= 122)) // space or 'a'-'z'
        {
            return 0.1f;
        }
        
        if ((nextByte >= 65 && nextByte <= 90) || (nextByte >= 48 && nextByte <= 57)) // 'A'-'Z', '0'-'9'
        {
            return 0.5f;
        }

        if (nextByte < 128) // other ASCII (punctuation)
        {
            return 1.2f;
        }

        // Non-ASCII UTF-8 bytes trigger high entropy indicating a complex boundary.
        return 3.5f;
    }
}
