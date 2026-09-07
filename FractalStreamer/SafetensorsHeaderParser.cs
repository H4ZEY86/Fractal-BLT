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
            throw new SafeTensorsParseException("File is too small to contain a SafeTensors length prefix.");

        long headerLength = BitConverter.ToInt64(lengthBuffer);
        if (headerLength <= 0 || headerLength > fs.Length - 8) 
            throw new SafeTensorsParseException($"Invalid SafeTensors header length: {headerLength}. File may be corrupted or truncated.");

        // Allocate a buffer for the header.
        byte[] headerBytes = new byte[headerLength];
        if (fs.Read(headerBytes) != headerLength)
            throw new SafeTensorsParseException("Failed to read the entire SafeTensors JSON header.");

        try
        {
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

                                offset = 8 + headerLength + startOffset;
                                length = endOffset - startOffset;
                                return true;
                            }
                        }
                        throw new SafeTensorsParseException($"Expected 'data_offsets' array for tensor '{tensorName}'.");
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
        {
            throw new SafeTensorsParseException("Failed to parse SafeTensors JSON header.", ex);
        }

        throw new SafeTensorsParseException($"Tensor '{tensorName}' not found in the safetensors header.");
    }

    /// <summary>
    /// Parses the safetensors header to retrieve all tensor names.
    /// </summary>
    public static System.Collections.Generic.List<string> GetAllTensorNames(string filePath)
    {
        var tensorNames = new System.Collections.Generic.List<string>();

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        
        Span<byte> lengthBuffer = stackalloc byte[8];
        if (fs.Read(lengthBuffer) != 8)
            throw new SafeTensorsParseException("File is too small to contain a SafeTensors length prefix.");

        long headerLength = BitConverter.ToInt64(lengthBuffer);
        if (headerLength <= 0 || headerLength > fs.Length - 8) 
            throw new SafeTensorsParseException($"Invalid SafeTensors header length: {headerLength}. File may be corrupted or truncated.");

        byte[] headerBytes = new byte[headerLength];
        if (fs.Read(headerBytes) != headerLength)
            throw new SafeTensorsParseException("Failed to read the entire SafeTensors JSON header.");

        try
        {
            var reader = new Utf8JsonReader(headerBytes);
            // Safetensors header is a flat JSON object where keys are tensor names 
            // (except for a special "__metadata__" key)
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                throw new SafeTensorsParseException("Expected a JSON object at the root of the header.");

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propName = reader.GetString();
                    reader.Read(); // move to value
                    
                    if (propName != "__metadata__" && reader.TokenType == JsonTokenType.StartObject)
                    {
                        tensorNames.Add(propName);
                    }
                    
                    reader.Skip(); // skip the value (object or string)
                }
            }
        }
        catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException)
        {
            throw new SafeTensorsParseException("Failed to parse SafeTensors JSON header.", ex);
        }

        return tensorNames;
    }
}
