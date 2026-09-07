using System;
using System.IO;

namespace FractalCore;

public static class SyntheticGenerator
{
    public static byte[] GenerateMockSourceCode(int sizeInBytes)
    {
        Console.WriteLine($"[Synthetic] Generating {sizeInBytes / 1024.0 / 1024.0:F2} MB of mock UTF-8 source code...");
        var buffer = new byte[sizeInBytes];
        var random = new Random(42); // deterministic

        // Simulate code-like text: spaces, brackets, letters
        byte[] tokens = { 32, 32, 10, 123, 125, 40, 41, 59, 97, 98, 99, 100, 101, 102 };
        
        for (int i = 0; i < sizeInBytes; i++)
        {
            buffer[i] = tokens[random.Next(tokens.Length)];
        }

        return buffer;
    }

    public static void EnsureDummySafetensors(string path, long sizeInBytes)
    {
        if (File.Exists(path))
        {
            var info = new FileInfo(path);
            if (info.Length == sizeInBytes)
            {
                Console.WriteLine($"[Synthetic] Found existing {sizeInBytes / 1024.0 / 1024.0 / 1024.0:F2} GB safetensors file at {path}.");
                return;
            }
            File.Delete(path);
        }

        Console.WriteLine($"[Synthetic] Generating {sizeInBytes / 1024.0 / 1024.0 / 1024.0:F2} GB dummy safetensors file at {path}...");
        
        // Write in large chunks to speed up generation
        int chunkSize = 1024 * 1024 * 10; // 10MB chunks
        byte[] chunk = new byte[chunkSize];
        new Random(42).NextBytes(chunk);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan);
        
        long written = 0;
        while (written < sizeInBytes)
        {
            int toWrite = (int)Math.Min(chunkSize, sizeInBytes - written);
            fs.Write(chunk, 0, toWrite);
            written += toWrite;
        }

        Console.WriteLine("[Synthetic] Dummy file generation complete.");
    }
}
