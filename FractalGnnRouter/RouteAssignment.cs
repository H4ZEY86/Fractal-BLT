using System.Runtime.InteropServices;

namespace FractalGnnRouter;

/// <summary>
/// A strictly blittable, zero-allocation struct that defines a mapping between 
/// a specific latent patch and a target MoE expert.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct RouteAssignment
{
    public readonly int PatchIndex;
    public readonly ushort ExpertId;
    public readonly float Weight;

    public RouteAssignment(int patchIndex, ushort expertId, float weight)
    {
        PatchIndex = patchIndex;
        ExpertId = expertId;
        Weight = weight;
    }
}
