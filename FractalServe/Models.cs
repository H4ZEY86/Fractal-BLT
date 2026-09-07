using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FractalServe;

// Strictly typed structs for zero-allocation JSON parsing via source generators

public struct ChatCompletionMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}

public struct ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("messages")]
    public List<ChatCompletionMessage> Messages { get; set; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }
}

// Minimal JSON context to trigger NativeAOT source generation
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatCompletionMessage))]
public partial class FractalJsonContext : JsonSerializerContext
{
}
