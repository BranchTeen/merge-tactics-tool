using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MTtool.Models;
using MTtool.Services;
using MTtool.ViewModels;

namespace MTtool;

public partial class MainWindow : Window
{
    private List<Card> _allCards = null!;
    private List<string> _rowHeaders = null!;
    private List<string> _colHeaders = null!;
    private Dictionary<(string, string), Card> _cardMap = null!;
    private Dictionary<Button, Card> _buttonToCard = null!;
    private MainViewModel _viewModel = null!;
    private Dictionary<string, Trait> _trait1Info = null!;
    private Dictionary<string, Trait> _trait2Info = null!;

    public MainWindow()
    {
        InitializeComponent();
        LoadData();
        _viewModel = new MainViewModel(_allCards, _trait1Info, _trait2Info);
        DataContext = _viewModel;
        _buttonToCard = new Dictionary<Button, Card>();
        BuildTable();
        UpdateSelectedCountDisplay();

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.SelectedCount))
                UpdateSelectedCountDisplay();
        };
    }

    private void LoadData()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string cardsPath = Path.Combine(baseDir, "Data", "cards.json");
        string traitsPath = Path.Combine(baseDir, "Data", "traits.json");

        _allCards = DataLoader.LoadCards(cardsPath);
        var traits = DataLoader.LoadTraits(traitsPath);
        _trait1Info = traits.Trait1List.ToDictionary(t => t.Name, t => t);
        _trait2Info = traits.Trait2List.ToDictionary(t => t.Name, t => t);
        (_rowHeaders, _colHeaders, _cardMap) = DataLoader.BuildTableData(_allCards);
    }

    private void BuildTable()
    {
        int rowCount = _rowHeaders.Count + 1;
        int colCount = _colHeaders.Count + 1;

        TableGrid.RowDefinitions.Clear();
        TableGrid.ColumnDefinitions.Clear();
        TableGrid.Children.Clear();

        for (int i = 0; i < rowCount; i++)
            TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int j = 0; j < colCount; j++)
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cornerBorder = new Border { Width = 40, Height = 40 };
        Grid.SetRow(cornerBorder, 0);
        Grid.SetColumn(cornerBorder, 0);
        TableGrid.Children.Add(cornerBorder);

        for (int j = 0; j < _colHeaders.Count; j++)
            AddTraitHeader(_colHeaders[j], 0, j + 1, _trait2Info);

        for (int i = 0; i < _rowHeaders.Count; i++)
            AddTraitHeader(_rowHeaders[i], i + 1, 0, _trait1Info);

        for (int i = 0; i < _rowHeaders.Count; i++)
        {
            for (int j = 0; j < _colHeaders.Count; j++)
            {
                string race = _rowHeaders[i];
                string profession = _colHeaders[j];
                var key = (race, profession);
                Card? card = _cardMap.GetValueOrDefault(key);

                Button btn = new Button
                {
                    Margin = new Thickness(2),
                    Padding = new Thickness(2),
                    Width = 80,
                    Height = 80,
                    Tag = card
                };
                btn.Style = CreateButtonStyle();

                if (card != null)
                {
                    var img = new Image { Width = 70, Height = 70, Stretch = Stretch.Uniform, Margin = new Thickness(2) };
                    try
                    {
                        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, card.ImagePath);
                        if (File.Exists(fullPath))
                            img.Source = new BitmapImage(new Uri(fullPath));
                    }
                    catch { }
                    btn.Content = img;

                    btn.Click += CardButton_LeftClick;
                    btn.PreviewMouseRightButtonDown += CardButton_RightClick;
                    _buttonToCard[btn] = card;
                    UpdateButtonStyle(btn, _viewModel.GetCardState(card));
                }
                else
                {
                    btn.Content = null;
                    btn.IsEnabled = false;
                    btn.Background = Brushes.Transparent;
                    btn.BorderBrush = Brushes.LightGray;
                    btn.BorderThickness = new Thickness(1);
                }

                Grid.SetRow(btn, i + 1);
                Grid.SetColumn(btn, j + 1);
                TableGrid.Children.Add(btn);
            }
        }
    }

    private void AddTraitHeader(string traitName, int row, int col, Dictionary<string, Trait> traitInfo)
    {
        if (traitInfo.TryGetValue(traitName, out var trait))
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, trait.ImagePath);
            if (File.Exists(fullPath))
            {
                var img = new Image
                {
                    Source = new BitmapImage(new Uri(fullPath)),
                    Width = 50,
                    Height = 50,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(5),
                    ToolTip = trait.Text
                };
                Grid.SetRow(img, row);
                Grid.SetColumn(img, col);
                TableGrid.Children.Add(img);
                return;
            }
        }
        var textBlock = new TextBlock
        {
            Text = traitName,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(5),
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(textBlock, row);
        Grid.SetColumn(textBlock, col);
        TableGrid.Children.Add(textBlock);
    }

    private Style CreateButtonStyle()
    {
        Style style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.White));
        style.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Green));
        style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(2)));
        Trigger trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Button.BackgroundProperty, null));
        trigger.Setters.Add(new Setter(Button.BorderBrushProperty, null));
        style.Triggers.Add(trigger);
        style.Setters.Add(new Setter(Button.FocusVisualStyleProperty, null));
        return style;
    }

    private void CardButton_LeftClick(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn?.Tag is not Card card) return;

        var current = _viewModel.GetCardState(card);
        MainViewModel.CardState newState = current switch
        {
            MainViewModel.CardState.Optional => MainViewModel.CardState.Selected,
            MainViewModel.CardState.Selected => MainViewModel.CardState.Optional,
            MainViewModel.CardState.Ignored => MainViewModel.CardState.Selected,
            _ => current
        };
        var (success, _) = _viewModel.TryChangeState(card, newState);
        if (success)
            UpdateButtonStyle(btn, newState);
    }

    private void CardButton_RightClick(object sender, MouseButtonEventArgs e)
    {
        var btn = sender as Button;
        if (btn?.Tag is not Card card) return;
        e.Handled = true;

        var current = _viewModel.GetCardState(card);
        MainViewModel.CardState newState = current == MainViewModel.CardState.Ignored
            ? MainViewModel.CardState.Optional
            : MainViewModel.CardState.Ignored;

        var (success, _) = _viewModel.TryChangeState(card, newState);
        if (success)
            UpdateButtonStyle(btn, newState);
    }

    private void UpdateButtonStyle(Button btn, MainViewModel.CardState state)
    {
        switch (state)
        {
            case MainViewModel.CardState.Optional:
                btn.Background = Brushes.White;
                btn.BorderBrush = Brushes.Green;
                btn.BorderThickness = new Thickness(2);
                btn.Opacity = 1.0;
                break;
            case MainViewModel.CardState.Selected:
                btn.Background = Brushes.Gold;
                btn.BorderBrush = Brushes.Orange;
                btn.BorderThickness = new Thickness(3);
                btn.Opacity = 1.0;
                break;
            case MainViewModel.CardState.Ignored:
                btn.Background = Brushes.Gray;
                btn.BorderBrush = Brushes.DarkGray;
                btn.BorderThickness = new Thickness(1);
                btn.Opacity = 0.6;
                break;
        }
    }

    private void UpdateSelectedCountDisplay()
    {
        SelectedCountText.Text = $"{_viewModel.SelectedCount}/6";
    }

    private void ResetAllButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetAll();
        RefreshAllButtons();
    }

    private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearSelected();
        RefreshAllButtons();
    }

    private void RefreshAllButtons()
    {
        foreach (var kv in _buttonToCard)
            UpdateButtonStyle(kv.Key, _viewModel.GetCardState(kv.Value));
    }
}