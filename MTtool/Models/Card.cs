using System.Text.Json.Serialization;

namespace MTtool.Models;

public class Card
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("trait1")]
    public string Trait1 { get; set; } = string.Empty;

    [JsonPropertyName("trait2")]
    public string Trait2 { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("imagePath")]
    public string ImagePath { get; set; } = string.Empty;
}
