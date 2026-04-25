using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MTtool.Models;

public class TraitsRoot
{
    [JsonPropertyName("trait1")]
    public List<Trait> Trait1List { get; set; } = new();

    [JsonPropertyName("trait2")]
    public List<Trait> Trait2List { get; set; } = new();
}