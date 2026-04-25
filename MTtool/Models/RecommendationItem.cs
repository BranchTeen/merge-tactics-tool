using System.Collections.Generic;
using System.Windows.Media;

namespace MTtool.Models;

public class RecommendationItem
{
    public List<CardDisplay> Cards { get; set; } = new();
    public List<ActivatedTraitDisplay> ActivatedTraits { get; set; } = new();
    public double Score { get; set; }
    public string ScoreDisplay => $"评分: {Score:F2}";
}

public class CardDisplay
{
    public string Name { get; set; }
    public ImageSource ImageSource { get; set; }
    public bool IsSelected { get; set; }
    public Brush BorderBrush => IsSelected ? Brushes.Green : Brushes.Orange;
}

public class ActivatedTraitDisplay
{
    public string Name { get; set; }
    public int CurrentCount { get; set; }
    public int MaxCount { get; set; }
    public string DisplayText => $"{Name} {CurrentCount}/{MaxCount}";
    public ImageSource TraitImage { get; set; }
}