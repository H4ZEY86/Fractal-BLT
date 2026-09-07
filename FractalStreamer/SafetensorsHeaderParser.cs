using System;
using System.IO;
using System.Text.Json;

namespace FractalStreamer;

/// <summary>
/// A zero-allocation JSON parser to extract tensor byte offsets from a .safetensors file header.
/// </summary>
public static class SafetensorsHeaderParser
{
    /// <summary>
    /// Parses the safetensors header to find the byte offset and length for a specific tensor name.
    /// This avoids allocating objects or strings, using Utf8JsonReader on a stack or unmanaged buffer.
    /// </summary>
    public static bool TryGetTensorOffsets(string filePath, string tensorName, out long offset, out long length)
    {
        offset = 0;
        length = 0;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        
        // Safetensors format: First 8 bytes are an unsigned 64-bit integer (little endian) specifying the header size.
        Span<byte> lengthBuffer = stackalloc byte[8];
        if (fs.Read(lengthBuffer) != 8)
            return false;

        long headerLength = BitConverter.ToInt64(lengthBuffer);
        if (headerLength <= 0 || headerLength > 100 * 1024 * 1024) // Sanity check (max 100MB header)
            return false;

        // Allocate a buffer for the header.
        byte[] headerBytes = new byte[headerLength];
        if (fs.Read(headerBytes) != headerLength)
            return false;

        // Parse JSON using Utf8JsonReader
        var reader = new Utf8JsonReader(headerBytes);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals(tensorName))
                {
                    // Found the tensor, the value should be an object containing "data_offsets"
                    reader.Read(); // move to StartObject
                    if (reader.TokenType != JsonTokenType.StartObject)
                        continue;

                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("data_offsets"))
                        {
                            // "data_offsets": [START, END]
                            reader.Read(); // move to StartArray
                            reader.Read(); // move to START number
                            long startOffset = reader.GetInt64();
                            reader.Read(); // move to END number
                            long endOffset = reader.GetInt64();

                            offset = startOffset;
                            length = endOffset - startOffset;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
