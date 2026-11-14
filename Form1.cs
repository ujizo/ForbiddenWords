
using System.Drawing;

namespace ForbiddenWords;

public class Form1 : Form
{
    TextBox txtWords = new();
    TextBox txtSource = new();
    TextBox txtDestination = new();

    Button btnLoad = new();
    Button btnSource = new();
    Button btnDestination = new();

    Button btnStart = new();
    Button btnPause = new();
    Button btnResume = new();
    Button btnStop = new();

    ProgressBar progressTotal = new();
    ProgressBar progressFiles = new();

    Label lblStatus = new();
    Label lblChecked = new();
    Label lblFound = new();
    Label lblCurrent = new();

    DataGridView grid = new();
    ListBox lstTop = new();

    CancellationTokenSource? cts;
    ManualResetEventSlim pause = new(true);

    public Form1()
    {
        Text = "Forbidden Words";
        Size = new Size(1100, 760);
        MinimumSize = new Size(900, 650);
        StartPosition = FormStartPosition.CenterScreen;

        CreateInterface();
    }

    void AddLabel(string text, int x, int y)
    {
        Controls.Add(new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true
        });
    }

    void AddButton(
        Button button,
        string text,
        int x,
        int y,
        int width,
        EventHandler handler)
    {
        button.Text = text;
        button.Location = new Point(x, y);
        button.Size = new Size(width, 35);
        button.Click += handler;
        Controls.Add(button);
    }

    void CreateInterface()
    {
        AddLabel("Запрещённые слова:", 20, 15);

        txtWords.Location = new Point(20, 40);
        txtWords.Size = new Size(500, 90);
        txtWords.Multiline = true;
        txtWords.ScrollBars = ScrollBars.Vertical;
        Controls.Add(txtWords);

        AddButton(btnLoad, "Загрузить слова", 540, 40, 140, LoadWords);

        AddLabel("Папка поиска:", 20, 145);

        txtSource.Location = new Point(20, 170);
        txtSource.Size = new Size(500, 25);
        Controls.Add(txtSource);

        AddButton(btnSource, "Обзор", 540, 165, 130, BrowseSource);

        AddLabel("Папка сохранения:", 20, 210);

        txtDestination.Location = new Point(20, 235);
        txtDestination.Size = new Size(500, 25);
        Controls.Add(txtDestination);

        AddButton(
            btnDestination,
            "Обзор",
            540,
            230,
            130,
            BrowseDestination
        );

        AddButton(btnStart, "Старт", 20, 280, 100, StartScan);
        AddButton(btnPause, "Пауза", 130, 280, 100, PauseScan);
        AddButton(btnResume, "Продолжить", 240, 280, 120, ResumeScan);
        AddButton(btnStop, "Стоп", 370, 280, 100, StopScan);

        AddLabel("Статус:", 20, 330);

        lblStatus.Text = "Ожидание";
        lblStatus.Location = new Point(90, 330);
        lblStatus.AutoSize = true;
        Controls.Add(lblStatus);

        AddLabel("Общий прогресс:", 20, 365);

        progressTotal.Location = new Point(150, 360);
        progressTotal.Size = new Size(500, 25);
        Controls.Add(progressTotal);

        AddLabel("Обработка:", 20, 405);

        progressFiles.Location = new Point(150, 400);
        progressFiles.Size = new Size(500, 25);
        Controls.Add(progressFiles);

        lblChecked.Text = "Проверено: 0";
        lblChecked.Location = new Point(680, 365);
        lblChecked.AutoSize = true;
        Controls.Add(lblChecked);

        lblFound.Text = "Найдено: 0";
        lblFound.Location = new Point(680, 400);
        lblFound.AutoSize = true;
        Controls.Add(lblFound);

        lblCurrent.Text = "Текущий файл:";
        lblCurrent.Location = new Point(20, 445);
        lblCurrent.Size = new Size(1000, 25);
        Controls.Add(lblCurrent);

        grid.Location = new Point(20, 480);
        grid.Size = new Size(700, 210);
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        grid.Columns.Add("Path", "Файл");
        grid.Columns.Add("Size", "Размер");
        grid.Columns.Add("Count", "Количество замен");

        Controls.Add(grid);

        AddLabel("Топ-10 слов:", 750, 480);

        lstTop.Location = new Point(750, 505);
        lstTop.Size = new Size(300, 185);
        Controls.Add(lstTop);

        SetButtons(false);
    }

    void SetButtons(bool running)
    {
        btnStart.Enabled = !running;
        btnPause.Enabled = running;
        btnResume.Enabled = running;
        btnStop.Enabled = running;
    }

    void LoadWords(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "Текстовые файлы (*.txt)|*.txt"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
            txtWords.Text = File.ReadAllText(dialog.FileName);
    }

    void BrowseSource(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new();

        if (dialog.ShowDialog() == DialogResult.OK)
            txtSource.Text = dialog.SelectedPath;
    }

    void BrowseDestination(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new();

        if (dialog.ShowDialog() == DialogResult.OK)
            txtDestination.Text = dialog.SelectedPath;
    }

    async void StartScan(object? sender, EventArgs e)
    {
        if (!Directory.Exists(txtSource.Text))
        {
            MessageBox.Show("Выберите папку поиска.");
            return;
        }

        if (string.IsNullOrWhiteSpace(txtDestination.Text))
        {
            MessageBox.Show("Выберите папку сохранения.");
            return;
        }

        if (string.IsNullOrWhiteSpace(txtWords.Text))
        {
            MessageBox.Show("Введите запрещённые слова.");
            return;
        }

        string sourcePath = Path.GetFullPath(txtSource.Text);
        string destinationPath = Path.GetFullPath(txtDestination.Text);

        string sourcePrefix = sourcePath.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        string destinationPrefix = destinationPath.TrimEnd(
            Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (sourcePath.Equals(destinationPath,
            StringComparison.OrdinalIgnoreCase)
            || sourcePrefix.StartsWith(destinationPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "Папка сохранения не должна совпадать с папкой поиска или находиться внутри неё."
            );
            return;
        }

        string[] forbiddenWords = txtWords.Text.Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries
        );

        cts = new CancellationTokenSource();
        pause.Set();

        grid.Rows.Clear();
        lstTop.Items.Clear();

        progressTotal.Value = 0;
        progressFiles.Value = 0;

        lblChecked.Text = "Проверено: 0";
        lblFound.Text = "Найдено: 0";
        lblStatus.Text = "Выполняется поиск";

        SetButtons(true);

        var progress = new Progress<ScanProgress>(p =>
        {
            int value = p.Total == 0
                ? 0
                : p.Done * 100 / p.Total;

            progressTotal.Value = Math.Min(100, value);
            progressFiles.Value = Math.Min(100, value);

            lblChecked.Text = $"Проверено: {p.Done} / {p.Total}";
            lblFound.Text = $"Найдено: {p.Found}";
            lblCurrent.Text = "Текущий файл: " + p.CurrentFile;
        });

        try
        {
            ScanResult result = await Scanner.RunAsync(
                sourcePath,
                destinationPath,
                forbiddenWords,
                progress,
                pause,
                cts.Token
            );

            foreach (FoundFile file in result.Files.OrderBy(
                x => x.OriginalPath))
            {
                grid.Rows.Add(
                    file.OriginalPath,
                    file.Size,
                    file.Replacements
                );
            }

            foreach (var word in result.WordCounts
                .OrderByDescending(x => x.Value)
                .Take(10))
            {
                lstTop.Items.Add($"{word.Key} - {word.Value}");
            }

            progressTotal.Value = 100;
            progressFiles.Value = 100;

            lblStatus.Text = "Работа завершена";

            MessageBox.Show(
                "Поиск завершён.\nОтчёт сохранён в Report.txt."
            );
        }
        catch (OperationCanceledException)
        {
            lblStatus.Text = "Работа остановлена";
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Ошибка";
            MessageBox.Show(ex.Message);
        }
        finally
        {
            pause.Set();
            cts.Dispose();
            cts = null;
            SetButtons(false);
        }
    }

    void PauseScan(object? sender, EventArgs e)
    {
        pause.Reset();
        lblStatus.Text = "Пауза";
    }

    void ResumeScan(object? sender, EventArgs e)
    {
        pause.Set();
        lblStatus.Text = "Выполняется поиск";
    }

    void StopScan(object? sender, EventArgs e)
    {
        pause.Set();
        cts?.Cancel();
        lblStatus.Text = "Остановка...";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (cts != null)
        {
            pause.Set();
            cts.Cancel();
        }

        pause.Dispose();

        base.OnFormClosing(e);
    }
}
