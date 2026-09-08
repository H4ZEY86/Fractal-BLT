using System;
using System.IO;

namespace FractalStreamer;

/// <summary>
/// A diagnostic utility to parse and inspect the safetensors file structure and its
/// routing weights/bias vectors directly from NVMe without large managed allocations.
/// </summary>
public static class RouterInspector
{
    public static void InspectModel(string modelPath)
    {
        Console.WriteLine($"=== Router Inspector: {modelPath} ===");

        try
        {
            var tensorNames = SafetensorsHeaderParser.GetAllTensorNames(modelPath);
            Console.WriteLine($"Found {tensorNames.Count} tensors in the header.");

            // Output first 10 tensors as a sample
            for (int i = 0; i < Math.Min(10, tensorNames.Count); i++)
            {
                if (SafetensorsHeaderParser.TryGetTensorOffsets(modelPath, tensorNames[i], out long offset, out long length))
                {
                    Console.WriteLine($"Tensor '{tensorNames[i]}': Offset = {offset}, Length = {length} bytes");
                }
            }

            Console.WriteLine("======================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error Inspecting Model]: {ex.Message}");
        }
    }
}
