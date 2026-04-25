using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MTtool.Models;

public class Trait
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("maxCount")]
    public int MaxCount { get; set; }

    [JsonPropertyName("multiplexer")]
    public Dictionary<string, double> Multiplexer { get; set; } = new();

    [JsonPropertyName("globalMultiplexer")]
    public Dictionary<string, double> GlobalMultiplexer { get; set; } = new();

    [JsonPropertyName("imagePath")]
    public string ImagePath { get; set; } = string.Empty;

    public Dictionary<int, double> MultiplexerInt =>
        Multiplexer.ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);

    public Dictionary<int, double> GlobalMultiplexerInt =>
        GlobalMultiplexer.ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
}