using System;
using System.Text;
using Xunit;
using FractalBltEncoder;

namespace FractalStreamer.Tests;

public class EntropyTests
{
    [Fact]
    public void ShannonEntropyScorer_CalculatesCorrectEntropy()
    {
        // Arrange
        var scorer = new ShannonEntropyScorer();
        byte[] history = Encoding.UTF8.GetBytes("hello worl");
        byte nextByte = (byte)'d';

        // Act
        float entropy = scorer.ScoreNextByte(nextByte, history);

        // Assert
        // "hello world" has 11 characters. 
        // Counts: h=1, e=1, l=3, o=2, space=1, w=1, r=1, d=1 -> 6 ones, one 3, one 2.
        // Total = 11.
        float inv = 1.0f / 11.0f;
        float expected = -(6 * (inv * MathF.Log2(inv)) + (3*inv * MathF.Log2(3*inv)) + (2*inv * MathF.Log2(2*inv)));
        
        Assert.Equal(expected, entropy, 4); // precision of 4 decimals
    }

    [Fact]
    public void ShannonEntropyScorer_HandlesAllSameBytes_EntropyZero()
    {
        // Arrange
        var scorer = new ShannonEntropyScorer();
        byte[] history = new byte[100];
        Array.Fill(history, (byte)'A');
        
        // Act
        float entropy = scorer.ScoreNextByte((byte)'A', history);

        // Assert
        // All bytes are the same, probability is 1.0, log2(1.0) = 0. So entropy must be 0.
        Assert.Equal(0.0f, entropy, 4);
    }
}
