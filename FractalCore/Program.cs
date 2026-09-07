using System;
using System.IO;

namespace FractalCore;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("/// FRACTAL-CORE BOOT SEQUENCE INITIALIZED ///");
        
        if (args.Length > 0 && args[0] == "--verify")
        {
            string path = args.Length > 1 ? args[1] : @".\dummy_expert.safetensors";
            bool strict = args.Length > 2 && args[2] == "--strict";
            FractalStreamer.TensorReader.VerifySafetensorsAlignment(path, strict);
            return;
        }

        string dummySafetensorsPath = @".\dummy_expert.safetensors";
        
        // Generate a 10MB UTF-8 source payload
        byte[] sourcePayload = SyntheticGenerator.GenerateMockSourceCode(10 * 1024 * 1024);
        
        // Generate a 1GB dummy safetensors file for NVMe simulation (Optional: uncomment to physically test disk)
        // SyntheticGenerator.EnsureDummySafetensors(dummySafetensorsPath, 1024L * 1024L * 1024L);

        // Run the Gauntlet
        BenchmarkRunner.RunGauntlet(sourcePayload, dummySafetensorsPath);

        Console.WriteLine("/// FRACTAL-CORE BOOT SEQUENCE TERMINATED ///");
    }
}
