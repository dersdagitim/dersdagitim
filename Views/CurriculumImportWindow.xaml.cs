using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DersDagitim.Models;
using DersDagitim.Persistence;

namespace DersDagitim.Views
{
    public partial class CurriculumImportWindow : Window
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/dersdagitim/dersdagitim/contents/dershavuzu";
        private const string RawBaseUrl = "https://github.com/dersdagitim/dersdagitim/raw/main/dershavuzu/";
        private static readonly string LocalCacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DersDagitim", "DersHavuzu");

        private readonly ObservableCollection<TemplateItem> _templates = new();
        private bool _isDownloading;

        /// <summary>
        /// True if any lessons were imported during this session
        /// </summary>
        public bool LessonsImported { get; private set; }

        public CurriculumImportWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory(LocalCacheFolder);
            TemplateListBox.ItemsSource = _templates;
            Loaded += async (_, _) => await LoadTemplateListAsync();
        }

        private async Task LoadTemplateListAsync()
        {
            LoadingPanel.Visibility = Visibility.Visible;
            ErrorPanel.Visibility = Visibility.Collapsed;
            TemplateListBox.Visibility = Visibility.Collapsed;

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DersDagitim-App");
                var json = await client.GetStringAsync(GitHubApiUrl);
                var items = JsonSerializer.Deserialize<List<GitHubFileInfo>>(json);

                _templates.Clear();
                if (items != null)
                {
                    foreach (var item in items
                        .Where(i => i.name.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(i => i.name))
                    {
                        var localPath = Path.Combine(LocalCacheFolder, item.name);
                        var cached = File.Exists(localPath);

                        _templates.Add(new TemplateItem
                        {
                            FileName = item.name,
                            DisplayName = FormatDisplayName(item.name),
                            InfoText = cached ? "Önbellekte mevcut" : "İndirilmedi",
                            LocalPath = localPath,
                            DownloadUrl = RawBaseUrl + Uri.EscapeDataString(item.name),
                            IsCached = cached,
                            StatusText = "İndir ve Aktar",
                            StatusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
                        });
                    }
                }

                LoadingPanel.Visibility = Visibility.Collapsed;
                TemplateListBox.Visibility = Visibility.Visible;
                FooterText.Text = $"{_templates.Count} okul türü şablonu bulundu — çift tıklayarak içe aktarın";
            }
            catch (Exception ex)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                ErrorText.Text = $"Şablonlar yüklenemedi:\n{ex.Message}";
            }
        }

        private async void TemplateListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TemplateListBox.SelectedItem is not TemplateItem template) return;
            if (_isDownloading) return;

            await DownloadAndImportAsync(template);
        }

        private async Task DownloadAndImportAsync(TemplateItem template)
        {
            _isDownloading = true;

            try
            {
                // Step 1: Download if not cached
                if (!template.IsCached || !File.Exists(template.LocalPath))
                {
                    template.StatusText = "İndiriliyor...";
                    template.StatusColor = Brushes.DodgerBlue;
                    template.ProgressVisibility = Visibility.Visible;
                    template.DownloadPercent = 0;

                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("DersDagitim-App");
                    using var response = await client.GetAsync(template.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(template.LocalPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    var buffer = new byte[81920];
                    long bytesRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read));
                        bytesRead += read;

                        if (totalBytes > 0)
                        {
                            var percent = (int)(bytesRead * 100 / totalBytes);
                            template.DownloadPercent = percent;
                            template.StatusText = $"İndiriliyor %{percent}";
                        }
                    }

                    template.IsCached = true;
                    template.ProgressVisibility = Visibility.Collapsed;
                }

                // Step 2: Read lessons from downloaded SQLite
                var lessons = ReadLessonsFromTemplate(template.LocalPath);
                if (lessons.Count == 0)
                {
                    template.StatusText = "Boş şablon";
                    template.StatusColor = Brushes.Orange;
                    MessageBox.Show("Bu şablonda ders bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Step 3: Ask for confirmation
                var result = MessageBox.Show(
                    $"{template.DisplayName} şablonunda {lessons.Count} ders bulundu.\n\n" +
                    $"Bu dersler ders havuzunuza eklensin mi?\n" +
                    $"(Zaten mevcut olan dersler atlanacaktır)",
                    "Dersleri İçe Aktar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    template.StatusText = "İndir ve Aktar";
                    template.StatusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));
                    return;
                }

                // Step 4: Import lessons (skip duplicates by code)
                var repo = new LessonRepository();
                var existingLessons = repo.GetAll();
                var existingCodes = new HashSet<string>(existingLessons.Select(l => l.Code), StringComparer.OrdinalIgnoreCase);

                int added = 0;
                int skipped = 0;

                foreach (var lesson in lessons)
                {
                    if (existingCodes.Contains(lesson.Code))
                    {
                        skipped++;
                        continue;
                    }

                    repo.Save(new Lesson
                    {
                        Id = 0,
                        Code = lesson.Code,
                        Name = lesson.Name,
                        DefaultBlock = lesson.DefaultBlock,
                        MorningPriority = lesson.MorningPriority
                    });
                    existingCodes.Add(lesson.Code);
                    added++;
                }

                // Step 5: Show result
                template.StatusText = "Aktarıldı ✓";
                template.StatusColor = Brushes.Green;
                template.InfoText = $"{added} ders eklendi, {skipped} atlandı";
                LessonsImported = added > 0;

                MessageBox.Show(
                    $"{added} ders başarıyla ders havuzuna eklendi.\n{skipped} ders zaten mevcut olduğu için atlandı.",
                    "İçe Aktarma Tamamlandı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                template.StatusText = "Hata!";
                template.StatusColor = Brushes.Red;
                template.ProgressVisibility = Visibility.Collapsed;

                MessageBox.Show($"Şablon aktarılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isDownloading = false;
            }
        }

        /// <summary>
        /// Reads lesson records from a template SQLite database
        /// </summary>
        private static List<TemplateLessonData> ReadLessonsFromTemplate(string dbPath)
        {
            var lessons = new List<TemplateLessonData>();

            try
            {
                using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT kod, ad, varsayilan_blok, sabah_onceligi FROM ders ORDER BY id";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    lessons.Add(new TemplateLessonData
                    {
                        Code = reader.GetString(0),
                        Name = reader.GetString(1),
                        DefaultBlock = reader.IsDBNull(2) ? "2" : reader.GetString(2),
                        MorningPriority = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon veritabanı okunamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return lessons;
        }

        private async void Retry_Click(object sender, RoutedEventArgs e)
        {
            await LoadTemplateListAsync();
        }

        private static string FormatDisplayName(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            name = name.Replace('_', ' ').Replace('-', ' ');

            // Capitalize each word
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                    words[i] = char.ToUpper(words[i][0]) + words[i][1..];
            }
            return string.Join(" ", words);
        }
    }

    public class TemplateItem : INotifyPropertyChanged
    {
        public string FileName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string LocalPath { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public bool IsCached { get; set; }

        private string _infoText = "";
        public string InfoText
        {
            get => _infoText;
            set { _infoText = value; OnPropertyChanged(); }
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private Brush _statusColor = Brushes.Gray;
        public Brush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        private Visibility _progressVisibility = Visibility.Collapsed;
        public Visibility ProgressVisibility
        {
            get => _progressVisibility;
            set { _progressVisibility = value; OnPropertyChanged(); }
        }

        private int _downloadPercent;
        public int DownloadPercent
        {
            get => _downloadPercent;
            set { _downloadPercent = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TemplateLessonData
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string DefaultBlock { get; set; } = "2";
        public int MorningPriority { get; set; }
    }
}
