using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MTtool.Models;

namespace MTtool.Services;

public static class DataLoader
{
    public static List<Card> LoadCards(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<Card>>(json) ?? new List<Card>();
    }

    public static TraitsRoot LoadTraits(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<TraitsRoot>(json) ?? new TraitsRoot();
    }

    public static (List<string> rows, List<string> cols, Dictionary<(string, string), Card> map)
        BuildTableData(List<Card> cards)
    {
        var rows = cards.Select(c => c.Trait1).Distinct().OrderBy(x => x).ToList();
        var cols = cards.Select(c => c.Trait2).Distinct().OrderBy(x => x).ToList();
        var map = cards.ToDictionary(c => (c.Trait1, c.Trait2), c => c);
        return (rows, cols, map);
    }
}