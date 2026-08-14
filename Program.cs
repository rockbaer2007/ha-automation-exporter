using System.Text;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Windows.Forms;

if (args.Length >= 2)
{
    return AutomationExporter.RunCli(args[0], args[1]);
}

ApplicationConfiguration.Initialize();
Application.Run(new ExporterForm());
return 0;

internal sealed class ExporterForm : Form
{
    private readonly TextBox sourceTextBox = new();
    private readonly TextBox outputTextBox = new();
    private readonly DataGridView automationGrid = new();
    private readonly Label statusLabel = new();
    private readonly Button exportButton = new();
    private List<AutomationEntry> automations = [];

    public ExporterForm()
    {
        Text = "Home Assistant Automationen exportieren";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 560);
        Size = new Size(1060, 680);

        var defaultSource = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "automations.yaml");
        var defaultOutput = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "automations-export-selected");

        sourceTextBox.Text = File.Exists(defaultSource) ? defaultSource : string.Empty;
        outputTextBox.Text = AppSettings.Default.ExportFolder;

        if (string.IsNullOrWhiteSpace(outputTextBox.Text))
        {
            outputTextBox.Text = defaultOutput;
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

        root.Controls.Add(CreatePathRow("automations.yaml", sourceTextBox, "Datei wählen...", SelectSourceFile), 0, 0);
        root.Controls.Add(CreatePathRow("Export-Ordner", outputTextBox, "Ordner wählen...", SelectOutputFolder, () => SaveSettings()), 0, 1);

        ConfigureGrid();
        root.Controls.Add(automationGrid, 0, 2);

        var footer = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            Padding = new Padding(0, 10, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.Controls.Add(footer, 0, 3);

        var loadButton = new Button { Text = "Laden", AutoSize = true };
        loadButton.Click += (_, _) => LoadAutomations();

        var selectAllButton = new Button { Text = "Alle auswählen", AutoSize = true };
        selectAllButton.Click += (_, _) => SetAllChecked(true);

        var selectNoneButton = new Button { Text = "Keine auswählen", AutoSize = true };
        selectNoneButton.Click += (_, _) => SetAllChecked(false);

        exportButton.Text = "Ausgewählte exportieren";
        exportButton.AutoSize = true;
        exportButton.Enabled = false;
        exportButton.Click += (_, _) => ExportSelected();

        statusLabel.AutoSize = true;
        statusLabel.Anchor = AnchorStyles.Left;

        footer.Controls.Add(loadButton, 0, 0);
        footer.Controls.Add(selectAllButton, 1, 0);
        footer.Controls.Add(statusLabel, 2, 0);
        footer.Controls.Add(selectNoneButton, 3, 0);
        footer.Controls.Add(exportButton, 4, 0);

        if (!string.IsNullOrWhiteSpace(sourceTextBox.Text))
        {
            Load += (_, _) => LoadAutomations();
        }
    }

    private static Control CreatePathRow(
        string labelText,
        TextBox textBox,
        string buttonText,
        Action browseAction,
        Action? saveAction = null)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = saveAction is null ? 3 : 4,
            Padding = new Padding(0, 0, 0, 8)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        if (saveAction is not null)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };

        textBox.Dock = DockStyle.Fill;

        var button = new Button { Text = buttonText, AutoSize = true };
        button.Click += (_, _) => browseAction();

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(textBox, 1, 0);
        panel.Controls.Add(button, 2, 0);

        if (saveAction is not null)
        {
            var saveButton = new Button { Text = "Einstellung speichern", AutoSize = true };
            saveButton.Click += (_, _) => saveAction();
            panel.Controls.Add(saveButton, 3, 0);
        }

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
            HeaderText = "Alias",
            ReadOnly = true,
            FillWeight = 42
        });
        automationGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "ID",
            ReadOnly = true,
            FillWeight = 22
        });
        automationGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Dateiname",
            ReadOnly = true,
            FillWeight = 36
        });
    }

    private void SelectSourceFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "YAML-Dateien (*.yaml;*.yml)|*.yaml;*.yml|Alle Dateien (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(sourceTextBox.Text) ? "automations.yaml" : sourceTextBox.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            sourceTextBox.Text = dialog.FileName;
            LoadAutomations();
        }
    }

    private void SelectOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = Directory.Exists(outputTextBox.Text) ? outputTextBox.Text : string.Empty
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            outputTextBox.Text = dialog.SelectedPath;
            SaveSettings(showMessage: false);
        }
    }

    private void LoadAutomations()
    {
        try
        {
            automations = AutomationExporter.Load(sourceTextBox.Text).ToList();
            automationGrid.Rows.Clear();

            foreach (var automation in automations)
            {
                var rowIndex = automationGrid.Rows.Add(true, automation.Alias, automation.Id, automation.FileName);
                automationGrid.Rows[rowIndex].Tag = automation;
            }

            exportButton.Enabled = automations.Count > 0;
            statusLabel.Text = $"{automations.Count} Automation(en) geladen.";
        }
        catch (Exception exception)
        {
            exportButton.Enabled = false;
            statusLabel.Text = "Laden fehlgeschlagen.";
            MessageBox.Show(this, exception.Message, "Fehler beim Laden", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetAllChecked(bool isChecked)
    {
        foreach (DataGridViewRow row in automationGrid.Rows)
        {
            row.Cells[0].Value = isChecked;
        }
    }

    private void ExportSelected()
    {
        automationGrid.EndEdit();

        var selected = automationGrid.Rows
            .Cast<DataGridViewRow>()
            .Where(row => row.Tag is AutomationEntry && Convert.ToBoolean(row.Cells[0].Value))
            .Select(row => (AutomationEntry)row.Tag!)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Bitte mindestens eine Automation auswählen.", "Nichts ausgewählt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var exported = AutomationExporter.Export(selected, outputTextBox.Text).ToList();
            SaveSettings(showMessage: false);
            statusLabel.Text = $"{exported.Count} Automation(en) exportiert.";
            MessageBox.Show(
                this,
                $"{exported.Count} Automation(en) exportiert nach:{Environment.NewLine}{Path.GetFullPath(outputTextBox.Text)}",
                "Export abgeschlossen",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            statusLabel.Text = "Export fehlgeschlagen.";
            MessageBox.Show(this, exception.Message, "Fehler beim Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettings(bool showMessage = true)
    {
        try
        {
            AppSettings.Default.ExportFolder = outputTextBox.Text.Trim();
            AppSettings.Default.Save();
            statusLabel.Text = "Einstellung gespeichert.";

            if (showMessage)
            {
                MessageBox.Show(this, "Export-Ordner wurde in user.config gespeichert.", "Einstellung gespeichert", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            statusLabel.Text = "Speichern fehlgeschlagen.";
            MessageBox.Show(this, exception.Message, "Fehler beim Speichern", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal sealed class AppSettings : ApplicationSettingsBase
{
    private static readonly AppSettings SettingsInstance = (AppSettings)Synchronized(new AppSettings());

    public static AppSettings Default => SettingsInstance;

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string ExportFolder
    {
        get => (string)this[nameof(ExportFolder)];
        set => this[nameof(ExportFolder)] = value;
    }
}

internal static class AutomationExporter
{
    public static int RunCli(string sourcePath, string outputPath)
    {
        try
        {
            var automations = Load(sourcePath).ToList();

            if (automations.Count == 0)
            {
                Console.Error.WriteLine("No top-level automation entries found. Expected a YAML list starting with '- ...'.");
                return 1;
            }

            var exported = Export(automations, outputPath).ToList();

            foreach (var export in exported)
            {
                Console.WriteLine($"{export.Index,3}: {export.FileName}");
            }

            Console.WriteLine();
            Console.WriteLine($"Exported {exported.Count} automation(s) to:");
            Console.WriteLine(Path.GetFullPath(outputPath));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    public static IEnumerable<AutomationEntry> Load(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("Bitte eine automations.yaml auswählen.");
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
            throw new InvalidOperationException("Bitte einen Export-Ordner auswählen.");
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

            File.WriteAllText(targetFile, automation.Yaml.TrimEnd() + Environment.NewLine, new UTF8Encoding(false));
            yield return new AutomationExport(exported, fileName, targetFile);
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

internal sealed record AutomationExport(int Index, string FileName, string TargetFile);
