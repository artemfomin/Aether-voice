using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using VoiceInput.Core.Config;
using VoiceInput.Core.History;

namespace VoiceInput.App.Settings;

/// <summary>
/// Settings window — Windows 11 Fluent Design with acrylic backdrop,
/// vertical navigation, dark theme, rounded inputs.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SettingsWindow : Window
{
    // ── DWM interop for Mica / Acrylic backdrop ───────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    // ── Palette ──────────────────────────────────────────────────────
    private static readonly Color BgColor = Color.FromArgb(0xE8, 0x1C, 0x1C, 0x2A);
    private static readonly Color SurfaceColor = Color.FromArgb(0xB0, 0x28, 0x28, 0x3C);
    private static readonly Color CardColor = Color.FromArgb(0x90, 0x30, 0x30, 0x46);
    private static readonly Color InputBg = Color.FromArgb(0xFF, 0x22, 0x22, 0x36);
    private static readonly Color InputBorder = Color.FromArgb(0xFF, 0x3A, 0x3A, 0x52);
    private static readonly Color AccentColor = Color.FromRgb(0x63, 0xE6, 0xBE); // teal accent
    private static readonly Color TextPrimary = Color.FromRgb(0xF0, 0xF0, 0xF4);
    private static readonly Color TextSecondary = Color.FromRgb(0x8E, 0x8E, 0xA0);
    private static readonly Color NavHover = Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);
    private static readonly Color NavSelected = Color.FromArgb(0x20, 0x63, 0xE6, 0xBE);

    private static readonly Brush BgBrush = new SolidColorBrush(BgColor);
    private static readonly Brush SurfaceBrush = new SolidColorBrush(SurfaceColor);
    private static readonly Brush CardBrush = new SolidColorBrush(CardColor);
    private static readonly Brush InputBgBrush = new SolidColorBrush(InputBg);
    private static readonly Brush InputBorderBrush = new SolidColorBrush(InputBorder);
    private static readonly Brush AccentBrush = new SolidColorBrush(AccentColor);
    private static readonly Brush TextBrush = new SolidColorBrush(TextPrimary);
    private static readonly Brush TextDimBrush = new SolidColorBrush(TextSecondary);
    private static readonly Brush TransparentBrush = Brushes.Transparent;

    private readonly AppConfig _config;
    private readonly Action<AppConfig> _onSave;
    private readonly ContentControl _contentArea;
    private ListBoxItem? _selectedNavItem;

    public SettingsWindow(AppConfig config, Action<AppConfig> onSave, IHistoryStore? historyStore = null)
    {
        _config = config;
        _onSave = onSave;

        Title = "Aether Voice";
        Width = 720;
        Height = 520;
        MinWidth = 600;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.SingleBorderWindow;
        Background = BgBrush;

        // ── Layout: Nav sidebar | Content ────────────────────────────
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ── Nav sidebar ──────────────────────────────────────────────
        var navPanel = new Border
        {
            Background = SurfaceBrush,
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(0, 12, 0, 12),
        };

        var navStack = new StackPanel();

        // App title in sidebar
        var titleBlock = new TextBlock
        {
            Text = "Aether Voice",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            Margin = new Thickness(20, 8, 20, 20),
        };
        navStack.Children.Add(titleBlock);

        // Nav items
        var sections = new (string icon, string label, Func<FrameworkElement> content)[]
        {
            ("\uE713", "General", CreateGeneralPage),
            ("\uE720", "Speech Recognition", CreateSttPage),
            ("\uE767", "Audio", CreateAudioPage),
            ("\uE765", "Hotkey", CreateHotkeyPage),
            ("\uE771", "Appearance", CreateAppearancePage),
            ("\uE945", "AI Processing", CreateLlmPage),
        };

        if (historyStore != null)
        {
            var histSections = new (string icon, string label, Func<FrameworkElement> content)[sections.Length + 1];
            Array.Copy(sections, histSections, sections.Length);
            histSections[sections.Length] = ("\uE81C", "History", () => new HistoryView(historyStore));
            sections = histSections;
        }

        _contentArea = new ContentControl { Margin = new Thickness(24) };

        bool first = true;
        foreach (var (icon, label, contentFactory) in sections)
        {
            var navItem = CreateNavItem(icon, label, contentFactory);
            navStack.Children.Add(navItem);
            if (first)
            {
                SelectNavItem(navItem, contentFactory);
                first = false;
            }
        }

        navPanel.Child = navStack;
        Grid.SetColumn(navPanel, 0);
        root.Children.Add(navPanel);

        // ── Content area ─────────────────────────────────────────────
        var contentBorder = new Border
        {
            Background = TransparentBrush,
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _contentArea,
            }
        };
        Grid.SetColumn(contentBorder, 1);
        root.Children.Add(contentBorder);

        // ── Dark theme styles for standard controls ─────────────────
        ApplyDarkTheme(root);

        Content = root;

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Applies dark-theme styles including a full ComboBox ControlTemplate
    /// so the dropdown popup is also dark (WPF Popup has a separate visual tree).
    /// </summary>
    private static void ApplyDarkTheme(FrameworkElement root)
    {
        var res = new ResourceDictionary();
        var inputBg = new SolidColorBrush(InputBg);
        var inputBrd = new SolidColorBrush(InputBorder);
        var text = new SolidColorBrush(TextPrimary);
        var accent = new SolidColorBrush(AccentColor);
        var dropdownBg = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x3C));
        var hoverBg = new SolidColorBrush(Color.FromArgb(0x40, 0x63, 0xE6, 0xBE));

        // ── ComboBox: full template override ──
        var comboStyle = new Style(typeof(ComboBox));
        comboStyle.Setters.Add(new Setter(Control.ForegroundProperty, text));
        comboStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        comboStyle.Setters.Add(new Setter(Control.SnapsToDevicePixelsProperty, true));
        comboStyle.Setters.Add(new Setter(Control.TemplateProperty, BuildComboBoxTemplate(inputBg, inputBrd, text, dropdownBg)));
        res[typeof(ComboBox)] = comboStyle;

        // ── ComboBoxItem: dark with hover ──
        var comboItemStyle = new Style(typeof(ComboBoxItem));
        comboItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        comboItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, text));
        comboItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)));
        comboItemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        comboItemStyle.Setters.Add(new Setter(Control.SnapsToDevicePixelsProperty, true));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, hoverBg));
        comboItemStyle.Triggers.Add(hover);
        var selected = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, hoverBg));
        comboItemStyle.Triggers.Add(selected);
        res[typeof(ComboBoxItem)] = comboItemStyle;

        // ── TextBox ──
        var textBoxStyle = new Style(typeof(TextBox));
        textBoxStyle.Setters.Add(new Setter(Control.BackgroundProperty, inputBg));
        textBoxStyle.Setters.Add(new Setter(Control.ForegroundProperty, text));
        textBoxStyle.Setters.Add(new Setter(TextBox.CaretBrushProperty, accent));
        textBoxStyle.Setters.Add(new Setter(Control.BorderBrushProperty, inputBrd));
        textBoxStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        textBoxStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));
        textBoxStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        res[typeof(TextBox)] = textBoxStyle;

        // ── PasswordBox ──
        var pwStyle = new Style(typeof(PasswordBox));
        pwStyle.Setters.Add(new Setter(Control.BackgroundProperty, inputBg));
        pwStyle.Setters.Add(new Setter(Control.ForegroundProperty, text));
        pwStyle.Setters.Add(new Setter(PasswordBox.CaretBrushProperty, accent));
        pwStyle.Setters.Add(new Setter(Control.BorderBrushProperty, inputBrd));
        pwStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        pwStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));
        pwStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        res[typeof(PasswordBox)] = pwStyle;

        // ── CheckBox ──
        var checkStyle = new Style(typeof(CheckBox));
        checkStyle.Setters.Add(new Setter(Control.ForegroundProperty, text));
        checkStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        res[typeof(CheckBox)] = checkStyle;

        // ── ListBoxItem (nav) ──
        var listItemStyle = new Style(typeof(ListBoxItem));
        listItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        listItemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        listItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, text));
        listItemStyle.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        res[typeof(ListBoxItem)] = listItemStyle;

        root.Resources = res;
    }

    /// <summary>
    /// Builds a dark ComboBox ControlTemplate with a dark dropdown popup.
    /// </summary>
    private static ControlTemplate BuildComboBoxTemplate(Brush bg, Brush border, Brush fg, Brush dropdownBg)
    {
        var template = new ControlTemplate(typeof(ComboBox));

        // Root grid
        var gridFactory = new FrameworkElementFactory(typeof(Grid));
        gridFactory.SetValue(FrameworkElement.SnapsToDevicePixelsProperty, true);

        // Two columns: content | toggle button
        // ── Border (background) ──
        var borderFactory = new FrameworkElementFactory(typeof(Border), "Border");
        borderFactory.SetValue(Border.BackgroundProperty, bg);
        borderFactory.SetValue(Border.BorderBrushProperty, border);
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        gridFactory.AppendChild(borderFactory);

        // ── ToggleButton (covers entire area, transparent) ──
        var toggleFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.ToggleButton), "ToggleButton");
        toggleFactory.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        toggleFactory.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        toggleFactory.SetValue(FrameworkElement.FocusableProperty, false);
        toggleFactory.SetValue(UIElement.IsHitTestVisibleProperty, true);
        toggleFactory.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            new System.Windows.Data.Binding("IsDropDownOpen")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
                Mode = System.Windows.Data.BindingMode.TwoWay
            });

        // Arrow glyph
        var toggleTemplate = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
        var toggleBorder = new FrameworkElementFactory(typeof(Border));
        toggleBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);

        var arrowGrid = new FrameworkElementFactory(typeof(Grid));
        arrowGrid.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        arrowGrid.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        arrowGrid.SetValue(FrameworkElement.WidthProperty, 12.0);

        var arrowPath = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
        arrowPath.SetValue(System.Windows.Shapes.Path.DataProperty,
            System.Windows.Media.Geometry.Parse("M 0 0 L 4 4 L 8 0 Z"));
        arrowPath.SetValue(System.Windows.Shapes.Shape.FillProperty, fg);
        arrowPath.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        arrowPath.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        arrowGrid.AppendChild(arrowPath);
        toggleBorder.AppendChild(arrowGrid);
        toggleTemplate.VisualTree = toggleBorder;
        toggleFactory.SetValue(Control.TemplateProperty, toggleTemplate);

        gridFactory.AppendChild(toggleFactory);

        // ── ContentPresenter (selected item text) ──
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter), "ContentPresenter");
        contentFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 8, 28, 8));
        contentFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.ContentProperty,
            new System.Windows.TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
        contentFactory.SetValue(ContentPresenter.ContentTemplateSelectorProperty,
            new System.Windows.TemplateBindingExtension(ComboBox.ItemTemplateSelectorProperty));
        contentFactory.SetValue(TextElement.ForegroundProperty, fg);
        contentFactory.SetValue(UIElement.IsHitTestVisibleProperty, false);

        gridFactory.AppendChild(contentFactory);

        // ── Popup (dropdown) ──
        var popupFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup), "Popup");
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty,
            System.Windows.Controls.Primitives.PlacementMode.Bottom);
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
        popupFactory.SetValue(FrameworkElement.MinWidthProperty,
            new System.Windows.TemplateBindingExtension(FrameworkElement.ActualWidthProperty));
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.MaxHeightProperty, 300.0);
        popupFactory.SetBinding(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
            new System.Windows.Data.Binding("IsDropDownOpen")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        // Popup content: dark border + items presenter
        var popupBorder = new FrameworkElementFactory(typeof(Border));
        popupBorder.SetValue(Border.BackgroundProperty, dropdownBg);
        popupBorder.SetValue(Border.BorderBrushProperty, border);
        popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        popupBorder.SetValue(Border.PaddingProperty, new Thickness(0, 4, 0, 4));
        popupBorder.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

        var scrollFactory = new FrameworkElementFactory(typeof(ScrollViewer));
        scrollFactory.SetValue(ScrollViewer.CanContentScrollProperty, true);

        var itemsFactory = new FrameworkElementFactory(typeof(ItemsPresenter));
        scrollFactory.AppendChild(itemsFactory);
        popupBorder.AppendChild(scrollFactory);
        popupFactory.AppendChild(popupBorder);

        gridFactory.AppendChild(popupFactory);

        template.VisualTree = gridFactory;
        return template;
    }

    // ── DWM backdrop setup ───────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Dark mode title bar
        int dark = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        // Rounded corners
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        // Try Mica Alt (4) first, fallback to Acrylic (3), then tabbed Mica (2)
        int backdrop = 4; // Mica Alt
        int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        if (result != 0)
        {
            backdrop = 3; // Acrylic
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }

        // Extend frame into client area for backdrop to show through transparent bg
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    // ── Nav item factory ─────────────────────────────────────────────
    private ListBoxItem CreateNavItem(string icon, string label, Func<FrameworkElement> contentFactory)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(16, 0, 16, 0),
        };

        stack.Children.Add(new TextBlock
        {
            Text = icon,
            FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
            FontSize = 15,
            Foreground = TextDimBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        });

        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var item = new ListBoxItem
        {
            Content = stack,
            Padding = new Thickness(4, 10, 4, 10),
            Margin = new Thickness(8, 1, 8, 1),
            Background = TransparentBrush,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = Cursors.Hand,
        };

        // Rounded highlight on hover
        item.MouseEnter += (_, _) =>
        {
            if (item != _selectedNavItem)
                item.Background = new SolidColorBrush(NavHover);
        };
        item.MouseLeave += (_, _) =>
        {
            if (item != _selectedNavItem)
                item.Background = TransparentBrush;
        };

        item.Selected += (_, _) => SelectNavItem(item, contentFactory);
        item.PreviewMouseLeftButtonUp += (_, _) => SelectNavItem(item, contentFactory);

        return item;
    }

    private void SelectNavItem(ListBoxItem item, Func<FrameworkElement> contentFactory)
    {
        if (_selectedNavItem != null)
            _selectedNavItem.Background = TransparentBrush;

        _selectedNavItem = item;
        item.Background = new SolidColorBrush(NavSelected);
        _contentArea.Content = contentFactory();
    }

    // ── Page builders ────────────────────────────────────────────────

    private FrameworkElement CreateGeneralPage()
    {
        var panel = CreatePagePanel("General");

        AddCard(panel, card =>
        {
            card.Children.Add(CreateFieldLabel("Activation Mode"));
            var combo = StyledComboBox<ActivationMode>();
            combo.SelectedItem = _config.ActivationMode;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is ActivationMode m) { _config.ActivationMode = m; _onSave(_config); }
            };
            card.Children.Add(combo);
        });

        AddCard(panel, card =>
        {
            var check = StyledCheckBox("Run at Windows startup", _config.RunAtStartup);
            check.Checked += (_, _) => { _config.RunAtStartup = true; _onSave(_config); };
            check.Unchecked += (_, _) => { _config.RunAtStartup = false; _onSave(_config); };
            card.Children.Add(check);
        });

        return panel;
    }

    private FrameworkElement CreateSttPage()
    {
        var panel = CreatePagePanel("Speech Recognition");

        AddCard(panel, card =>
        {
            card.Children.Add(CreateFieldLabel("STT Provider"));
            var combo = StyledComboBox<SttProviderType>();
            combo.SelectedItem = _config.SttProvider;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is SttProviderType t) { _config.SttProvider = t; _onSave(_config); }
            };
            card.Children.Add(combo);

            card.Children.Add(CreateFieldLabel("API URL"));
            var url = StyledTextBox(_config.SttConfig.Url, "https://api.openai.com");
            url.TextChanged += (_, _) => { _config.SttConfig.Url = url.Text; _onSave(_config); };
            card.Children.Add(url);

            card.Children.Add(CreateFieldLabel("API Key"));
            var key = StyledPasswordBox(_config.SttConfig.ApiKey);
            key.PasswordChanged += (_, _) => { _config.SttConfig.ApiKey = key.Password; _onSave(_config); };
            card.Children.Add(key);

            card.Children.Add(CreateFieldLabel("Model"));
            var model = StyledTextBox(_config.SttConfig.Model, "whisper-1");
            model.TextChanged += (_, _) => { _config.SttConfig.Model = model.Text; _onSave(_config); };
            card.Children.Add(model);
        });

        AddCard(panel, card =>
        {
            card.Children.Add(CreateFieldLabel("Language"));
            var combo = new ComboBox
            {
                Background = InputBgBrush,
                Foreground = TextBrush,
                BorderBrush = InputBorderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 13,
            };
            combo.Items.Add("ru");
            combo.Items.Add("en");
            combo.SelectedItem = _config.Language;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is string l) { _config.Language = l; _onSave(_config); }
            };
            card.Children.Add(combo);
        });

        return panel;
    }

    private FrameworkElement CreateAudioPage()
    {
        var panel = CreatePagePanel("Audio");

        AddCard(panel, card =>
        {
            card.Children.Add(CreateFieldLabel("Microphone Device"));
            card.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_config.AudioDeviceId) ? "Default system device" : _config.AudioDeviceId,
                Foreground = TextDimBrush, FontSize = 12, FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 4, 0, 0),
            });
        });

        AddCard(panel, card =>
        {
            card.Children.Add(CreateFieldLabel("Recording Mode"));
            var combo = StyledComboBox<RecordingMode>();
            combo.SelectedItem = _config.RecordingMode;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is RecordingMode m) { _config.RecordingMode = m; _onSave(_config); }
            };
            card.Children.Add(combo);
        });

        AddCard(panel, card =>
        {
            card.Children.Add(CreateFieldLabel("Silence Timeout (ms)"));
            card.Children.Add(new TextBlock
            {
                Text = "How long to wait after speech stops before auto-ending recording",
                Foreground = TextDimBrush, FontSize = 11,
                Margin = new Thickness(0, 0, 0, 6),
            });

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 10000,
                Value = _config.SilenceTimeoutMs,
                TickFrequency = 500,
                IsSnapToTickEnabled = true,
                Foreground = AccentBrush,
            };
            var valueLabel = new TextBlock
            {
                Text = _config.SilenceTimeoutMs == 0 ? "Disabled (push-to-talk only)" : $"{_config.SilenceTimeoutMs} ms",
                Foreground = TextBrush,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 4, 0, 0),
            };
            slider.ValueChanged += (_, e) =>
            {
                _config.SilenceTimeoutMs = (int)e.NewValue;
                valueLabel.Text = _config.SilenceTimeoutMs == 0 ? "Disabled (push-to-talk only)" : $"{_config.SilenceTimeoutMs} ms";
                _onSave(_config);
            };
            card.Children.Add(slider);
            card.Children.Add(valueLabel);
        });

        return panel;
    }

    private FrameworkElement CreateHotkeyPage()
    {
        var panel = CreatePagePanel("Hotkey");

        AddCard(panel, card =>
        {
            card.Children.Add(CreateFieldLabel("Global Hotkey"));

            var modifiers = string.IsNullOrWhiteSpace(_config.Hotkey.Modifiers) ? "Ctrl+Shift" : _config.Hotkey.Modifiers;
            var key = string.IsNullOrWhiteSpace(_config.Hotkey.Key) ? "Space" : _config.Hotkey.Key;

            var hotkeyDisplay = new Border
            {
                Background = InputBgBrush,
                BorderBrush = InputBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = $"{modifiers} + {key}",
                    FontSize = 15,
                    FontWeight = FontWeights.Medium,
                    Foreground = AccentBrush,
                    FontFamily = new FontFamily("Cascadia Code,Consolas,Segoe UI"),
                }
            };
            card.Children.Add(hotkeyDisplay);

            card.Children.Add(new TextBlock
            {
                Text = "Edit config.json to change the hotkey",
                Foreground = TextDimBrush, FontSize = 11,
                Margin = new Thickness(0, 8, 0, 0),
            });
        });

        return panel;
    }

    private FrameworkElement CreateAppearancePage()
    {
        var panel = CreatePagePanel("Appearance");

        AddCard(panel, card =>
        {
            card.Children.Add(CreateFieldLabel("Overlay Animation"));
            var combo = StyledComboBox<AnimationStyle>();
            combo.SelectedItem = _config.AnimationStyle;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is AnimationStyle s) { _config.AnimationStyle = s; _onSave(_config); }
            };
            card.Children.Add(combo);
        });

        return panel;
    }

    private FrameworkElement CreateLlmPage()
    {
        var panel = CreatePagePanel("AI Processing");

        AddCard(panel, card =>
        {
            var check = StyledCheckBox("Enable LLM post-processing", _config.LlmPostProcessing.Enabled);
            check.Checked += (_, _) => { _config.LlmPostProcessing.Enabled = true; _onSave(_config); };
            check.Unchecked += (_, _) => { _config.LlmPostProcessing.Enabled = false; _onSave(_config); };
            card.Children.Add(check);
        });

        AddCard(panel, card =>
        {
            card.Children.Add(CreateFieldLabel("LLM API URL"));
            var url = StyledTextBox(_config.LlmPostProcessing.Url, "http://localhost:11434");
            url.TextChanged += (_, _) => { _config.LlmPostProcessing.Url = url.Text; _onSave(_config); };
            card.Children.Add(url);

            card.Children.Add(CreateFieldLabel("Processing Mode"));
            var combo = StyledComboBox<LlmPostProcessingMode>();
            combo.SelectedItem = _config.LlmPostProcessing.Mode;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is LlmPostProcessingMode m) { _config.LlmPostProcessing.Mode = m; _onSave(_config); }
            };
            card.Children.Add(combo);
        });

        return panel;
    }

    // ── Reusable UI building blocks ──────────────────────────────────

    private static StackPanel CreatePagePanel(string title)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(TextPrimary),
            Margin = new Thickness(0, 0, 0, 16),
        });
        return panel;
    }

    private static void AddCard(StackPanel parent, Action<StackPanel> build)
    {
        var inner = new StackPanel();
        build(inner);

        var card = new Border
        {
            Background = new SolidColorBrush(CardColor),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 10),
            Child = inner,
        };
        parent.Children.Add(card);
    }

    private static TextBlock CreateFieldLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeights.Medium,
        Foreground = new SolidColorBrush(TextSecondary),
        Margin = new Thickness(0, 0, 0, 2),
    };

    private static TextBox StyledTextBox(string text, string placeholder = "")
    {
        var box = new TextBox
        {
            Text = text,
            Background = InputBgBrush,
            Foreground = new SolidColorBrush(TextPrimary),
            CaretBrush = new SolidColorBrush(AccentColor),
            BorderBrush = InputBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 4, 0, 6),
            FontSize = 13,
        };

        if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(placeholder))
        {
            box.Text = placeholder;
            box.Foreground = new SolidColorBrush(TextSecondary);
            box.GotFocus += (_, _) =>
            {
                if (box.Text == placeholder)
                {
                    box.Text = "";
                    box.Foreground = new SolidColorBrush(TextPrimary);
                }
            };
            box.LostFocus += (_, _) =>
            {
                if (string.IsNullOrEmpty(box.Text))
                {
                    box.Text = placeholder;
                    box.Foreground = new SolidColorBrush(TextSecondary);
                }
            };
        }

        return box;
    }

    private static PasswordBox StyledPasswordBox(string password) => new()
    {
        Password = password,
        Background = InputBgBrush,
        Foreground = new SolidColorBrush(TextPrimary),
        CaretBrush = new SolidColorBrush(AccentColor),
        BorderBrush = InputBorderBrush,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 8, 10, 8),
        Margin = new Thickness(0, 4, 0, 6),
        FontSize = 13,
    };

    private static ComboBox StyledComboBox<T>() where T : struct, Enum
    {
        var combo = new ComboBox
        {
            Background = InputBgBrush,
            Foreground = new SolidColorBrush(TextPrimary),
            BorderBrush = InputBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 4, 0, 6),
            FontSize = 13,
        };
        foreach (var value in Enum.GetValues<T>())
            combo.Items.Add(value);
        return combo;
    }

    private static CheckBox StyledCheckBox(string label, bool isChecked) => new()
    {
        Content = label,
        IsChecked = isChecked,
        Foreground = new SolidColorBrush(TextPrimary),
        FontSize = 13,
        Margin = new Thickness(0, 2, 0, 2),
    };
}
