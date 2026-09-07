using System;

namespace FractalStreamer;

/// <summary>
/// Exception thrown when a SafeTensors file header is malformed, missing required keys, or contains misaligned offsets.
/// </summary>
public class SafeTensorsParseException : Exception
{
    public SafeTensorsParseException(string message) : base(message)
    {
    }

    public SafeTensorsParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
