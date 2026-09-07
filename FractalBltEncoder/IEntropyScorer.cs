using System;

namespace FractalBltEncoder;

/// <summary>
/// Defines the contract for an entropy scoring model that dynamically determines
/// the information density of the next byte given a sequence of history.
/// </summary>
public interface IEntropyScorer
{
    /// <summary>
    /// Scores the cross-entropy or surprise of the next byte.
    /// </summary>
    /// <param name="nextByte">The byte to score.</param>
    /// <param name="history">The recent byte history (e.g., current patch context).</param>
    /// <returns>The calculated entropy score.</returns>
    float ScoreNextByte(byte nextByte, ReadOnlySpan<byte> history);
}
