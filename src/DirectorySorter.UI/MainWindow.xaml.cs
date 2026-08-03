using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DirectorySorter.Core;
using Microsoft.Win32;

namespace DirectorySorter.UI;

public partial class MainWindow : Window
{
    private readonly Logger _log;
    private readonly PluginLoader _loader;
    private readonly SorterConfig _config;
    private readonly string _baseDir;
    private List<ISortPlugin> _plugins = new();
    private string? _lastJournalPath;

    public ObservableCollection<PreviewRow> PreviewRows { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _baseDir = AppContext.BaseDirectory;
        _log = new Logger(Path.Combine(_baseDir, "Logs", "ui.log"));
        _loader = new PluginLoader(_log);
        _config = SorterConfig.Load(Path.Combine(_baseDir, "sorter.config.json"));

        ResultsGrid.ItemsSource = PreviewRows;
        LoadPlugins();
    }

    private void LoadPlugins()
    {
        _plugins = _loader.LoadFrom(Path.Combine(_baseDir, _config.PluginsFolder)).ToList();

        if (_config.Rules.Count > 0)
            _plugins.Add(new RuleBasedPlugin(_config.Rules, _plugins, _config.RulesFallbackStrategy));

        StrategyCombo.ItemsSource = _plugins;
        StrategyCombo.DisplayMemberPath = nameof(ISortPlugin.DisplayName);
        if (_plugins.Count > 0)
            StrategyCombo.SelectedIndex = 0;

        StatusText.Text = _plugins.Count > 0
            ? $"Loaded {_plugins.Count} strategies."
            : "No plugins found -- build the Plugins.* projects and check sorter.config.json.";
    }

    private ISortPlugin? SelectedPlugin => StrategyCombo.SelectedItem as ISortPlugin;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder to sort" };
        if (dialog.ShowDialog() == true)
            FolderBox.Text = dialog.FolderName;
    }

    private SortOptions BuildOptions(bool dryRun) => new()
    {
        DryRun = dryRun,
        Recursive = RecursiveCheck.IsChecked == true,
        DetectDuplicates = DuplicatesCheck.IsChecked == true,
        ConflictResolution = (ConflictCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "rename"
    };

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateFolder(out var folder) || SelectedPlugin is null)
            return;

        var plugin = SelectedPlugin;
        SetBusy(true, "Scanning...");
        var options = BuildOptions(dryRun: true);
        var engine = new SortEngine(_log);

        try
        {
            var plan = await Task.Run(() => engine.Preview(folder, plugin, options));
            PreviewRows.Clear();
            foreach (var move in plan)
            {
                PreviewRows.Add(new PreviewRow
                {
                    Source = move.SourcePath,
                    Destination = move.RelativeDestination,
                    Note = move.IsDuplicate ? "Duplicate content" : ""
                });
            }
            StatusText.Text = $"Preview: {plan.Count} file(s) would be affected. Nothing has been moved yet.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Preview failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateFolder(out var folder) || SelectedPlugin is null)
            return;

        var plugin = SelectedPlugin;
        var confirm = MessageBox.Show(this,
            $"This will move files inside:\n{folder}\n\nA journal will be saved so this can be undone. Continue?",
            "Confirm sort", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        SetBusy(true, "Sorting...");
        var options = BuildOptions(dryRun: false);
        var engine = new SortEngine(_log);
        var journalFolder = Path.Combine(_baseDir, _config.JournalFolder);

        try
        {
            var result = await Task.Run(() => engine.Run(folder, plugin, options, journalFolder));
            _lastJournalPath = result.JournalPath;
            UndoButton.IsEnabled = _lastJournalPath is not null;
            PreviewRows.Clear();
            StatusText.Text = $"Moved {result.FilesMoved}, skipped {result.FilesSkipped}, " +
                               $"duplicates found {result.DuplicatesFound}." +
                               (_lastJournalPath is not null ? " Undo is available." : "");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Sort failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        var path = _lastJournalPath;
        if (path is null)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Journal files (*.json)|*.json",
                Title = "Choose a journal file to undo",
                InitialDirectory = Path.Combine(_baseDir, _config.JournalFolder)
            };
            if (dialog.ShowDialog() != true)
                return;
            path = dialog.FileName;
        }

        try
        {
            var restored = JournalManager.Undo(path);
            StatusText.Text = $"Undo complete. Restored {restored} file(s).";
            UndoButton.IsEnabled = false;
            _lastJournalPath = null;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Undo failed: {ex.Message}";
        }
    }

    private bool ValidateFolder(out string folder)
    {
        folder = FolderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            StatusText.Text = "Pick a valid folder first.";
            return false;
        }
        return true;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        PreviewButton.IsEnabled = !busy;
        RunButton.IsEnabled = !busy;
        BrowseButton.IsEnabled = !busy;
        BusyText.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (message is not null)
            StatusText.Text = message;
    }

    protected override void OnClosed(EventArgs e)
    {
        _loader.UnloadAll();
        base.OnClosed(e);
    }
}

public sealed class PreviewRow
{
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Note { get; set; } = "";
}
