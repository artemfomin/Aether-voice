using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VoiceInput.Core.Config;

namespace VoiceInput.App.Settings;

/// <summary>
/// Settings window for Voice Input. Fluent Design, dark theme.
/// Contains tabs: General, Speech Recognition, Audio, Hotkey, Appearance, AI Processing.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private readonly Action<AppConfig> _onSave;

    public SettingsWindow(AppConfig config, Action<AppConfig> onSave)
    {
        _config = config;
        _onSave = onSave;

        Title = "Voice Input — Settings";
        Width = 600;
        Height = 500;
        MinWidth = 500;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));

        var tabControl = new TabControl
        {
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
        };

        tabControl.Items.Add(CreateGeneralTab());
        tabControl.Items.Add(CreateSttTab());
        tabControl.Items.Add(CreateAudioTab());
        tabControl.Items.Add(CreateHotkeyTab());
        tabControl.Items.Add(CreateAppearanceTab());
        tabControl.Items.Add(CreateLlmTab());

        Content = tabControl;
    }

    private TabItem CreateGeneralTab()
    {
        var panel = CreatePanel();

        panel.Children.Add(CreateLabel("Activation Mode"));
        var activationCombo = CreateComboBox<ActivationMode>();
        activationCombo.SelectedItem = _config.ActivationMode;
        activationCombo.SelectionChanged += (_, _) =>
        {
            if (activationCombo.SelectedItem is ActivationMode mode)
            {
                _config.ActivationMode = mode;
                _onSave(_config);
            }
        };
        panel.Children.Add(activationCombo);

        var startupCheck = new CheckBox
        {
            Content = "Run at Windows startup",
            IsChecked = _config.RunAtStartup,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 10, 0, 0)
        };
        startupCheck.Checked += (_, _) => { _config.RunAtStartup = true; _onSave(_config); };
        startupCheck.Unchecked += (_, _) => { _config.RunAtStartup = false; _onSave(_config); };
        panel.Children.Add(startupCheck);

        return new TabItem { Header = "General", Content = panel };
    }

    private TabItem CreateSttTab()
    {
        var panel = CreatePanel();

        panel.Children.Add(CreateLabel("STT Provider"));
        var providerCombo = CreateComboBox<SttProviderType>();
        providerCombo.SelectedItem = _config.SttProvider;
        providerCombo.SelectionChanged += (_, _) =>
        {
            if (providerCombo.SelectedItem is SttProviderType type)
            {
                _config.SttProvider = type;
                _onSave(_config);
            }
        };
        panel.Children.Add(providerCombo);

        panel.Children.Add(CreateLabel("API URL"));
        var urlBox = CreateTextBox(_config.SttConfig.Url);
        urlBox.TextChanged += (_, _) => { _config.SttConfig.Url = urlBox.Text; _onSave(_config); };
        panel.Children.Add(urlBox);

        panel.Children.Add(CreateLabel("API Key"));
        var keyBox = new PasswordBox
        {
            Password = _config.SttConfig.ApiKey,
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 2, 0, 8)
        };
        keyBox.PasswordChanged += (_, _) => { _config.SttConfig.ApiKey = keyBox.Password; _onSave(_config); };
        panel.Children.Add(keyBox);

        panel.Children.Add(CreateLabel("Model"));
        var modelBox = CreateTextBox(_config.SttConfig.Model);
        modelBox.TextChanged += (_, _) => { _config.SttConfig.Model = modelBox.Text; _onSave(_config); };
        panel.Children.Add(modelBox);

        panel.Children.Add(CreateLabel("Language"));
        var langCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 2, 0, 8)
        };
        langCombo.Items.Add("ru");
        langCombo.Items.Add("en");
        langCombo.SelectedItem = _config.Language;
        langCombo.SelectionChanged += (_, _) =>
        {
            if (langCombo.SelectedItem is string lang) { _config.Language = lang; _onSave(_config); }
        };
        panel.Children.Add(langCombo);

        return new TabItem { Header = "Speech Recognition", Content = new ScrollViewer { Content = panel } };
    }

    private TabItem CreateAudioTab()
    {
        var panel = CreatePanel();

        panel.Children.Add(CreateLabel("Microphone Device"));
        panel.Children.Add(CreateLabel("(Device selector populated at runtime)"));

        panel.Children.Add(CreateLabel("Recording Mode"));
        var recCombo = CreateComboBox<RecordingMode>();
        recCombo.SelectedItem = _config.RecordingMode;
        recCombo.SelectionChanged += (_, _) =>
        {
            if (recCombo.SelectedItem is RecordingMode mode)
            {
                _config.RecordingMode = mode;
                _onSave(_config);
            }
        };
        panel.Children.Add(recCombo);

        return new TabItem { Header = "Audio", Content = panel };
    }

    private TabItem CreateHotkeyTab()
    {
        var panel = CreatePanel();
        panel.Children.Add(CreateLabel("Global Hotkey"));
        panel.Children.Add(CreateLabel($"Current: {_config.Hotkey.Modifiers}+{_config.Hotkey.Key}"));
        panel.Children.Add(CreateLabel("(Hotkey recorder to be added)"));
        return new TabItem { Header = "Hotkey", Content = panel };
    }

    private TabItem CreateAppearanceTab()
    {
        var panel = CreatePanel();

        panel.Children.Add(CreateLabel("Animation Style"));
        var animCombo = CreateComboBox<AnimationStyle>();
        animCombo.SelectedItem = _config.AnimationStyle;
        animCombo.SelectionChanged += (_, _) =>
        {
            if (animCombo.SelectedItem is AnimationStyle style)
            {
                _config.AnimationStyle = style;
                _onSave(_config);
            }
        };
        panel.Children.Add(animCombo);

        return new TabItem { Header = "Appearance", Content = panel };
    }

    private TabItem CreateLlmTab()
    {
        var panel = CreatePanel();

        var enableCheck = new CheckBox
        {
            Content = "Enable LLM Post-Processing",
            IsChecked = _config.LlmPostProcessing.Enabled,
            Foreground = Brushes.White
        };
        enableCheck.Checked += (_, _) => { _config.LlmPostProcessing.Enabled = true; _onSave(_config); };
        enableCheck.Unchecked += (_, _) => { _config.LlmPostProcessing.Enabled = false; _onSave(_config); };
        panel.Children.Add(enableCheck);

        panel.Children.Add(CreateLabel("LLM API URL"));
        var urlBox = CreateTextBox(_config.LlmPostProcessing.Url);
        urlBox.TextChanged += (_, _) => { _config.LlmPostProcessing.Url = urlBox.Text; _onSave(_config); };
        panel.Children.Add(urlBox);

        panel.Children.Add(CreateLabel("Mode"));
        var modeCombo = CreateComboBox<LlmPostProcessingMode>();
        modeCombo.SelectedItem = _config.LlmPostProcessing.Mode;
        modeCombo.SelectionChanged += (_, _) =>
        {
            if (modeCombo.SelectedItem is LlmPostProcessingMode mode)
            {
                _config.LlmPostProcessing.Mode = mode;
                _onSave(_config);
            }
        };
        panel.Children.Add(modeCombo);

        return new TabItem { Header = "AI Processing", Content = panel };
    }

    private static StackPanel CreatePanel() => new()
    {
        Margin = new Thickness(16),
    };

    private static TextBlock CreateLabel(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xB0)),
        FontSize = 13,
        Margin = new Thickness(0, 8, 0, 2)
    };

    private static TextBox CreateTextBox(string text) => new()
    {
        Text = text,
        Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E)),
        Foreground = Brushes.White,
        Margin = new Thickness(0, 2, 0, 8),
        Padding = new Thickness(4)
    };

    private static ComboBox CreateComboBox<T>() where T : struct, Enum
    {
        var combo = new ComboBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E)),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 2, 0, 8)
        };
        foreach (var value in Enum.GetValues<T>())
        {
            combo.Items.Add(value);
        }

        return combo;
    }
}
