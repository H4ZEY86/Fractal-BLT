using System;
using System.IO;
using System.Text;
using FractalStreamer;
using Xunit;

namespace FractalStreamer.Tests;

public class SafetensorsHeaderParserTests : IDisposable
{
    private readonly string _tempFilePath;

    public SafetensorsHeaderParserTests()
    {
        _tempFilePath = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Fact]
    public void TryGetTensorOffsets_ValidHeader_ReturnsOffsets()
    {
        string json = "{ \"tensor1\": { \"data_offsets\": [ 100, 200 ] } }";
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        long headerLength = jsonBytes.Length;
        byte[] lengthBytes = BitConverter.GetBytes(headerLength);
        
        using (var fs = File.OpenWrite(_tempFilePath))
        {
            fs.Write(lengthBytes);
            fs.Write(jsonBytes);
        }

        bool result = SafetensorsHeaderParser.TryGetTensorOffsets(_tempFilePath, "tensor1", out long start, out long length);
        Assert.True(result);
        Assert.Equal(100, start);
        Assert.Equal(100, length);
    }

    [Fact]
    public void TryGetTensorOffsets_MalformedJson_ThrowsException()
    {
        string json = "{ \"tensor1\": { \"data_offsets\": [ 100, "; // Structurally malformed
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        long headerLength = jsonBytes.Length;
        byte[] lengthBytes = BitConverter.GetBytes(headerLength);
        
        using (var fs = File.OpenWrite(_tempFilePath))
        {
            fs.Write(lengthBytes);
            fs.Write(jsonBytes);
        }

        Assert.Throws<SafeTensorsParseException>(() =>
        {
            SafetensorsHeaderParser.TryGetTensorOffsets(_tempFilePath, "tensor1", out long start, out long end);
        });
    }
}
