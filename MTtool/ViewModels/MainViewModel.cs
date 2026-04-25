#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MTtool.Models;

namespace MTtool.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly List<Card> _allCards;
    private readonly Dictionary<Card, CardState> _cardState;
    private readonly Dictionary<string, Trait> _trait1Info;
    private readonly Dictionary<string, Trait> _trait2Info;
    private const int MaxSelection = 6;
    private CancellationTokenSource _cts = new();  // 不再使用 readonly

    public enum CardState { Optional, Selected, Ignored }

    // 额外羁绊存储
    private readonly Dictionary<string, int> _extraTrait1 = new();
    private readonly Dictionary<string, int> _extraTrait2 = new();

    public MainViewModel(List<Card> allCards,
                         Dictionary<string, Trait> trait1Info,
                         Dictionary<string, Trait> trait2Info)
    {
        _allCards = allCards;
        _trait1Info = trait1Info;
        _trait2Info = trait2Info;
        _cardState = allCards.ToDictionary(c => c, _ => CardState.Optional);
        ActiveTraits = new ObservableCollection<ActiveTraitItem>();
        Recommendations = new ObservableCollection<RecommendationItem>();
        ExtraTraitsIcons = new ObservableCollection<ExtraTraitIconItem>();

        foreach (var t in trait1Info.Keys) _extraTrait1[t] = 0;
        foreach (var t in trait2Info.Keys) _extraTrait2[t] = 0;

        IncrementExtraTraitCommand = new RelayCommand(param => IncrementExtraTrait((ExtraTraitViewModel)param));
        ResetExtraTraitCommand = new RelayCommand(param => ResetExtraTrait((ExtraTraitViewModel)param));

        UpdateExtraTraitsList();
        UpdateActiveTraits();

        // 启动实时推荐（初始计算）
        _ = UpdateRecommendationsAsync();
    }

    // ------------------- 额外羁绊 UI 绑定 -------------------
    public ObservableCollection<ExtraTraitViewModel> ExtraTraits1List { get; } = new();
    public ObservableCollection<ExtraTraitViewModel> ExtraTraits2List { get; } = new();
    public ObservableCollection<ExtraTraitIconItem> ExtraTraitsIcons { get; set; }
    public ICommand IncrementExtraTraitCommand { get; }
    public ICommand ResetExtraTraitCommand { get; }

    public string ExtraTraitsHeader => $"额外羁绊 (总计{GetTotalExtraCount()})";
    private int GetTotalExtraCount() => _extraTrait1.Values.Sum() + _extraTrait2.Values.Sum();

    private void UpdateExtraTraitsList()
    {
        ExtraTraits1List.Clear();
        foreach (var kv in _trait1Info)
        {
            var vm = new ExtraTraitViewModel
            {
                Name = kv.Key,
                Type = "trait1",
                Image = GetTraitImage(kv.Key, _trait1Info),
                Count = _extraTrait1[kv.Key]
            };
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(ExtraTraitViewModel.Count)) { UpdateActiveTraits(); _ = UpdateRecommendationsAsync(); } };
            ExtraTraits1List.Add(vm);
        }

        ExtraTraits2List.Clear();
        foreach (var kv in _trait2Info)
        {
            var vm = new ExtraTraitViewModel
            {
                Name = kv.Key,
                Type = "trait2",
                Image = GetTraitImage(kv.Key, _trait2Info),
                Count = _extraTrait2[kv.Key]
            };
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(ExtraTraitViewModel.Count)) { UpdateActiveTraits(); _ = UpdateRecommendationsAsync(); } };
            ExtraTraits2List.Add(vm);
        }

        RefreshExtraTraitsIcons();
        OnPropertyChanged(nameof(ExtraTraitsHeader));
    }

    private void RefreshExtraTraitsIcons()
    {
        var list = new List<ExtraTraitIconItem>();
        foreach (var kv in _extraTrait1.Where(kv => kv.Value > 0))
        {
            var img = GetTraitImage(kv.Key, _trait1Info);
            list.Add(new ExtraTraitIconItem { Image = img, Count = kv.Value });
        }
        foreach (var kv in _extraTrait2.Where(kv => kv.Value > 0))
        {
            var img = GetTraitImage(kv.Key, _trait2Info);
            list.Add(new ExtraTraitIconItem { Image = img, Count = kv.Value });
        }
        ExtraTraitsIcons = new ObservableCollection<ExtraTraitIconItem>(list);
        OnPropertyChanged(nameof(ExtraTraitsIcons));
    }

    private void IncrementExtraTrait(ExtraTraitViewModel vm)
    {
        if (vm.Type == "trait1")
        {
            int newVal = (vm.Count + 1) % 3;
            vm.Count = newVal;
            _extraTrait1[vm.Name] = newVal;
        }
        else
        {
            int newVal = (vm.Count + 1) % 3;
            vm.Count = newVal;
            _extraTrait2[vm.Name] = newVal;
        }
        UpdateActiveTraits();
        RefreshExtraTraitsIcons();
        OnPropertyChanged(nameof(ExtraTraitsHeader));
        _ = UpdateRecommendationsAsync();
    }

    private void ResetExtraTrait(ExtraTraitViewModel vm)
    {
        if (vm.Type == "trait1")
        {
            vm.Count = 0;
            _extraTrait1[vm.Name] = 0;
        }
        else
        {
            vm.Count = 0;
            _extraTrait2[vm.Name] = 0;
        }
        UpdateActiveTraits();
        RefreshExtraTraitsIcons();
        OnPropertyChanged(nameof(ExtraTraitsHeader));
        _ = UpdateRecommendationsAsync();
    }

    // ------------------- 已激活羁绊显示 -------------------
    private ObservableCollection<ActiveTraitItem> _activeTraits;
    public ObservableCollection<ActiveTraitItem> ActiveTraits
    {
        get => _activeTraits;
        set { _activeTraits = value; OnPropertyChanged(); }
    }

    private void UpdateActiveTraits()
    {
        var selectedCards = _allCards.Where(c => _cardState[c] == CardState.Selected).ToList();
        var t1Cnt = selectedCards.GroupBy(c => c.Trait1).ToDictionary(g => g.Key, g => g.Count());
        var t2Cnt = selectedCards.GroupBy(c => c.Trait2).ToDictionary(g => g.Key, g => g.Count());

        foreach (var kv in _extraTrait1)
            if (kv.Value > 0)
                t1Cnt[kv.Key] = t1Cnt.GetValueOrDefault(kv.Key) + kv.Value;
        foreach (var kv in _extraTrait2)
            if (kv.Value > 0)
                t2Cnt[kv.Key] = t2Cnt.GetValueOrDefault(kv.Key) + kv.Value;

        var activeItems = new List<ActiveTraitItem>();
        foreach (var kv in t1Cnt.Where(kv => kv.Value >= 2))
        {
            int maxCount = _trait1Info.TryGetValue(kv.Key, out var t) ? t.MaxCount : 6;
            var img = GetTraitImage(kv.Key, _trait1Info);
            activeItems.Add(new ActiveTraitItem { TraitImage = img, DisplayText = $"{kv.Key}：{kv.Value}/{maxCount}" });
        }
        foreach (var kv in t2Cnt.Where(kv => kv.Value >= 2))
        {
            int maxCount = _trait2Info.TryGetValue(kv.Key, out var t) ? t.MaxCount : 6;
            var img = GetTraitImage(kv.Key, _trait2Info);
            activeItems.Add(new ActiveTraitItem { TraitImage = img, DisplayText = $"{kv.Key}：{kv.Value}/{maxCount}" });
        }

        ActiveTraits = new ObservableCollection<ActiveTraitItem>(activeItems);
        OnPropertyChanged(nameof(ActiveTraits));
        OnPropertyChanged(nameof(ExtraTraitsHeader));
    }

    // ------------------- 卡牌状态管理 -------------------
    private int _selectedCount;
    public int SelectedCount
    {
        get => _selectedCount;
        set { _selectedCount = value; OnPropertyChanged(); }
    }

    public CardState GetCardState(Card card) => _cardState[card];
    public void SetCardState(Card card, CardState state) => _cardState[card] = state;

    public (bool success, int delta) TryChangeState(Card card, CardState newState)
    {
        var oldState = _cardState[card];
        if (oldState == newState) return (false, 0);

        if (newState == CardState.Selected && oldState != CardState.Selected && SelectedCount >= MaxSelection)
            return (false, 0);

        _cardState[card] = newState;

        int delta = 0;
        if (oldState == CardState.Selected && newState != CardState.Selected) delta = -1;
        else if (oldState != CardState.Selected && newState == CardState.Selected) delta = +1;

        if (delta != 0)
            SelectedCount += delta;

        UpdateActiveTraits();
        _ = UpdateRecommendationsAsync();  // 触发实时推荐
        return (true, delta);
    }

    public void ResetAll()
    {
        foreach (var card in _allCards)
            _cardState[card] = CardState.Optional;
        SelectedCount = 0;
        UpdateActiveTraits();
        _ = UpdateRecommendationsAsync();
    }

    public void ClearSelected()
    {
        foreach (var card in _allCards.Where(c => _cardState[c] == CardState.Selected))
            _cardState[card] = CardState.Optional;
        SelectedCount = 0;
        UpdateActiveTraits();
        _ = UpdateRecommendationsAsync();
    }

    // ------------------- 实时推荐（异步，支持取消） -------------------
    private ObservableCollection<RecommendationItem> _recommendations = new();
    public ObservableCollection<RecommendationItem> Recommendations
    {
        get => _recommendations;
        set { _recommendations = value; OnPropertyChanged(); }
    }

    private string _calculationTimeText = "";
    public string CalculationTimeText
    {
        get => _calculationTimeText;
        set { _calculationTimeText = value; OnPropertyChanged(); }
    }

    private async Task UpdateRecommendationsAsync()
    {
        // 取消之前的任务
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var sw = Stopwatch.StartNew();

        try
        {
            var selectedCards = _allCards.Where(c => _cardState[c] == CardState.Selected).ToList();
            var availableCards = _allCards.Where(c => _cardState[c] != CardState.Ignored && !selectedCards.Contains(c)).ToList();

            if (selectedCards.Count > MaxSelection) return;
            int need = MaxSelection - selectedCards.Count;
            if (need < 0 || need > availableCards.Count) return;

            // 在后台线程计算
            var result = await Task.Run(() =>
            {
                var scored = new List<(double score, List<Card> lineup, List<ActivatedTrait> traits)>();
                foreach (var combo in Combinations(availableCards, need))
                {
                    if (token.IsCancellationRequested) return null;
                    var lineup = selectedCards.Concat(combo).ToList();
                    var (score, traits) = GetScoreAndTraits(lineup);
                    scored.Add((score, lineup, traits));
                }
                if (token.IsCancellationRequested) return null;
                var top = scored.OrderByDescending(x => x.score).Take(10).ToList();
                return top;
            }, token);

            if (result == null) return;

            // 构造展示数据
            var recItems = new List<RecommendationItem>();
            foreach (var (score, lineup, traits) in result)
            {
                var cardsDisplay = new List<CardDisplay>();
                foreach (var card in lineup)
                {
                    var img = GetCardImage(card.ImagePath);
                    cardsDisplay.Add(new CardDisplay
                    {
                        Name = card.Name,
                        ImageSource = img,
                        IsSelected = selectedCards.Contains(card)
                    });
                }
                var traitDisplays = new List<ActivatedTraitDisplay>();
                foreach (var t in traits)
                {
                    var img = GetTraitImage(t.Name, t.Type == "trait1" ? _trait1Info : _trait2Info);
                    traitDisplays.Add(new ActivatedTraitDisplay
                    {
                        Name = t.Name,
                        CurrentCount = t.Count,
                        MaxCount = t.MaxCount,
                        TraitImage = img
                    });
                }
                recItems.Add(new RecommendationItem
                {
                    Cards = cardsDisplay,
                    ActivatedTraits = traitDisplays,
                    Score = score
                });
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Recommendations.Clear();
                foreach (var item in recItems)
                    Recommendations.Add(item);
                CalculationTimeText = $"推荐计算时间: {sw.ElapsedMilliseconds} ms";
            });
        }
        catch (OperationCanceledException) { }
        finally
        {
            sw.Stop();
        }
    }

    private IEnumerable<List<Card>> Combinations(List<Card> list, int k)
    {
        if (k == 0) return new[] { new List<Card>() };
        if (k > list.Count) return Enumerable.Empty<List<Card>>();
        var result = new List<List<Card>>();
        for (int i = 0; i <= list.Count - k; i++)
        {
            var head = list[i];
            var tails = Combinations(list.Skip(i + 1).ToList(), k - 1);
            foreach (var tail in tails)
            {
                var combo = new List<Card> { head };
                combo.AddRange(tail);
                result.Add(combo);
            }
        }
        return result;
    }

    private (double score, List<ActivatedTrait> activatedTraits) GetScoreAndTraits(List<Card> lineup)
    {
        var t1Cnt = new Dictionary<string, int>();
        var t2Cnt = new Dictionary<string, int>();
        foreach (var card in lineup)
        {
            t1Cnt[card.Trait1] = t1Cnt.GetValueOrDefault(card.Trait1) + 1;
            t2Cnt[card.Trait2] = t2Cnt.GetValueOrDefault(card.Trait2) + 1;
        }
        foreach (var kv in _extraTrait1)
            if (kv.Value > 0)
                t1Cnt[kv.Key] = t1Cnt.GetValueOrDefault(kv.Key) + kv.Value;
        foreach (var kv in _extraTrait2)
            if (kv.Value > 0)
                t2Cnt[kv.Key] = t2Cnt.GetValueOrDefault(kv.Key) + kv.Value;

        var activated = new List<ActivatedTrait>();
        foreach (var kv in t1Cnt)
        {
            if (_trait1Info.TryGetValue(kv.Key, out var trait))
            {
                int level = GetActivationLevel(kv.Value, trait.MaxCount);
                if (level > 0)
                {
                    double mp = trait.MultiplexerInt.GetValueOrDefault(level, 1.0);
                    double gmp = trait.GlobalMultiplexerInt.GetValueOrDefault(level, 1.0);
                    activated.Add(new ActivatedTrait { Name = kv.Key, Type = "trait1", Count = kv.Value, MaxCount = trait.MaxCount, Level = level, Multiplexer = mp, GlobalMultiplexer = gmp });
                }
            }
        }
        foreach (var kv in t2Cnt)
        {
            if (_trait2Info.TryGetValue(kv.Key, out var trait))
            {
                int level = GetActivationLevel(kv.Value, trait.MaxCount);
                if (level > 0)
                {
                    double mp = trait.MultiplexerInt.GetValueOrDefault(level, 1.0);
                    double gmp = trait.GlobalMultiplexerInt.GetValueOrDefault(level, 1.0);
                    activated.Add(new ActivatedTrait { Name = kv.Key, Type = "trait2", Count = kv.Value, MaxCount = trait.MaxCount, Level = level, Multiplexer = mp, GlobalMultiplexer = gmp });
                }
            }
        }

        double total = 0.0;
        foreach (var card in lineup)
        {
            double mp1 = 1.0, mp2 = 1.0;
            var a1 = activated.FirstOrDefault(a => a.Type == "trait1" && a.Name == card.Trait1);
            if (a1 != null) mp1 = a1.Multiplexer;
            var a2 = activated.FirstOrDefault(a => a.Type == "trait2" && a.Name == card.Trait2);
            if (a2 != null) mp2 = a2.Multiplexer;
            total += card.Value * mp1 * mp2;
        }
        double global = activated.Aggregate(1.0, (acc, a) => acc * a.GlobalMultiplexer);
        return (total * global, activated);
    }

    private int GetActivationLevel(int count, int maxCount)
    {
        if (count < 2) return 0;
        if (count >= 6 && maxCount >= 6) return 6;
        if (count >= 4 && maxCount >= 4) return 4;
        return 2;
    }

    private ImageSource GetCardImage(string relativePath)
    {
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
        if (File.Exists(fullPath))
            return new BitmapImage(new Uri(fullPath));
        return null;
    }

    private ImageSource GetTraitImage(string traitName, Dictionary<string, Trait> traitInfo)
    {
        if (!traitInfo.TryGetValue(traitName, out var trait)) return null;
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, trait.ImagePath);
        if (!File.Exists(fullPath)) return null;
        return new BitmapImage(new Uri(fullPath));
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// 辅助类
public class ExtraTraitViewModel : INotifyPropertyChanged
{
    public string Name { get; set; }
    public string Type { get; set; }
    public ImageSource Image { get; set; }
    private int _count;
    public int Count
    {
        get => _count;
        set { _count = value; OnPropertyChanged(); }
    }
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ExtraTraitIconItem
{
    public ImageSource Image { get; set; }
    public int Count { get; set; }
}

public class ActivatedTrait
{
    public string Name { get; set; }
    public string Type { get; set; }
    public int Count { get; set; }
    public int MaxCount { get; set; }
    public int Level { get; set; }
    public double Multiplexer { get; set; }
    public double GlobalMultiplexer { get; set; }
}