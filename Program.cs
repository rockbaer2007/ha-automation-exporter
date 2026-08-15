using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length >= 2)
        {
            return AutomationExporter.RunCli(args[0], args[1], args.Length >= 3 ? args[2] : null);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new ExporterForm());
        return 0;
    }
}

internal sealed class ExporterForm : Form
{
    private static readonly Size FooterButtonSize = new(168, 30);
    private static readonly string DisplayVersion = AppVersion.GetDisplayVersion();

    private readonly TextBox sourceTextBox = new();
    private readonly TextBox filterTextBox = new();
    private readonly Label filterLabel = new();
    private readonly DataGridView automationGrid = new();
    private readonly Label statusLabel = new();
    private readonly Button sourceBrowseButton = new();
    private readonly Button loadButton = new();
    private readonly Button selectAllButton = new();
    private readonly Button selectNoneButton = new();
    private readonly Button settingsButton = new();
    private readonly Button detailsButton = new();
    private readonly Button exportButton = new();
    private readonly Button clearFilterButton = new();
    private readonly Button openExportFolderButton = new();
    private PortableSettings settings = PortableSettings.Load();
    private List<AutomationEntry> automations = [];
    private readonly Dictionary<int, bool> checkedAutomations = [];

    public ExporterForm()
    {
        I18n.Use(settings.Language);

        Text = GetWindowTitle();
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 560);
        Size = new Size(980, 680);

        var defaultSource = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "automations.yaml");
        var defaultOutput = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "automations-export-selected");

        sourceTextBox.Text = File.Exists(defaultSource) ? defaultSource : string.Empty;

        if (string.IsNullOrWhiteSpace(settings.ExportFolder))
        {
            settings = settings with { ExportFolder = defaultOutput };
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(CreateSourcePathRow(), 0, 0);
        root.Controls.Add(CreateFilterRow(), 0, 1);

        ConfigureGrid();
        root.Controls.Add(automationGrid, 0, 2);

        var footer = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            Padding = new Padding(0, 10, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(footer, 0, 3);

        ConfigureFooterButton(loadButton);
        loadButton.Click += (_, _) => LoadAutomations();

        ConfigureFooterButton(selectAllButton);
        selectAllButton.Click += (_, _) => SetAllChecked(true);

        ConfigureFooterButton(settingsButton);
        settingsButton.Click += (_, _) => ShowSettingsDialog();

        ConfigureFooterButton(selectNoneButton);
        selectNoneButton.Click += (_, _) => SetAllChecked(false);

        ConfigureFooterButton(detailsButton);
        detailsButton.Enabled = false;
        detailsButton.Click += (_, _) => ShowSelectedAutomationDetails();

        ConfigureFooterButton(exportButton);
        exportButton.Enabled = false;
        exportButton.Click += (_, _) => ExportSelected();

        ConfigureFooterButton(openExportFolderButton);
        openExportFolderButton.Click += (_, _) => OpenExportFolder();

        statusLabel.AutoSize = true;
        statusLabel.Anchor = AnchorStyles.Left;

        footer.Controls.Add(loadButton, 0, 0);
        footer.Controls.Add(selectAllButton, 1, 0);
        footer.Controls.Add(settingsButton, 2, 0);
        footer.Controls.Add(detailsButton, 3, 0);
        footer.Controls.Add(statusLabel, 4, 0);
        footer.SetColumnSpan(statusLabel, 2);
        footer.Controls.Add(selectNoneButton, 0, 1);
        footer.Controls.Add(exportButton, 1, 1);
        footer.Controls.Add(openExportFolderButton, 2, 1);

        ApplyUiText();

        if (!string.IsNullOrWhiteSpace(sourceTextBox.Text))
        {
            Load += (_, _) => LoadAutomations();
        }
    }

    private static void ConfigureFooterButton(Button button)
    {
        button.AutoSize = false;
        button.Size = FooterButtonSize;
        button.MinimumSize = FooterButtonSize;
        button.MaximumSize = FooterButtonSize;
        button.TextAlign = ContentAlignment.MiddleCenter;
    }

    private Control CreateSourcePathRow()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(0, 0, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        sourceTextBox.Dock = DockStyle.Fill;

        sourceBrowseButton.AutoSize = true;
        sourceBrowseButton.Click += (_, _) => SelectSourceFile();

        panel.Controls.Add(sourceTextBox, 0, 0);
        panel.Controls.Add(sourceBrowseButton, 1, 0);

        return panel;
    }

    private Control CreateFilterRow()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 0, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        filterLabel.Text = I18n.T("SearchLabel");
        filterLabel.AutoSize = true;
        filterLabel.Anchor = AnchorStyles.Left;
        filterLabel.TextAlign = ContentAlignment.MiddleLeft;

        filterTextBox.Dock = DockStyle.Fill;
        filterTextBox.PlaceholderText = I18n.T("SearchPlaceholder");
        filterTextBox.TextChanged += (_, _) => ApplyFilter();

        clearFilterButton.AutoSize = true;
        clearFilterButton.Click += (_, _) => filterTextBox.Clear();

        panel.Controls.Add(filterLabel, 0, 0);
        panel.Controls.Add(filterTextBox, 1, 0);
        panel.Controls.Add(clearFilterButton, 2, 0);
        return panel;
    }

    private void ConfigureGrid()
    {
        automationGrid.Dock = DockStyle.Fill;
        automationGrid.AllowUserToAddRows = false;
        automationGrid.AllowUserToDeleteRows = false;
        automationGrid.AllowUserToResizeRows = false;
        automationGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        automationGrid.BackgroundColor = SystemColors.Window;
        automationGrid.BorderStyle = BorderStyle.FixedSingle;
        automationGrid.MultiSelect = false;
        automationGrid.RowHeadersVisible = false;
        automationGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        automationGrid.CellDoubleClick += (_, args) =>
        {
            if (args.RowIndex >= 0)
            {
                ShowSelectedAutomationDetails();
            }
        };
        automationGrid.SelectionChanged += (_, _) => UpdateDetailsButtonState();

        automationGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "",
            Width = 42,
            FillWeight = 8,
            TrueValue = true,
            FalseValue = false
        });
        automationGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            ReadOnly = true,
            FillWeight = 42
        });
        automationGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            ReadOnly = true,
            FillWeight = 22
        });
        automationGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            ReadOnly = true,
            FillWeight = 36
        });
    }

    private void ApplyUiText()
    {
        Text = GetWindowTitle();
        sourceBrowseButton.Text = I18n.T("ChooseFileButton");
        loadButton.Text = I18n.T("LoadButton");
        selectAllButton.Text = I18n.T("SelectAllButton");
        settingsButton.Text = I18n.T("SettingsButton");
        selectNoneButton.Text = I18n.T("SelectNoneButton");
        detailsButton.Text = I18n.T("DetailsButton");
        exportButton.Text = I18n.T("ExportSelectedButton");
        clearFilterButton.Text = I18n.T("ClearFilterButton");
        openExportFolderButton.Text = I18n.T("OpenExportFolderButton");
        filterLabel.Text = I18n.T("SearchLabel");
        filterTextBox.PlaceholderText = I18n.T("SearchPlaceholder");

        automationGrid.Columns[1].HeaderText = I18n.T("AliasColumn");
        automationGrid.Columns[2].HeaderText = I18n.T("IdColumn");
        automationGrid.Columns[3].HeaderText = I18n.T("FileNameColumn");
    }

    private static string GetWindowTitle()
    {
        return I18n.Format("AppTitleWithVersion", I18n.T("AppTitle"), DisplayVersion);
    }

    private void ShowSettingsDialog()
    {
        using var dialog = new SettingsDialog(settings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        settings = dialog.Settings;
        I18n.Use(settings.Language);
        ApplyUiText();
        PortableSettings.Save(settings);
        statusLabel.Text = I18n.T("SettingSavedStatus");
    }

    private void SelectSourceFile()
    {
        var sourcePath = sourceTextBox.Text.Trim();
        var initialDirectory = File.Exists(sourcePath)
            ? Path.GetDirectoryName(sourcePath)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        using var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            FileName = File.Exists(sourcePath) ? Path.GetFileName(sourcePath) : "automations.yaml",
            Filter = I18n.T("YamlFileFilter"),
            InitialDirectory = string.IsNullOrWhiteSpace(initialDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : initialDirectory,
            RestoreDirectory = true,
            Title = I18n.T("SelectSourceTitle")
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            sourceTextBox.Text = dialog.FileName;
            LoadAutomations();
        }
    }

    private void LoadAutomations()
    {
        try
        {
            automations = AutomationExporter.Load(sourceTextBox.Text).ToList();
            checkedAutomations.Clear();

            foreach (var automation in automations)
            {
                checkedAutomations[automation.Index] = true;
            }

            PopulateGrid(AutomationExporter.Filter(automations, filterTextBox.Text).ToList());
            UpdateDetailsButtonState();
            UpdateStatus();
        }
        catch (Exception exception)
        {
            automationGrid.Rows.Clear();
            exportButton.Enabled = false;
            detailsButton.Enabled = false;
            statusLabel.Text = I18n.T("LoadFailedStatus");
            MessageBox.Show(this, exception.Message, I18n.T("LoadErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter()
    {
        StoreCurrentChecks();
        PopulateGrid(AutomationExporter.Filter(automations, filterTextBox.Text).ToList());
        UpdateStatus();
    }

    private void PopulateGrid(IReadOnlyCollection<AutomationEntry> entries)
    {
        automationGrid.Rows.Clear();

        foreach (var automation in entries)
        {
            var isChecked = checkedAutomations.TryGetValue(automation.Index, out var savedChecked)
                ? savedChecked
                : true;
            var rowIndex = automationGrid.Rows.Add(isChecked, automation.Alias, automation.Id, automation.FileName);
            automationGrid.Rows[rowIndex].Tag = automation;
        }

        exportButton.Enabled = automationGrid.Rows.Count > 0;
        UpdateDetailsButtonState();
    }

    private void StoreCurrentChecks()
    {
        automationGrid.EndEdit();

        foreach (DataGridViewRow row in automationGrid.Rows)
        {
            if (row.Tag is AutomationEntry automation)
            {
                checkedAutomations[automation.Index] = Convert.ToBoolean(row.Cells[0].Value);
            }
        }
    }

    private void UpdateStatus()
    {
        var filter = filterTextBox.Text.Trim();

        statusLabel.Text = string.IsNullOrWhiteSpace(filter)
            ? I18n.Format("LoadedStatus", automations.Count)
            : I18n.Format("FilteredStatus", automationGrid.Rows.Count, automations.Count);
    }

    private void UpdateDetailsButtonState()
    {
        detailsButton.Enabled = automationGrid.CurrentRow?.Tag is AutomationEntry;
    }

    private void ShowSelectedAutomationDetails()
    {
        if (automationGrid.CurrentRow?.Tag is not AutomationEntry automation)
        {
            MessageBox.Show(this, I18n.T("SelectAutomationMessage"), I18n.T("NoAutomationSelectedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new AutomationDetailsDialog(automation);
        dialog.ShowDialog(this);
    }

    private void SetAllChecked(bool isChecked)
    {
        foreach (DataGridViewRow row in automationGrid.Rows)
        {
            row.Cells[0].Value = isChecked;

            if (row.Tag is AutomationEntry automation)
            {
                checkedAutomations[automation.Index] = isChecked;
            }
        }
    }

    private void ExportSelected()
    {
        StoreCurrentChecks();

        var selected = automationGrid.Rows
            .Cast<DataGridViewRow>()
            .Where(row => row.Tag is AutomationEntry && Convert.ToBoolean(row.Cells[0].Value))
            .Select(row => (AutomationEntry)row.Tag!)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(this, I18n.T("SelectAtLeastOneMessage"), I18n.T("NothingSelectedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var exported = AutomationExporter.Export(selected, settings.ExportFolder).ToList();
            SaveSettings(showMessage: false);
            statusLabel.Text = I18n.Format("ExportedStatus", exported.Count);
            MessageBox.Show(
                this,
                I18n.Format("ExportCompletedMessage", exported.Count, Environment.NewLine, Path.GetFullPath(settings.ExportFolder)),
                I18n.T("ExportCompletedTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            statusLabel.Text = I18n.T("ExportFailedStatus");
            MessageBox.Show(this, exception.Message, I18n.T("ExportErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettings(bool showMessage = true)
    {
        try
        {
            PortableSettings.Save(settings);
            statusLabel.Text = I18n.T("SettingSavedStatus");

            if (showMessage)
            {
                MessageBox.Show(this, I18n.T("SettingSavedMessage"), I18n.T("SettingSavedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            statusLabel.Text = I18n.T("SaveFailedStatus");
            MessageBox.Show(this, exception.Message, I18n.T("SaveErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenExportFolder()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.ExportFolder))
            {
                MessageBox.Show(this, I18n.T("MissingOutputPath"), I18n.T("NoExportFolderTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var outputFolder = Path.GetFullPath(settings.ExportFolder.Trim());
            Directory.CreateDirectory(outputFolder);
            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(outputFolder);
            Process.Start(startInfo);
            SaveSettings(showMessage: false);
            statusLabel.Text = I18n.T("ExportFolderOpenedStatus");
        }
        catch (Exception exception)
        {
            statusLabel.Text = I18n.T("OpenExportFolderFailedStatus");
            MessageBox.Show(this, exception.Message, I18n.T("OpenExportFolderErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal sealed class SettingsDialog : Form
{
    private readonly TextBox exportFolderTextBox = new();
    private readonly ComboBox languageComboBox = new();

    public SettingsDialog(PortableSettings settings)
    {
        Settings = settings;

        Text = I18n.T("SettingsTitle");
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(660, 190);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(root);

        root.Controls.Add(CreateExportFolderRow(), 0, 0);
        root.Controls.Add(CreateLanguageRow(settings.Language), 0, 1);
        root.Controls.Add(CreateButtonRow(), 0, 2);
    }

    public PortableSettings Settings { get; private set; }

    private Control CreateExportFolderRow()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 0, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = I18n.T("ExportFolderLabel"),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };

        exportFolderTextBox.Text = Settings.ExportFolder;
        exportFolderTextBox.Dock = DockStyle.Fill;

        var browseButton = new Button
        {
            Text = I18n.T("ChooseFolderButton"),
            AutoSize = true
        };
        browseButton.Click += (_, _) => SelectOutputFolder();

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(exportFolderTextBox, 1, 0);
        panel.Controls.Add(browseButton, 2, 0);
        return panel;
    }

    private Control CreateLanguageRow(string selectedLanguage)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(0, 0, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = I18n.T("LanguageLabel"),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };

        languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        languageComboBox.Width = 220;
        languageComboBox.DisplayMember = nameof(LanguageChoice.DisplayName);
        languageComboBox.ValueMember = nameof(LanguageChoice.Key);
        languageComboBox.DataSource = I18n.GetLanguageChoices().ToList();
        languageComboBox.SelectedValue = selectedLanguage;

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(languageComboBox, 1, 0);
        return panel;
    }

    private Control CreateButtonRow()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            MinimumSize = new Size(0, 44),
            Padding = new Padding(0, 8, 0, 0)
        };

        var okButton = new Button
        {
            Text = I18n.T("OkButton"),
            AutoSize = true,
            DialogResult = DialogResult.OK
        };
        okButton.Click += (_, _) => SaveDialogSettings();

        var cancelButton = new Button
        {
            Text = I18n.T("CancelButton"),
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;
        panel.Controls.Add(okButton);
        panel.Controls.Add(cancelButton);
        return panel;
    }

    private void SelectOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = I18n.T("SelectOutputTitle"),
            SelectedPath = Directory.Exists(exportFolderTextBox.Text)
                ? exportFolderTextBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            exportFolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void SaveDialogSettings()
    {
        Settings = Settings with
        {
            ExportFolder = exportFolderTextBox.Text.Trim(),
            Language = languageComboBox.SelectedValue as string ?? I18n.SystemLanguageKey
        };
    }
}

internal static class AppVersion
{
    public static string GetDisplayVersion()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.0.0";
        }

        var metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex >= 0 ? version[..metadataIndex] : version;
    }
}

internal static class AppIcon
{
    public static Icon? Load()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record PortableSettings(string ExportFolder = "", string Language = I18n.SystemLanguageKey)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsFile => Path.Combine(AppContext.BaseDirectory, "HaAutomationExporter.settings.json");

    public static PortableSettings Load()
    {
        if (!File.Exists(SettingsFile))
        {
            return new PortableSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsFile, Encoding.UTF8);
            return JsonSerializer.Deserialize<PortableSettings>(json, JsonOptions) ?? new PortableSettings();
        }
        catch
        {
            return new PortableSettings();
        }
    }

    public static void Save(PortableSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFile, json + Environment.NewLine, new UTF8Encoding(false));
    }
}

internal static class I18n
{
    public const string SystemLanguageKey = "system";

    private static readonly Dictionary<string, string> SystemLanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["de"] = "de",
        ["en"] = "en",
        ["fr"] = "fr",
        ["es"] = "es",
        ["pl"] = "pl",
        ["ru"] = "ru"
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new()
        {
            ["LanguageSystem"] = "System",
            ["LanguageGerman"] = "German",
            ["LanguageEnglish"] = "English",
            ["LanguageFrench"] = "French",
            ["LanguageSpanish"] = "Spanish",
            ["LanguagePolish"] = "Polish",
            ["LanguageRussian"] = "Russian",
            ["AppTitle"] = "Export Home Assistant Automations",
            ["AppTitleWithVersion"] = "{0} v{1}",
            ["SourceFileLabel"] = "automations.yaml",
            ["ExportFolderLabel"] = "Export folder",
            ["LanguageLabel"] = "Language",
            ["ChooseFileButton"] = "Choose file...",
            ["ChooseFolderButton"] = "Choose folder...",
            ["SaveSettingButton"] = "Save setting",
            ["SettingsButton"] = "Settings",
            ["SettingsTitle"] = "Settings",
            ["OkButton"] = "OK",
            ["CancelButton"] = "Cancel",
            ["LoadButton"] = "Load",
            ["SelectAllButton"] = "Select all",
            ["SelectNoneButton"] = "Select none",
            ["DetailsButton"] = "Details",
            ["ExportSelectedButton"] = "Export selected",
            ["SearchLabel"] = "Search",
            ["SearchPlaceholder"] = "Search alias, ID, file name or YAML, e.g. pool",
            ["ClearFilterButton"] = "Clear",
            ["OpenExportFolderButton"] = "Open Explorer",
            ["AliasColumn"] = "Alias",
            ["IdColumn"] = "ID",
            ["FileNameColumn"] = "File name",
            ["YamlFileFilter"] = "YAML files (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*",
            ["SelectSourceTitle"] = "Select automations.yaml",
            ["SelectOutputTitle"] = "Select export folder",
            ["LoadedStatus"] = "{0} automation(s) loaded.",
            ["FilteredStatus"] = "{0} of {1} automation(s) found.",
            ["LoadFailedStatus"] = "Loading failed.",
            ["LoadErrorTitle"] = "Load error",
            ["SelectAutomationMessage"] = "Please select an automation first.",
            ["NoAutomationSelectedTitle"] = "No automation selected",
            ["SelectAtLeastOneMessage"] = "Please select at least one automation.",
            ["NothingSelectedTitle"] = "Nothing selected",
            ["ExportedStatus"] = "{0} automation(s) exported.",
            ["ExportCompletedMessage"] = "{0} automation(s) exported to:{1}{2}",
            ["ExportCompletedTitle"] = "Export complete",
            ["ExportFailedStatus"] = "Export failed.",
            ["ExportErrorTitle"] = "Export error",
            ["NoExportFolderTitle"] = "No export folder",
            ["ExportFolderOpenedStatus"] = "Export folder opened in Explorer.",
            ["OpenExportFolderFailedStatus"] = "Could not open Explorer.",
            ["OpenExportFolderErrorTitle"] = "Open error",
            ["SettingSavedStatus"] = "Setting saved.",
            ["SettingSavedMessage"] = "The export folder and language were saved next to the EXE.",
            ["SettingSavedTitle"] = "Setting saved",
            ["SaveFailedStatus"] = "Saving failed.",
            ["SaveErrorTitle"] = "Save error",
            ["DetailsTitle"] = "Automation: {0}",
            ["EntitiesTab"] = "Entities",
            ["YamlTab"] = "YAML",
            ["EntityColumn"] = "Entity",
            ["SourceColumn"] = "Source",
            ["LineColumn"] = "Line",
            ["ContextColumn"] = "Context",
            ["NoEntitiesFooter"] = "No entities detected.",
            ["EntitiesFooter"] = "{0} entity/entities detected. Template matches are detected heuristically.",
            ["CopyYamlButton"] = "Copy YAML",
            ["SourceYaml"] = "YAML",
            ["SourceTemplate"] = "Template",
            ["MissingSourcePath"] = "Please select an automations.yaml file.",
            ["MissingOutputPath"] = "Please select an export folder.",
            ["NoAutomationsFound"] = "No top-level automation entries found. Expected a YAML list starting with '- ...'.",
            ["NoFilterMatches"] = "No automation entries match the filter: {0}"
        },
        ["de"] = new()
        {
            ["LanguageSystem"] = "System",
            ["LanguageGerman"] = "Deutsch",
            ["LanguageEnglish"] = "Englisch",
            ["LanguageFrench"] = "Französisch",
            ["LanguageSpanish"] = "Spanisch",
            ["LanguagePolish"] = "Polnisch",
            ["LanguageRussian"] = "Russisch",
            ["AppTitle"] = "Home Assistant Automationen exportieren",
            ["AppTitleWithVersion"] = "{0} v{1}",
            ["SourceFileLabel"] = "automations.yaml",
            ["ExportFolderLabel"] = "Export-Ordner",
            ["LanguageLabel"] = "Sprache",
            ["ChooseFileButton"] = "Datei wählen...",
            ["ChooseFolderButton"] = "Ordner wählen...",
            ["SaveSettingButton"] = "Einstellung speichern",
            ["SettingsButton"] = "Einstellungen",
            ["SettingsTitle"] = "Einstellungen",
            ["OkButton"] = "OK",
            ["CancelButton"] = "Abbrechen",
            ["LoadButton"] = "Laden",
            ["SelectAllButton"] = "Alle auswählen",
            ["SelectNoneButton"] = "Keine auswählen",
            ["DetailsButton"] = "Details",
            ["ExportSelectedButton"] = "Ausgewählte exportieren",
            ["SearchLabel"] = "Suche",
            ["SearchPlaceholder"] = "Alias, ID, Dateiname oder YAML durchsuchen, z.B. pool",
            ["ClearFilterButton"] = "Zurücksetzen",
            ["OpenExportFolderButton"] = "Explorer öffnen",
            ["AliasColumn"] = "Alias",
            ["IdColumn"] = "ID",
            ["FileNameColumn"] = "Dateiname",
            ["YamlFileFilter"] = "YAML-Dateien (*.yaml;*.yml)|*.yaml;*.yml|Alle Dateien (*.*)|*.*",
            ["SelectSourceTitle"] = "automations.yaml auswählen",
            ["SelectOutputTitle"] = "Export-Ordner auswählen",
            ["LoadedStatus"] = "{0} Automation(en) geladen.",
            ["FilteredStatus"] = "{0} von {1} Automation(en) gefunden.",
            ["LoadFailedStatus"] = "Laden fehlgeschlagen.",
            ["LoadErrorTitle"] = "Fehler beim Laden",
            ["SelectAutomationMessage"] = "Bitte zuerst eine Automation auswählen.",
            ["NoAutomationSelectedTitle"] = "Keine Automation ausgewählt",
            ["SelectAtLeastOneMessage"] = "Bitte mindestens eine Automation auswählen.",
            ["NothingSelectedTitle"] = "Nichts ausgewählt",
            ["ExportedStatus"] = "{0} Automation(en) exportiert.",
            ["ExportCompletedMessage"] = "{0} Automation(en) exportiert nach:{1}{2}",
            ["ExportCompletedTitle"] = "Export abgeschlossen",
            ["ExportFailedStatus"] = "Export fehlgeschlagen.",
            ["ExportErrorTitle"] = "Fehler beim Export",
            ["NoExportFolderTitle"] = "Kein Export-Ordner",
            ["ExportFolderOpenedStatus"] = "Export-Ordner im Explorer geöffnet.",
            ["OpenExportFolderFailedStatus"] = "Explorer konnte nicht geöffnet werden.",
            ["OpenExportFolderErrorTitle"] = "Fehler beim Öffnen",
            ["SettingSavedStatus"] = "Einstellung gespeichert.",
            ["SettingSavedMessage"] = "Export-Ordner und Sprache wurden neben der EXE gespeichert.",
            ["SettingSavedTitle"] = "Einstellung gespeichert",
            ["SaveFailedStatus"] = "Speichern fehlgeschlagen.",
            ["SaveErrorTitle"] = "Fehler beim Speichern",
            ["DetailsTitle"] = "Automation: {0}",
            ["EntitiesTab"] = "Entitäten",
            ["YamlTab"] = "YAML",
            ["EntityColumn"] = "Entität",
            ["SourceColumn"] = "Quelle",
            ["LineColumn"] = "Zeile",
            ["ContextColumn"] = "Kontext",
            ["NoEntitiesFooter"] = "Keine Entitäten erkannt.",
            ["EntitiesFooter"] = "{0} Entität(en) erkannt. Template-Treffer sind heuristisch erkannt.",
            ["CopyYamlButton"] = "YAML kopieren",
            ["SourceYaml"] = "YAML",
            ["SourceTemplate"] = "Template",
            ["MissingSourcePath"] = "Bitte eine automations.yaml auswählen.",
            ["MissingOutputPath"] = "Bitte einen Export-Ordner auswählen.",
            ["NoAutomationsFound"] = "Keine Top-Level-Automationen gefunden. Erwartet wird eine YAML-Liste, die mit '- ...' beginnt.",
            ["NoFilterMatches"] = "Keine Automation passt zum Filter: {0}"
        },
        ["fr"] = new()
        {
            ["LanguageSystem"] = "Système",
            ["LanguageGerman"] = "Allemand",
            ["LanguageEnglish"] = "Anglais",
            ["LanguageFrench"] = "Français",
            ["LanguageSpanish"] = "Espagnol",
            ["LanguagePolish"] = "Polonais",
            ["LanguageRussian"] = "Russe",
            ["AppTitle"] = "Exporter les automatisations Home Assistant",
            ["AppTitleWithVersion"] = "{0} v{1}",
            ["SourceFileLabel"] = "automations.yaml",
            ["ExportFolderLabel"] = "Dossier export",
            ["LanguageLabel"] = "Langue",
            ["ChooseFileButton"] = "Choisir fichier...",
            ["ChooseFolderButton"] = "Choisir dossier...",
            ["SaveSettingButton"] = "Enregistrer",
            ["SettingsButton"] = "Paramètres",
            ["SettingsTitle"] = "Paramètres",
            ["OkButton"] = "OK",
            ["CancelButton"] = "Annuler",
            ["LoadButton"] = "Charger",
            ["SelectAllButton"] = "Tout sélectionner",
            ["SelectNoneButton"] = "Aucun",
            ["DetailsButton"] = "Détails",
            ["ExportSelectedButton"] = "Exporter sélection",
            ["AliasColumn"] = "Alias",
            ["IdColumn"] = "ID",
            ["FileNameColumn"] = "Nom de fichier",
            ["YamlFileFilter"] = "Fichiers YAML (*.yaml;*.yml)|*.yaml;*.yml|Tous les fichiers (*.*)|*.*",
            ["SelectSourceTitle"] = "Sélectionner automations.yaml",
            ["SelectOutputTitle"] = "Sélectionner le dossier d'export",
            ["LoadedStatus"] = "{0} automatisation(s) chargée(s).",
            ["LoadFailedStatus"] = "Chargement échoué.",
            ["LoadErrorTitle"] = "Erreur de chargement",
            ["SelectAutomationMessage"] = "Sélectionnez d'abord une automatisation.",
            ["NoAutomationSelectedTitle"] = "Aucune automatisation sélectionnée",
            ["SelectAtLeastOneMessage"] = "Sélectionnez au moins une automatisation.",
            ["NothingSelectedTitle"] = "Aucune sélection",
            ["ExportedStatus"] = "{0} automatisation(s) exportée(s).",
            ["ExportCompletedMessage"] = "{0} automatisation(s) exportée(s) vers :{1}{2}",
            ["ExportCompletedTitle"] = "Export terminé",
            ["ExportFailedStatus"] = "Export échoué.",
            ["ExportErrorTitle"] = "Erreur d'export",
            ["SettingSavedStatus"] = "Paramètre enregistré.",
            ["SettingSavedMessage"] = "Le dossier d'export et la langue ont été enregistrés à côté de l'EXE.",
            ["SettingSavedTitle"] = "Paramètre enregistré",
            ["SaveFailedStatus"] = "Enregistrement échoué.",
            ["SaveErrorTitle"] = "Erreur d'enregistrement",
            ["DetailsTitle"] = "Automatisation : {0}",
            ["EntitiesTab"] = "Entités",
            ["YamlTab"] = "YAML",
            ["EntityColumn"] = "Entité",
            ["SourceColumn"] = "Source",
            ["LineColumn"] = "Ligne",
            ["ContextColumn"] = "Contexte",
            ["NoEntitiesFooter"] = "Aucune entité détectée.",
            ["EntitiesFooter"] = "{0} entité(s) détectée(s). Les correspondances de modèle sont heuristiques.",
            ["CopyYamlButton"] = "Copier YAML",
            ["SourceYaml"] = "YAML",
            ["SourceTemplate"] = "Modèle",
            ["MissingSourcePath"] = "Sélectionnez un fichier automations.yaml.",
            ["MissingOutputPath"] = "Sélectionnez un dossier d'export.",
            ["NoAutomationsFound"] = "Aucune automatisation de premier niveau trouvée. Une liste YAML commençant par '- ...' est attendue."
        },
        ["es"] = new()
        {
            ["LanguageSystem"] = "Sistema",
            ["LanguageGerman"] = "Alemán",
            ["LanguageEnglish"] = "Inglés",
            ["LanguageFrench"] = "Francés",
            ["LanguageSpanish"] = "Español",
            ["LanguagePolish"] = "Polaco",
            ["LanguageRussian"] = "Ruso",
            ["AppTitle"] = "Exportar automatizaciones de Home Assistant",
            ["AppTitleWithVersion"] = "{0} v{1}",
            ["SourceFileLabel"] = "automations.yaml",
            ["ExportFolderLabel"] = "Carpeta export",
            ["LanguageLabel"] = "Idioma",
            ["ChooseFileButton"] = "Elegir archivo...",
            ["ChooseFolderButton"] = "Elegir carpeta...",
            ["SaveSettingButton"] = "Guardar",
            ["SettingsButton"] = "Configuración",
            ["SettingsTitle"] = "Configuración",
            ["OkButton"] = "OK",
            ["CancelButton"] = "Cancelar",
            ["LoadButton"] = "Cargar",
            ["SelectAllButton"] = "Seleccionar todo",
            ["SelectNoneButton"] = "Ninguna",
            ["DetailsButton"] = "Detalles",
            ["ExportSelectedButton"] = "Exportar selección",
            ["AliasColumn"] = "Alias",
            ["IdColumn"] = "ID",
            ["FileNameColumn"] = "Archivo",
            ["YamlFileFilter"] = "Archivos YAML (*.yaml;*.yml)|*.yaml;*.yml|Todos los archivos (*.*)|*.*",
            ["SelectSourceTitle"] = "Seleccionar automations.yaml",
            ["SelectOutputTitle"] = "Seleccionar carpeta de exportación",
            ["LoadedStatus"] = "{0} automatización(es) cargada(s).",
            ["LoadFailedStatus"] = "Error al cargar.",
            ["LoadErrorTitle"] = "Error de carga",
            ["SelectAutomationMessage"] = "Seleccione primero una automatización.",
            ["NoAutomationSelectedTitle"] = "Ninguna automatización seleccionada",
            ["SelectAtLeastOneMessage"] = "Seleccione al menos una automatización.",
            ["NothingSelectedTitle"] = "Nada seleccionado",
            ["ExportedStatus"] = "{0} automatización(es) exportada(s).",
            ["ExportCompletedMessage"] = "{0} automatización(es) exportada(s) a:{1}{2}",
            ["ExportCompletedTitle"] = "Exportación completada",
            ["ExportFailedStatus"] = "Error al exportar.",
            ["ExportErrorTitle"] = "Error de exportación",
            ["SettingSavedStatus"] = "Configuración guardada.",
            ["SettingSavedMessage"] = "La carpeta de exportación y el idioma se guardaron junto al EXE.",
            ["SettingSavedTitle"] = "Configuración guardada",
            ["SaveFailedStatus"] = "Error al guardar.",
            ["SaveErrorTitle"] = "Error al guardar",
            ["DetailsTitle"] = "Automatización: {0}",
            ["EntitiesTab"] = "Entidades",
            ["YamlTab"] = "YAML",
            ["EntityColumn"] = "Entidad",
            ["SourceColumn"] = "Origen",
            ["LineColumn"] = "Línea",
            ["ContextColumn"] = "Contexto",
            ["NoEntitiesFooter"] = "No se detectaron entidades.",
            ["EntitiesFooter"] = "{0} entidad(es) detectada(s). Las coincidencias de plantillas son heurísticas.",
            ["CopyYamlButton"] = "Copiar YAML",
            ["SourceYaml"] = "YAML",
            ["SourceTemplate"] = "Plantilla",
            ["MissingSourcePath"] = "Seleccione un archivo automations.yaml.",
            ["MissingOutputPath"] = "Seleccione una carpeta de exportación.",
            ["NoAutomationsFound"] = "No se encontraron automatizaciones de nivel superior. Se esperaba una lista YAML que empieza con '- ...'."
        },
        ["pl"] = new()
        {
            ["LanguageSystem"] = "System",
            ["LanguageGerman"] = "Niemiecki",
            ["LanguageEnglish"] = "Angielski",
            ["LanguageFrench"] = "Francuski",
            ["LanguageSpanish"] = "Hiszpański",
            ["LanguagePolish"] = "Polski",
            ["LanguageRussian"] = "Rosyjski",
            ["AppTitle"] = "Eksport automatyzacji Home Assistant",
            ["AppTitleWithVersion"] = "{0} v{1}",
            ["SourceFileLabel"] = "automations.yaml",
            ["ExportFolderLabel"] = "Folder eksportu",
            ["LanguageLabel"] = "Język",
            ["ChooseFileButton"] = "Wybierz plik...",
            ["ChooseFolderButton"] = "Wybierz folder...",
            ["SaveSettingButton"] = "Zapisz",
            ["SettingsButton"] = "Ustawienia",
            ["SettingsTitle"] = "Ustawienia",
            ["OkButton"] = "OK",
            ["CancelButton"] = "Anuluj",
            ["LoadButton"] = "Wczytaj",
            ["SelectAllButton"] = "Zaznacz wszystko",
            ["SelectNoneButton"] = "Wyczyść",
            ["DetailsButton"] = "Szczegóły",
            ["ExportSelectedButton"] = "Eksportuj wybrane",
            ["AliasColumn"] = "Alias",
            ["IdColumn"] = "ID",
            ["FileNameColumn"] = "Nazwa pliku",
            ["YamlFileFilter"] = "Pliki YAML (*.yaml;*.yml)|*.yaml;*.yml|Wszystkie pliki (*.*)|*.*",
            ["SelectSourceTitle"] = "Wybierz automations.yaml",
            ["SelectOutputTitle"] = "Wybierz folder eksportu",
            ["LoadedStatus"] = "Wczytano automatyzacje: {0}.",
            ["LoadFailedStatus"] = "Wczytywanie nie powiodło się.",
            ["LoadErrorTitle"] = "Błąd wczytywania",
            ["SelectAutomationMessage"] = "Najpierw wybierz automatyzację.",
            ["NoAutomationSelectedTitle"] = "Nie wybrano automatyzacji",
            ["SelectAtLeastOneMessage"] = "Wybierz co najmniej jedną automatyzację.",
            ["NothingSelectedTitle"] = "Nic nie wybrano",
            ["ExportedStatus"] = "Wyeksportowano automatyzacje: {0}.",
            ["ExportCompletedMessage"] = "Wyeksportowano automatyzacje: {0} do:{1}{2}",
            ["ExportCompletedTitle"] = "Eksport zakończony",
            ["ExportFailedStatus"] = "Eksport nie powiódł się.",
            ["ExportErrorTitle"] = "Błąd eksportu",
            ["SettingSavedStatus"] = "Ustawienie zapisane.",
            ["SettingSavedMessage"] = "Folder eksportu i język zapisano obok pliku EXE.",
            ["SettingSavedTitle"] = "Ustawienie zapisane",
            ["SaveFailedStatus"] = "Zapisywanie nie powiodło się.",
            ["SaveErrorTitle"] = "Błąd zapisu",
            ["DetailsTitle"] = "Automatyzacja: {0}",
            ["EntitiesTab"] = "Encje",
            ["YamlTab"] = "YAML",
            ["EntityColumn"] = "Encja",
            ["SourceColumn"] = "Źródło",
            ["LineColumn"] = "Wiersz",
            ["ContextColumn"] = "Kontekst",
            ["NoEntitiesFooter"] = "Nie wykryto encji.",
            ["EntitiesFooter"] = "Wykryto encje: {0}. Trafienia w szablonach są heurystyczne.",
            ["CopyYamlButton"] = "Kopiuj YAML",
            ["SourceYaml"] = "YAML",
            ["SourceTemplate"] = "Szablon",
            ["MissingSourcePath"] = "Wybierz plik automations.yaml.",
            ["MissingOutputPath"] = "Wybierz folder eksportu.",
            ["NoAutomationsFound"] = "Nie znaleziono automatyzacji najwyższego poziomu. Oczekiwana jest lista YAML zaczynająca się od '- ...'."
        },
        ["ru"] = new()
        {
            ["LanguageSystem"] = "Система",
            ["LanguageGerman"] = "Немецкий",
            ["LanguageEnglish"] = "Английский",
            ["LanguageFrench"] = "Французский",
            ["LanguageSpanish"] = "Испанский",
            ["LanguagePolish"] = "Польский",
            ["LanguageRussian"] = "Русский",
            ["AppTitle"] = "Экспорт автоматизаций Home Assistant",
            ["AppTitleWithVersion"] = "{0} v{1}",
            ["SourceFileLabel"] = "automations.yaml",
            ["ExportFolderLabel"] = "Папка экспорта",
            ["LanguageLabel"] = "Язык",
            ["ChooseFileButton"] = "Выбрать файл...",
            ["ChooseFolderButton"] = "Выбрать папку...",
            ["SaveSettingButton"] = "Сохранить",
            ["SettingsButton"] = "Настройки",
            ["SettingsTitle"] = "Настройки",
            ["OkButton"] = "OK",
            ["CancelButton"] = "Отмена",
            ["LoadButton"] = "Загрузить",
            ["SelectAllButton"] = "Выбрать все",
            ["SelectNoneButton"] = "Снять выбор",
            ["DetailsButton"] = "Детали",
            ["ExportSelectedButton"] = "Экспорт выбранных",
            ["AliasColumn"] = "Алиас",
            ["IdColumn"] = "ID",
            ["FileNameColumn"] = "Имя файла",
            ["YamlFileFilter"] = "Файлы YAML (*.yaml;*.yml)|*.yaml;*.yml|Все файлы (*.*)|*.*",
            ["SelectSourceTitle"] = "Выбрать automations.yaml",
            ["SelectOutputTitle"] = "Выбрать папку экспорта",
            ["LoadedStatus"] = "Загружено автоматизаций: {0}.",
            ["LoadFailedStatus"] = "Ошибка загрузки.",
            ["LoadErrorTitle"] = "Ошибка загрузки",
            ["SelectAutomationMessage"] = "Сначала выберите автоматизацию.",
            ["NoAutomationSelectedTitle"] = "Автоматизация не выбрана",
            ["SelectAtLeastOneMessage"] = "Выберите хотя бы одну автоматизацию.",
            ["NothingSelectedTitle"] = "Ничего не выбрано",
            ["ExportedStatus"] = "Экспортировано автоматизаций: {0}.",
            ["ExportCompletedMessage"] = "Экспортировано автоматизаций: {0} в:{1}{2}",
            ["ExportCompletedTitle"] = "Экспорт завершен",
            ["ExportFailedStatus"] = "Ошибка экспорта.",
            ["ExportErrorTitle"] = "Ошибка экспорта",
            ["SettingSavedStatus"] = "Настройка сохранена.",
            ["SettingSavedMessage"] = "Папка экспорта и язык сохранены рядом с EXE.",
            ["SettingSavedTitle"] = "Настройка сохранена",
            ["SaveFailedStatus"] = "Ошибка сохранения.",
            ["SaveErrorTitle"] = "Ошибка сохранения",
            ["DetailsTitle"] = "Автоматизация: {0}",
            ["EntitiesTab"] = "Объекты",
            ["YamlTab"] = "YAML",
            ["EntityColumn"] = "Объект",
            ["SourceColumn"] = "Источник",
            ["LineColumn"] = "Строка",
            ["ContextColumn"] = "Контекст",
            ["NoEntitiesFooter"] = "Объекты не обнаружены.",
            ["EntitiesFooter"] = "Обнаружено объектов: {0}. Совпадения в шаблонах определяются эвристически.",
            ["CopyYamlButton"] = "Копировать YAML",
            ["SourceYaml"] = "YAML",
            ["SourceTemplate"] = "Шаблон",
            ["MissingSourcePath"] = "Выберите файл automations.yaml.",
            ["MissingOutputPath"] = "Выберите папку экспорта.",
            ["NoAutomationsFound"] = "Автоматизации верхнего уровня не найдены. Ожидается YAML-список, начинающийся с '- ...'."
        }
    };

    private static string language = "en";

    public static void Use(string? languageKey)
    {
        language = ResolveLanguage(languageKey);
    }

    public static string T(string key)
    {
        if (Translations.TryGetValue(language, out var selected) && selected.TryGetValue(key, out var translated))
        {
            return translated;
        }

        return Translations["en"].TryGetValue(key, out var fallback) ? fallback : key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, T(key), args);
    }

    public static IEnumerable<LanguageChoice> GetLanguageChoices()
    {
        yield return new LanguageChoice(SystemLanguageKey, T("LanguageSystem"));
        yield return new LanguageChoice("de", T("LanguageGerman"));
        yield return new LanguageChoice("en", T("LanguageEnglish"));
        yield return new LanguageChoice("fr", T("LanguageFrench"));
        yield return new LanguageChoice("es", T("LanguageSpanish"));
        yield return new LanguageChoice("pl", T("LanguagePolish"));
        yield return new LanguageChoice("ru", T("LanguageRussian"));
    }

    private static string ResolveLanguage(string? languageKey)
    {
        if (string.IsNullOrWhiteSpace(languageKey) || string.Equals(languageKey, SystemLanguageKey, StringComparison.OrdinalIgnoreCase))
        {
            return SystemLanguageMap.TryGetValue(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, out var detected)
                ? detected
                : "en";
        }

        return Translations.ContainsKey(languageKey) ? languageKey : "en";
    }
}

internal sealed record LanguageChoice(string Key, string DisplayName);

internal sealed class AutomationDetailsDialog : Form
{
    public AutomationDetailsDialog(AutomationEntry automation)
    {
        Text = I18n.Format("DetailsTitle", automation.Alias);
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 520);
        Size = new Size(900, 640);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Text = string.IsNullOrWhiteSpace(automation.Id)
                ? automation.Alias
                : $"{automation.Alias} ({automation.Id})",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 8)
        };
        root.Controls.Add(title, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        root.Controls.Add(tabs, 0, 1);

        tabs.TabPages.Add(CreateEntitiesTab(automation));
        tabs.TabPages.Add(CreateYamlTab(automation));
    }

    private static TabPage CreateEntitiesTab(AutomationEntry automation)
    {
        var tab = new TabPage(I18n.T("EntitiesTab"));
        var entities = AutomationExporter.ExtractEntities(automation).ToList();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tab.Controls.Add(root);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = I18n.T("EntityColumn"),
            FillWeight = 46
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = I18n.T("SourceColumn"),
            FillWeight = 20
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = I18n.T("LineColumn"),
            FillWeight = 10
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = I18n.T("ContextColumn"),
            FillWeight = 60
        });

        foreach (var entity in entities)
        {
            grid.Rows.Add(entity.EntityId, entity.Source, entity.LineNumber, entity.Context);
        }

        var footer = new Label
        {
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
            Text = entities.Count == 0
                ? I18n.T("NoEntitiesFooter")
                : I18n.Format("EntitiesFooter", entities.Count)
        };

        root.Controls.Add(grid, 0, 0);
        root.Controls.Add(footer, 0, 1);
        return tab;
    }

    private static TabPage CreateYamlTab(AutomationEntry automation)
    {
        var tab = new TabPage(I18n.T("YamlTab"));
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tab.Controls.Add(root);

        var yamlTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font(FontFamily.GenericMonospace, 9F),
            Text = automation.Yaml.TrimEnd()
        };

        var copyButton = new Button
        {
            Text = I18n.T("CopyYamlButton"),
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 8, 0, 0)
        };
        copyButton.Click += (_, _) => Clipboard.SetText(yamlTextBox.Text);

        root.Controls.Add(yamlTextBox, 0, 0);
        root.Controls.Add(copyButton, 0, 1);
        return tab;
    }
}

internal static class AutomationExporter
{
    private static readonly Regex EntityIdRegex = new(
        @"\b(?:alarm_control_panel|automation|binary_sensor|button|calendar|camera|climate|cover|device_tracker|event|fan|humidifier|input_boolean|input_button|input_datetime|input_number|input_select|input_text|light|lock|media_player|number|person|remote|scene|script|select|sensor|siren|sun|switch|text|timer|update|vacuum|valve|water_heater|weather|zone)\.[A-Za-z0-9_]+\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static int RunCli(string sourcePath, string outputPath, string? filter = null)
    {
        try
        {
            var loaded = Load(sourcePath).ToList();
            var automations = Filter(loaded, filter).ToList();

            if (loaded.Count == 0)
            {
                Console.Error.WriteLine(I18n.T("NoAutomationsFound"));
                return 1;
            }

            if (automations.Count == 0)
            {
                Console.Error.WriteLine(I18n.Format("NoFilterMatches", filter ?? string.Empty));
                return 1;
            }

            var exported = Export(automations, outputPath).ToList();

            foreach (var export in exported)
            {
                Console.WriteLine($"{export.Index,3}: {export.FileName}");
            }

            Console.WriteLine();
            Console.WriteLine(string.IsNullOrWhiteSpace(filter)
                ? $"Exported {exported.Count} automation(s) to:"
                : $"Exported {exported.Count} of {loaded.Count} automation(s) matching \"{filter}\" to:");
            Console.WriteLine(Path.GetFullPath(outputPath));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static IEnumerable<AutomationEntry> Filter(IEnumerable<AutomationEntry> automations, string? filter)
    {
        var normalizedFilter = filter?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedFilter))
        {
            return automations;
        }

        return automations.Where(automation =>
            automation.Alias.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
            automation.Id.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
            automation.FileName.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
            automation.Yaml.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<AutomationEntry> Load(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException(I18n.T("MissingSourcePath"));
        }

        var sourceFile = Path.GetFullPath(sourcePath);

        if (!File.Exists(sourceFile))
        {
            throw new FileNotFoundException($"Input file not found: {sourceFile}");
        }

        var lines = File.ReadAllLines(sourceFile, Encoding.UTF8);
        var index = 0;
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in SplitTopLevelAutomationBlocks(lines))
        {
            index++;

            var yaml = ConvertListItemToStandaloneYaml(block);
            var alias = ReadScalarValue(yaml, "alias");
            var id = ReadScalarValue(yaml, "id");
            var fileName = MakeUniqueFileName(CreateBaseFileName(alias, id, index), usedNames);

            yield return new AutomationEntry(
                Index: index,
                Id: id ?? string.Empty,
                Alias: alias ?? $"Automation {index:000}",
                FileName: fileName,
                Yaml: yaml);
        }
    }

    public static IEnumerable<AutomationExport> Export(IEnumerable<AutomationEntry> automations, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException(I18n.T("MissingOutputPath"));
        }

        var outputFolder = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(outputFolder);

        var exported = 0;
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var automation in automations)
        {
            exported++;

            var fileName = MakeUniqueFileName(automation.FileName, usedNames);
            var targetFile = Path.Combine(outputFolder, fileName);

            File.WriteAllText(targetFile, CreateImportReadyYaml(automation).TrimEnd() + Environment.NewLine, new UTF8Encoding(false));
            yield return new AutomationExport(exported, fileName, targetFile);
        }
    }

    public static string CreateImportReadyYaml(AutomationEntry automation)
    {
        var lines = automation.Yaml.ReplaceLineEndings("\n").Split('\n').ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count > 0 && Regex.IsMatch(lines[0], @"^id:\s*.*$", RegexOptions.CultureInvariant))
        {
            lines.RemoveAt(0);
        }

        for (var index = 0; index < lines.Count; index++)
        {
            lines[index] = ConvertToImportReadyLine(lines[index]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ConvertToImportReadyLine(string line)
    {
        return line switch
        {
            "triggers:" => "trigger:",
            "conditions:" => "condition:",
            "actions:" => "action:",
            _ => ConvertAutomationListItemLine(line)
        };
    }

    private static string ConvertAutomationListItemLine(string line)
    {
        var triggerMatch = Regex.Match(line, @"^(?<indent>\s*)-\s+trigger:\s*(?<value>.+?)\s*$", RegexOptions.CultureInvariant);

        if (triggerMatch.Success)
        {
            return $"{triggerMatch.Groups["indent"].Value}- platform: {triggerMatch.Groups["value"].Value}";
        }

        var actionMatch = Regex.Match(line, @"^(?<indent>\s*)-\s+action:\s*(?<value>[A-Za-z0-9_]+\.[A-Za-z0-9_]+)\s*$", RegexOptions.CultureInvariant);

        if (actionMatch.Success)
        {
            return $"{actionMatch.Groups["indent"].Value}- service: {actionMatch.Groups["value"].Value}";
        }

        return line;
    }

    public static IEnumerable<AutomationEntityReference> ExtractEntities(AutomationEntry automation)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = automation.Yaml.ReplaceLineEndings("\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var isTemplateLike =
                line.Contains("{{", StringComparison.Ordinal) ||
                line.Contains("{%", StringComparison.Ordinal) ||
                line.Contains("states(", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("state_attr(", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("is_state(", StringComparison.OrdinalIgnoreCase);

            foreach (Match match in EntityIdRegex.Matches(line))
            {
                var entityId = match.Value;
                var key = $"{entityId}|{index + 1}";

                if (!seen.Add(key))
                {
                    continue;
                }

                yield return new AutomationEntityReference(
                    EntityId: entityId,
                    Source: isTemplateLike ? I18n.T("SourceTemplate") : I18n.T("SourceYaml"),
                    LineNumber: index + 1,
                    Context: line.Trim());
            }
        }
    }

    private static IEnumerable<List<string>> SplitTopLevelAutomationBlocks(string[] lines)
    {
        List<string>? current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (current is { Count: > 0 })
                {
                    yield return current;
                }

                current = new List<string> { line };
                continue;
            }

            current?.Add(line);
        }

        if (current is { Count: > 0 })
        {
            yield return current;
        }
    }

    private static string ConvertListItemToStandaloneYaml(List<string> block)
    {
        var output = new StringBuilder();

        for (var index = 0; index < block.Count; index++)
        {
            var line = block[index];

            if (index == 0)
            {
                output.AppendLine(line.Length >= 2 ? line[2..] : string.Empty);
                continue;
            }

            output.AppendLine(line.StartsWith("  ", StringComparison.Ordinal) ? line[2..] : line);
        }

        return output.ToString();
    }

    private static string? ReadScalarValue(string yaml, string key)
    {
        var match = Regex.Match(
            yaml,
            $@"(?m)^{Regex.Escape(key)}:\s*(?<value>.*)\s*$",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["value"].Value.Trim();

        if (value.Length >= 2 &&
            ((value.StartsWith('\'') && value.EndsWith('\'')) ||
             (value.StartsWith('"') && value.EndsWith('"'))))
        {
            value = value[1..^1];
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string CreateBaseFileName(string? alias, string? id, int index)
    {
        var name = !string.IsNullOrWhiteSpace(alias)
            ? alias
            : !string.IsNullOrWhiteSpace(id)
                ? $"automation-{id}"
                : $"automation-{index:000}";

        return Slugify(name) + ".yaml";
    }

    private static string MakeUniqueFileName(string fileName, HashSet<string> usedNames)
    {
        if (usedNames.Add(fileName))
        {
            return fileName;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{name}-{suffix}{extension}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Slugify(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormD)
            .ToLowerInvariant();

        var output = new StringBuilder();
        var previousWasDash = false;

        foreach (var character in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                output.Append(character);
                previousWasDash = false;
                continue;
            }

            if (!previousWasDash)
            {
                output.Append('-');
                previousWasDash = true;
            }
        }

        var slug = output.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "automation" : slug;
    }
}

internal sealed record AutomationEntry(int Index, string Id, string Alias, string FileName, string Yaml);

internal sealed record AutomationEntityReference(string EntityId, string Source, int LineNumber, string Context);

internal sealed record AutomationExport(int Index, string FileName, string TargetFile);
