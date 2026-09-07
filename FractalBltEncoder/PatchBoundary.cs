using System.Runtime.InteropServices;

namespace FractalBltEncoder;

/// <summary>
/// A strictly blittable, unmanaged struct representing the boundary of a latent patch.
/// Designed to map directly into CUDA staging buffers.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PatchBoundary
{
    public readonly int StartOffset;
    public readonly int Length;
    public readonly float PeakEntropy;

    public PatchBoundary(int startOffset, int length, float peakEntropy)
    {
        StartOffset = startOffset;
        Length = length;
        PeakEntropy = peakEntropy;
    }
}
