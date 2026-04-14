using System;
using System.Globalization;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using VoiceInput.Core.History;

namespace VoiceInput.App.Settings;

/// <summary>
/// History tab content: DataGrid with transcription history + summary stats.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HistoryView : UserControl
{
    private const string PanelBackground = "#1C1C2A";
    private const string CardBackground = "#303046";
    private const string CellBackground = "#222236";
    private const string TextPrimary = "#F0F0F4";
    private const string TextDim = "#8E8EA0";
    private const string AccentColor = "#63E6BE";
    private const string BorderColor = "#3A3A52";
    private const string HeaderBackground = "#2A2A3E";
    private const string RowAlternateBackground = "#252538";

    private static readonly SolidColorBrush PanelBgBrush = new((Color)ColorConverter.ConvertFromString(PanelBackground));
    private static readonly SolidColorBrush AccentBrush = new((Color)ColorConverter.ConvertFromString(AccentColor));
    private static readonly SolidColorBrush TextPrimaryBrush = new((Color)ColorConverter.ConvertFromString(TextPrimary));
    private static readonly SolidColorBrush TextDimBrush = new((Color)ColorConverter.ConvertFromString(TextDim));
    private static readonly SolidColorBrush BorderBrushDark = new((Color)ColorConverter.ConvertFromString(BorderColor));
    private static readonly SolidColorBrush HeaderBgBrush = new((Color)ColorConverter.ConvertFromString(HeaderBackground));
    private static readonly SolidColorBrush CellBgBrush = new((Color)ColorConverter.ConvertFromString(CellBackground));
    private static readonly SolidColorBrush AltRowBgBrush = new((Color)ColorConverter.ConvertFromString(RowAlternateBackground));
    private static readonly SolidColorBrush HoverRowBrush = new(Color.FromArgb(0x30, 0x63, 0xE6, 0xBE));
    private static readonly SolidColorBrush CardBgBrush = new(Color.FromArgb(0x90, 0x30, 0x30, 0x46));

    private readonly IHistoryStore _historyStore;
    private readonly DataGrid _grid;
    private readonly TextBlock _summaryText;

    public HistoryView(IHistoryStore historyStore)
    {
        _historyStore = historyStore;

        var panel = new DockPanel
        {
            Background = PanelBgBrush,
            Margin = new Thickness(0)
        };

        // Summary panel at top
        _summaryText = new TextBlock
        {
            Text = "Loading...",
            Foreground = TextDimBrush,
            FontSize = 13,
            Margin = new Thickness(12, 10, 12, 6)
        };
        DockPanel.SetDock(_summaryText, Dock.Top);
        panel.Children.Add(_summaryText);

        // Clear button — dark themed
        var clearButton = CreateClearButton();
        DockPanel.SetDock(clearButton, Dock.Bottom);
        panel.Children.Add(clearButton);

        // DataGrid — fully dark themed
        _grid = CreateThemedDataGrid();
        panel.Children.Add(_grid);

        Content = panel;

        Loaded += (_, _) => LoadData();
    }

    private Button CreateClearButton()
    {
        var button = new Button
        {
            Content = "Clear History",
            Margin = new Thickness(12, 6, 12, 10),
            Padding = new Thickness(16, 6, 16, 6),
            Background = CardBgBrush,
            Foreground = TextPrimaryBrush,
            BorderBrush = BorderBrushDark,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            FontSize = 13
        };

        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, CardBgBrush));
        style.Setters.Add(new Setter(Control.ForegroundProperty, TextPrimaryBrush));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, BorderBrushDark));

        var hoverTrigger = new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, HoverRowBrush));
        hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, AccentBrush));
        hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, AccentBrush));
        style.Triggers.Add(hoverTrigger);
        button.Style = style;

#pragma warning disable VSTHRD001
        button.Click += (_, _) =>
        {
            var result = MessageBox.Show("Clear all history?", "Confirm", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    await _historyStore.ClearAsync().ConfigureAwait(false);
                    _ = Dispatcher.BeginInvoke(LoadData);
                });
            }
        };
#pragma warning restore VSTHRD001
        return button;
    }

    private DataGrid CreateThemedDataGrid()
    {
        var grid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            Background = CellBgBrush,
            Foreground = TextPrimaryBrush,
            RowBackground = CellBgBrush,
            AlternatingRowBackground = AltRowBgBrush,
            BorderBrush = BorderBrushDark,
            BorderThickness = new Thickness(0, 1, 0, 0),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = BorderBrushDark,
            VerticalGridLinesBrush = BorderBrushDark,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            CanUserSortColumns = true,
            Margin = new Thickness(12, 0, 12, 0)
        };

        // Column header style
        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, HeaderBgBrush));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, TextPrimaryBrush));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BorderBrushDark));
        headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));

        var headerHoverTrigger = new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true
        };
        headerHoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, HoverRowBrush));
        headerStyle.Triggers.Add(headerHoverTrigger);
        grid.ColumnHeaderStyle = headerStyle;

        // Row style with hover
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(Control.ForegroundProperty, TextPrimaryBrush));

        var rowHoverTrigger = new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true
        };
        rowHoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, HoverRowBrush));
        rowStyle.Triggers.Add(rowHoverTrigger);

        var rowSelectedTrigger = new Trigger
        {
            Property = DataGridRow.IsSelectedProperty,
            Value = true
        };
        rowSelectedTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, HoverRowBrush));
        rowStyle.Triggers.Add(rowSelectedTrigger);
        grid.RowStyle = rowStyle;

        // Cell style
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.ForegroundProperty, TextPrimaryBrush));
        cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));

        var cellSelectedTrigger = new Trigger
        {
            Property = DataGridCell.IsSelectedProperty,
            Value = true
        };
        cellSelectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, HoverRowBrush));
        cellSelectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, TextPrimaryBrush));
        cellStyle.Triggers.Add(cellSelectedTrigger);
        grid.CellStyle = cellStyle;

        grid.Columns.Add(new DataGridTextColumn { Header = "Date/Time", Binding = new Binding("CreatedAt") { StringFormat = "dd.MM.yyyy HH:mm" }, Width = 120 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Duration", Binding = new Binding("DurationMs") { Converter = new MsDurationConverter() }, Width = 70 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Chars", Binding = new Binding("CharCount"), Width = 50 });
        grid.Columns.Add(new DataGridTextColumn { Header = "App", Binding = new Binding("TargetApp"), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Text", Binding = new Binding("Text"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Provider", Binding = new Binding("SttProvider"), Width = 80 });

        return grid;
    }

    private Span BuildSummaryInlines(HistoryStats stats)
    {
        var span = new Span();

        span.Inlines.Add(new Run("Total: ") { Foreground = TextDimBrush });
        span.Inlines.Add(new Run($"{stats.TotalEntries}") { Foreground = AccentBrush, FontWeight = FontWeights.SemiBold });
        span.Inlines.Add(new Run(" entries  |  ") { Foreground = TextDimBrush });

        span.Inlines.Add(new Run("Duration: ") { Foreground = TextDimBrush });
        span.Inlines.Add(new Run($"{stats.TotalDurationMs / 1000.0:F0}s") { Foreground = AccentBrush, FontWeight = FontWeights.SemiBold });
        span.Inlines.Add(new Run("  |  ") { Foreground = TextDimBrush });

        span.Inlines.Add(new Run("Characters: ") { Foreground = TextDimBrush });
        span.Inlines.Add(new Run($"{stats.TotalCharCount}") { Foreground = AccentBrush, FontWeight = FontWeights.SemiBold });
        span.Inlines.Add(new Run("  |  ") { Foreground = TextDimBrush });

        span.Inlines.Add(new Run("Avg latency: ") { Foreground = TextDimBrush });
        span.Inlines.Add(new Run($"{stats.AverageLatencyMs:F0}ms") { Foreground = AccentBrush, FontWeight = FontWeights.SemiBold });

        return span;
    }

#pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs
    private void LoadData()
    {
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var entries = await _historyStore.GetEntriesAsync(limit: 200).ConfigureAwait(false);
                var stats = await _historyStore.GetStatsAsync().ConfigureAwait(false);

                _ = Dispatcher.BeginInvoke(() =>
                {
                    _grid.ItemsSource = entries;
                    _summaryText.Inlines.Clear();
                    _summaryText.Inlines.Add(BuildSummaryInlines(stats));
                });
            }
            catch
            {
                _ = Dispatcher.BeginInvoke(() => _summaryText.Text = "Failed to load history");
            }
        });
    }
#pragma warning restore VSTHRD001

    /// <summary>Converts milliseconds to human-readable duration.</summary>
    private sealed class MsDurationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int ms)
            {
                return ms < 1000 ? $"{ms}ms" : $"{ms / 1000.0:F1}s";
            }
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
