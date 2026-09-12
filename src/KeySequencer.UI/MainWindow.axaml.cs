using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using KeySequencer.Core.Domain;
using KeySequencer.Core.Services;
using KeySequencer.UI.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KeySequencer.UI;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly ISequenceExecutor _executor = new SequenceExecutor(new Win32InputSender());
    private readonly GlobalKeyboardHook _hotkeyHook = new();
    private readonly TrayIcon _trayIcon;
    private bool _enabled;
    private bool _reallyClosing;

    public MainWindow()
    {
        InitializeComponent();
        ResetToDefaults();

        _hotkeyHook.HotkeyPressed += OnHotkeyPressed;
        _hotkeyHook.Start();

        // Keep the active hotkey in sync with the UI at all times, not just when a specific
        // button is clicked - otherwise typing a new hotkey silently leaves the previous one
        // armed until Reset/Load/"Gerar prévia" happens to run.
        HotkeyComboBox.PropertyChanged += OnHotkeyControlPropertyChanged;
        HotkeyRequiresCtrl.PropertyChanged += OnHotkeyControlPropertyChanged;
        HotkeyRequiresAlt.PropertyChanged += OnHotkeyControlPropertyChanged;

        _trayIcon = CreateTrayIcon();

        // Minimizing or clicking the window's close (X) button only hides the window to the
        // tray - the hotkey hook keeps running in the background, which is the point (this app
        // is meant to sit hidden while another app/game has focus). Actually quitting only
        // happens via the tray icon's "Sair" menu item, which sets _reallyClosing before Close().
        Closing += OnWindowClosing;
        PropertyChanged += OnWindowPropertyChanged;

        Closed += (_, _) =>
        {
            _hotkeyHook.Dispose();
            _trayIcon.Dispose();
        };
    }

    private TrayIcon CreateTrayIcon()
    {
        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://KeySequencer.UI/Assets/app.ico"))),
            ToolTipText = "Key Sequencer",
            IsVisible = false
        };

        var showItem = new NativeMenuItem("Mostrar");
        showItem.Click += (_, _) => RestoreFromTray();

        var exitItem = new NativeMenuItem("Sair");
        exitItem.Click += (_, _) =>
        {
            _reallyClosing = true;
            Close();
        };

        var menu = new NativeMenu();
        menu.Add(showItem);
        menu.Add(exitItem);
        trayIcon.Menu = menu;
        trayIcon.Clicked += (_, _) => RestoreFromTray();

        return trayIcon;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_reallyClosing) return;

        e.Cancel = true;
        HideToTray();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _trayIcon.IsVisible = true;
    }

    private void RestoreFromTray()
    {
        _trayIcon.IsVisible = false;
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    private void OnHotkeyControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ComboBox.TextProperty || e.Property == CheckBox.IsCheckedProperty)
        {
            UpdateHotkeyTarget();
        }
    }

    private void UpdateHotkeyTarget()
    {
        _hotkeyHook.RequiresCtrl = HotkeyRequiresCtrl.IsChecked == true;
        _hotkeyHook.RequiresAlt = HotkeyRequiresAlt.IsChecked == true;
        _hotkeyHook.TargetVirtualKey = VirtualKeyMapper.TryResolve(
            NormalizeKey(HotkeyComboBox.Text, "SPACE"), out var vk, out _)
            ? vk
            : (ushort)0;

        // Only eat the physical keypress while armed. With the app DESLIGADO the hotkey key
        // (e.g. SPACE) must behave normally everywhere - including inside this app's own text
        // fields - otherwise the low-level hook silently blocks typing it system-wide.
        _hotkeyHook.Suppress = _enabled;
    }

    private void OnHotkeyPressed()
    {
        if (!_enabled)
        {
            StatusText.Text = "Status: ative no botão LIGADO/DESLIGADO para executar a sequência";
            return;
        }

        if (_executor.IsRunning) return;

        var config = BuildSequenceConfiguration();
        StatusText.Text = config.Keys.Count == 0
            ? "Status: nenhuma tecla configurada"
            : $"Status: executando sequência com {config.Keys.Count} tecla(s)...";

        _ = RunSequenceAsync(config);
    }

    private async Task RunSequenceAsync(SequenceConfiguration config)
    {
        try
        {
            await _executor.RunAsync(config);
            StatusText.Text = "Status: sequência executada";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Status: erro ao executar sequência ({ex.Message})";
        }
    }

    private SequenceConfiguration BuildSequenceConfiguration()
    {
        var pingMs = ParseInt(PingTextBox.Text, 0);
        return new SequenceConfiguration(
            ParseKeys(KeysTextBox.Text),
            new HotkeyConfig(
                NormalizeKey(HotkeyComboBox.Text, "SPACE"),
                Ctrl: HotkeyRequiresCtrl.IsChecked == true,
                Alt: HotkeyRequiresAlt.IsChecked == true),
            pingMs);
    }

    private void OnToggleStatusClick(object? sender, RoutedEventArgs e)
    {
        _enabled = !_enabled;
        ToggleStatusButton.Content = _enabled ? "LIGADO" : "DESLIGADO";

        ToggleStatusButton.Classes.Remove("green");
        ToggleStatusButton.Classes.Remove("red");
        ToggleStatusButton.Classes.Add(_enabled ? "green" : "red");

        UpdateHotkeyTarget();

        StatusText.Text = _enabled
            ? "Status: execução habilitada"
            : "Status: execução desabilitada";
    }

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        UpdateHotkeyTarget();

        var config = BuildSequenceConfiguration();
        PreviewBox.Text = config.Keys.Count == 0
            ? "(nenhuma tecla configurada)"
            : string.Join(" -> ", config.Keys);

        StatusText.Text = $"Status: sequência com {config.Keys.Count} tecla(s), ping {config.PingMs}ms";
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        ResetToDefaults();
        StatusText.Text = "Status: configuração resetada";
    }

    private void ResetToDefaults()
    {
        _enabled = false;
        ToggleStatusButton.Content = "DESLIGADO";
        ToggleStatusButton.Classes.Remove("green");
        if (!ToggleStatusButton.Classes.Contains("red"))
        {
            ToggleStatusButton.Classes.Add("red");
        }

        HotkeyComboBox.Text = "SPACE";
        HotkeyRequiresCtrl.IsChecked = false;
        HotkeyRequiresAlt.IsChecked = false;
        PingTextBox.Text = "0";
        KeysTextBox.Text = string.Empty;
        PreviewBox.Text = "(sem prévia)";
        SaveStatusText.Text = string.Empty;

        UpdateHotkeyTarget();
    }

    private static List<string> ParseKeys(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        return raw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => NormalizeKey(line, string.Empty))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToList();
    }

    private static string NormalizeKey(string? raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        var normalizedParts = raw
            .Replace(" ", "+")
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.ToUpperInvariant())
            .Select(part => part switch
            {
                "CONTROL" => "CTRL",
                "OPTION" => "ALT",
                _ => part
            })
            .ToArray();

        if (normalizedParts.Length == 0)
        {
            return fallback;
        }

        return string.Join("+", normalizedParts);
    }

    private static int ParseInt(string? raw, int fallback)
    {
        return int.TryParse(raw, out var value) && value >= 0 ? value : fallback;
    }

    private async void OnSaveConfigClick(object? sender, RoutedEventArgs e)
    {
        var suggestedName = ConfigNameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(suggestedName)) suggestedName = "config";

        Directory.CreateDirectory(_configService.ConfigDirectory);
        var startFolder = await StorageProvider.TryGetFolderFromPathAsync(_configService.ConfigDirectory);

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar configuração",
            SuggestedFileName = suggestedName,
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("Configuração JSON") { Patterns = ["*.json"] }],
            SuggestedStartLocation = startFolder
        });

        if (file is null) return;

        var name = Path.GetFileNameWithoutExtension(file.Name);
        var config = BuildSavedConfig(name);
        _configService.Save(file.Path.LocalPath, config);

        ConfigNameBox.Text = name;
        StatusText.Text = $"Status: configuração salva em {file.Path.LocalPath}";
        SaveStatusText.Text = $"Salvo em: {file.Path.LocalPath}";
    }

    private async void OnLoadConfigClick(object? sender, RoutedEventArgs e)
    {
        IStorageFolder? startFolder = null;
        if (Directory.Exists(_configService.ConfigDirectory))
            startFolder = await StorageProvider.TryGetFolderFromPathAsync(_configService.ConfigDirectory);

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importar configuração",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Configuração JSON") { Patterns = ["*.json"] }],
            SuggestedStartLocation = startFolder
        });

        if (files.Count == 0) return;

        try
        {
            var config = _configService.Load(files[0].Path.LocalPath);
            ApplySavedConfig(config);
            ConfigNameBox.Text = config.Name;
            StatusText.Text = $"Status: configuração '{config.Name}' carregada";
            SaveStatusText.Text = $"Carregado de: {files[0].Path.LocalPath}";
        }
        catch
        {
            StatusText.Text = "Status: erro ao importar o arquivo";
            SaveStatusText.Text = "Erro ao importar o arquivo.";
        }
    }

    private void OnOpenConfigFolderClick(object? sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_configService.ConfigDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _configService.ConfigDirectory,
            UseShellExecute = true
        });
    }

    private SavedConfig BuildSavedConfig(string name) => new()
    {
        Name = name,
        Hotkey = new HotkeyConfigDto
        {
            Key = NormalizeKey(HotkeyComboBox.Text, "SPACE"),
            Ctrl = HotkeyRequiresCtrl.IsChecked == true,
            Alt = HotkeyRequiresAlt.IsChecked == true
        },
        Ping = ParseInt(PingTextBox.Text, 0),
        Keys = ParseKeys(KeysTextBox.Text)
    };

    private void ApplySavedConfig(SavedConfig config)
    {
        HotkeyComboBox.Text = config.Hotkey.Key;
        HotkeyRequiresCtrl.IsChecked = config.Hotkey.Ctrl;
        HotkeyRequiresAlt.IsChecked = config.Hotkey.Alt;
        PingTextBox.Text = config.Ping.ToString();
        KeysTextBox.Text = string.Join(Environment.NewLine, config.Keys);

        UpdateHotkeyTarget();
    }
}
