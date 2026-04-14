using System;
using System.Globalization;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using VoiceInput.Core.History;

namespace VoiceInput.App.Settings;

/// <summary>
/// History tab content: DataGrid with transcription history + summary stats.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HistoryView : UserControl
{
    private readonly IHistoryStore _historyStore;
    private readonly DataGrid _grid;
    private readonly TextBlock _summaryText;

    public HistoryView(IHistoryStore historyStore)
    {
        _historyStore = historyStore;

        var panel = new DockPanel();

        // Summary panel at top
        _summaryText = new TextBlock
        {
            Text = "Loading...",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
            FontSize = 13,
            Margin = new Thickness(8)
        };
        DockPanel.SetDock(_summaryText, Dock.Top);
        panel.Children.Add(_summaryText);

        // Clear button
        var clearButton = new Button
        {
            Content = "Clear History",
            Margin = new Thickness(8, 0, 8, 8),
            Padding = new Thickness(12, 4, 12, 4)
        };
#pragma warning disable VSTHRD001
        clearButton.Click += (_, _) =>
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
        DockPanel.SetDock(clearButton, Dock.Bottom);
        panel.Children.Add(clearButton);

        // DataGrid
        _grid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x33)),
            Foreground = Brushes.White,
            RowBackground = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x33)),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x3C)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x44)),
            HeadersVisibility = DataGridHeadersVisibility.Column,
            CanUserSortColumns = true
        };

        _grid.Columns.Add(new DataGridTextColumn { Header = "Date/Time", Binding = new Binding("CreatedAt") { StringFormat = "dd.MM.yyyy HH:mm" }, Width = 120 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Duration", Binding = new Binding("DurationMs") { Converter = new MsDurationConverter() }, Width = 70 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Chars", Binding = new Binding("CharCount"), Width = 50 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "App", Binding = new Binding("TargetApp"), Width = 100 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Text", Binding = new Binding("Text"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Provider", Binding = new Binding("SttProvider"), Width = 80 });

        panel.Children.Add(_grid);
        Content = panel;

        Loaded += (_, _) => LoadData();
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
                    _summaryText.Text = $"Total: {stats.TotalEntries} entries | Duration: {stats.TotalDurationMs / 1000.0:F0}s | Characters: {stats.TotalCharCount} | Avg latency: {stats.AverageLatencyMs:F0}ms";
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
