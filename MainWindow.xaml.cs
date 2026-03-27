using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using DersDagitim.Models;
using DersDagitim.Persistence;
using DersDagitim.Services;
using DersDagitim.Views;

namespace DersDagitim;

// Row model for timetable DataGrid
public class TimetableRow : INotifyPropertyChanged
{
    public int Hour { get; set; }
    public string TimeRange { get; set; } = "";
    
    private bool[] _dayStates = new bool[7]; // true = Open, false = Closed
    
    public bool Day1Open { get => _dayStates[0]; set { _dayStates[0] = value; OnPropertyChanged(nameof(Day1Text)); OnPropertyChanged(nameof(Day1Color)); } }
    public bool Day2Open { get => _dayStates[1]; set { _dayStates[1] = value; OnPropertyChanged(nameof(Day2Text)); OnPropertyChanged(nameof(Day2Color)); } }
    public bool Day3Open { get => _dayStates[2]; set { _dayStates[2] = value; OnPropertyChanged(nameof(Day3Text)); OnPropertyChanged(nameof(Day3Color)); } }
    public bool Day4Open { get => _dayStates[3]; set { _dayStates[3] = value; OnPropertyChanged(nameof(Day4Text)); OnPropertyChanged(nameof(Day4Color)); } }
    public bool Day5Open { get => _dayStates[4]; set { _dayStates[4] = value; OnPropertyChanged(nameof(Day5Text)); OnPropertyChanged(nameof(Day5Color)); } }
    public bool Day6Open { get => _dayStates[5]; set { _dayStates[5] = value; OnPropertyChanged(nameof(Day6Text)); OnPropertyChanged(nameof(Day6Color)); } }
    public bool Day7Open { get => _dayStates[6]; set { _dayStates[6] = value; OnPropertyChanged(nameof(Day7Text)); OnPropertyChanged(nameof(Day7Color)); } }
    
    public string Day1Text => Day1Open ? "A" : "K";
    public string Day2Text => Day2Open ? "A" : "K";
    public string Day3Text => Day3Open ? "A" : "K";
    public string Day4Text => Day4Open ? "A" : "K";
    public string Day5Text => Day5Open ? "A" : "K";
    public string Day6Text => Day6Open ? "A" : "K";
    public string Day7Text => Day7Open ? "A" : "K";
    
    private static readonly Brush OpenBrush;
    private static readonly Brush ClosedBrush;
    private static readonly Brush WeekendBrush;

    static TimetableRow()
    {
        OpenBrush = new SolidColorBrush(Color.FromRgb(220, 252, 231)); OpenBrush.Freeze();
        ClosedBrush = new SolidColorBrush(Color.FromRgb(243, 244, 246)); ClosedBrush.Freeze();
        WeekendBrush = new SolidColorBrush(Color.FromRgb(255, 237, 213)); WeekendBrush.Freeze();
    }

    public Brush Day1Color => Day1Open ? OpenBrush : ClosedBrush;
    public Brush Day2Color => Day2Open ? OpenBrush : ClosedBrush;
    public Brush Day3Color => Day3Open ? OpenBrush : ClosedBrush;
    public Brush Day4Color => Day4Open ? OpenBrush : ClosedBrush;
    public Brush Day5Color => Day5Open ? OpenBrush : ClosedBrush;
    public Brush Day6Color => Day6Open ? WeekendBrush : ClosedBrush;
    public Brush Day7Color => Day7Open ? WeekendBrush : ClosedBrush;
    
    public bool GetDayState(int day) => _dayStates[day - 1];
    public void SetDayState(int day, bool value)
    {
        _dayStates[day - 1] = value;
        OnPropertyChanged($"Day{day}Open");
        OnPropertyChanged($"Day{day}Text");
        OnPropertyChanged($"Day{day}Color");
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class MainWindow : Window
{
    private SchoolInfo? _schoolInfo;
    private ObservableCollection<TimetableRow> _timetableRows = new();
    private List<OrtakMekan> _allOrtakMekans = new();
    
    public MainWindow()
    {
        InitializeComponent();
        ThemeManager.Initialize();
        Loaded += (s, e) => Initialize();
    }

    private void Initialize()
    {

        var dbPath = ConfigManager.Shared.GetActiveDatabase();

        // SAFETY CHECK: Never use sabit.sqlite as data db
        if (!string.IsNullOrEmpty(dbPath) && dbPath.EndsWith("sabit.sqlite", StringComparison.OrdinalIgnoreCase))
        {
            dbPath = null;
        }

        // --- SMART DB RECOVERY ---
        if (string.IsNullOrEmpty(dbPath) || !System.IO.File.Exists(dbPath))
        {
             // Eğer ayarlı veritabanı yoksa, data klasöründeki ilk sqlite dosyasını dene
             var dataDir = ConfigManager.Shared.DataDirectory;
             // Try common names first
             var preferred = new[] { "ders_dagitim.sqlite", "backup_restored.sqlite", "backup_db.sqlite", "demo.sqlite" };

             foreach(var p in preferred)
             {
                 var path = System.IO.Path.Combine(dataDir, p);
                 if (System.IO.File.Exists(path)) { dbPath = path; break; }
             }

             // Still not found? Any sqlite in data dir
             if (!System.IO.File.Exists(dbPath))
             {
                 var candidates = System.IO.Directory.GetFiles(dataDir, "*.sqlite")
                     .Where(f => !System.IO.Path.GetFileName(f).Equals("sabit.sqlite", StringComparison.OrdinalIgnoreCase))
                     .ToArray();
                 if (candidates.Length > 0) dbPath = candidates[0];
             }
        }
        // -------------------------

        if (!DatabaseManager.Shared.OpenDatabase(dbPath))
        {
            MessageBox.Show($"Veritabanı açılamadı ve yedek bulunamadı:\nHedef: {dbPath}\n\nLütfen 'Veritabanı Aç' menüsünü kullanın.", "Veritabanı Hatası", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            // Don't return, let the UI load empty so user can use File menu
        }
        
        DatabaseSchema.Initialize(DatabaseManager.Shared);
        DatabaseNameLabel.Text = !string.IsNullOrEmpty(dbPath) ? System.IO.Path.GetFileNameWithoutExtension(dbPath) : "Veritabanı Yok";
        
        CheckLicense();
        
        // Load Settings (Must load before password check to know if active)
        LoadSettingsData();
        CheckHideWeekend.IsChecked = SettingsManager.GetBool("TeacherHideWeekend", false);
        CheckClassHideWeekend.IsChecked = SettingsManager.GetBool("ClassHideWeekend", false);
        CheckDistHideWeekend.IsChecked = SettingsManager.GetBool("DistHideWeekend", false);

        // Security Check
        bool isPassActive = SettingsManager.GetBool("AppPasswordActive", false);
        if (isPassActive)
        {
            var currentPw = SettingsManager.Get("AppPassword", "12345");
            var login = new LoginWindow(currentPw);
            if (login.ShowDialog() != true)
            {
                Application.Current.Shutdown();
                return;
            }
        }
    }

    private bool _isSummerPeriod;

    private async void CheckLicense()
    {
        _isSummerPeriod = await Services.InternetTimeService.IsSummerPeriodAsync();
        var trustedDate = await Services.InternetTimeService.GetTrustedDateAsync();
        var result = Services.LicenseManager.Shared.ValidateLicense(_isSummerPeriod, trustedDate);
        
        // 1. Auto-restore from backup if missing in current database
        if (result.Status == Models.LicenseStatus.Missing)
        {
            // A) Check for demo.sqlite fallback
            var demoPath = System.IO.Path.Combine(ConfigManager.Shared.DataDirectory, "demo.sqlite");
            
            // If we are NOT currently using demo.sqlite, but it exists, switch to it!
            var currentDb = ConfigManager.Shared.GetActiveDatabase();
            bool isUsingDemo = !string.IsNullOrEmpty(currentDb) && System.IO.Path.GetFileName(currentDb).Equals("demo.sqlite", StringComparison.OrdinalIgnoreCase);

            if (!isUsingDemo && System.IO.File.Exists(demoPath))
            {
                DatabaseManager.Shared.CloseDatabase();
                ConfigManager.Shared.SetActiveDatabase(demoPath);
                DatabaseManager.Shared.OpenDatabase(demoPath);
                DatabaseSchema.Initialize(DatabaseManager.Shared);
                DatabaseNameLabel.Text = "demo";
                result = Services.LicenseManager.Shared.ValidateLicense(_isSummerPeriod, trustedDate);
                // Continue with normal license check below (no recursion)
            }

            // B) Normal Backup Restore Logic
            var backupCode = ConfigManager.Shared.GetBackupLicense();
            if (!string.IsNullOrEmpty(backupCode))
            {
                // Install back to current active database
                if (Services.LicenseManager.Shared.InstallLicense(backupCode))
                {
                    result = Services.LicenseManager.Shared.ValidateLicense(_isSummerPeriod, trustedDate);
                }
            }
        }

        // 2. If valid, enforce school name from license to the database
        if (result.Status == Models.LicenseStatus.Valid && !string.IsNullOrEmpty(result.SchoolName))
        {
            var repo = new Persistence.SchoolRepository();
            repo.UpdateSchoolName(result.SchoolName);
            
            // Sync local info if loaded
            if (_schoolInfo != null) _schoolInfo.Name = result.SchoolName;
        }

        if (result.Status == Models.LicenseStatus.Valid)
        {
            LicenseOverlay.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            LoadDashboardData();

            // Check if DEMO and warn user (skip warning during summer period)
            if (!_isSummerPeriod && (result.LicensedMac == "DEMO-USER" || (result.SchoolName != null && result.SchoolName.StartsWith("DEMO"))))
            {
                 MessageBox.Show("DEMO modunda kullanıyorsunuz. Yakın zamanda kullanım hakkınız sonlanacaktır.",
                                 "Demo Sürüm Uyarısı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            LicenseOverlay.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
            RequestCodeBox.Text = Services.LicenseManager.Shared.GetRequestCode();
            
            LicenseStatusText.Text = result.Status switch
            {
                Models.LicenseStatus.Missing => "Lisans bulunamadı",
                Models.LicenseStatus.Invalid => $"Geçersiz lisans: {result.Error}",
                Models.LicenseStatus.Expired => "Lisans süresi dolmuş",
                _ => ""
            };
        }
    }
    
    private async void LoadDashboardData()
    {
        try
        {
            // Heavy DB queries on background thread
            var data = await Task.Run(() =>
            {
                var teacherRepo = new TeacherRepository();
                var teachers = teacherRepo.GetAll();
                int classCount = new ClassRepository().GetAll().Count;
                int lessonCount = new LessonRepository().GetAll().Count;

                var distRepo = new DistributionRepository();
                var allBlocks = distRepo.GetAllBlocks();
                var placedBlocks = allBlocks.Where(b => b.Day > 0 && b.Hour > 0).ToList();

                int totalGaps = 0;
                var teacherGaps = new List<(Teacher Teacher, int Gap)>();

                if (allBlocks.Count > 0)
                {
                    foreach (var teacher in teachers)
                    {
                        var tBlocks = placedBlocks.Where(b => b.TeacherIds.Contains(teacher.Id)).ToList();
                        if (tBlocks.Count > 0)
                        {
                            int tGap = CalculateSingleTeacherGap(tBlocks);
                            if (tGap > 0)
                            {
                                totalGaps += tGap;
                                teacherGaps.Add((teacher, tGap));
                            }
                        }
                    }
                }

                return new
                {
                    Teachers = teachers,
                    TeacherCount = teachers.Count,
                    ClassCount = classCount,
                    LessonCount = lessonCount,
                    AllBlockCount = allBlocks.Count,
                    PlacedBlockCount = placedBlocks.Count,
                    TotalGaps = totalGaps,
                    TeacherGaps = teacherGaps
                };
            });

            // UI updates on main thread
            TeacherCount.Text = data.TeacherCount.ToString();
            ClassCount.Text = data.ClassCount.ToString();
            LessonCount.Text = data.LessonCount.ToString();

            if (data.AllBlockCount > 0)
            {
                int percent = (int)((double)data.PlacedBlockCount / data.AllBlockCount * 100);
                PlacementPercent.Text = $"%{percent}";
                TotalGaps.Text = data.TotalGaps.ToString();
                LoadGapTeachersPanel(data.TeacherGaps);
            }
            else
            {
                PlacementPercent.Text = "%0";
                TotalGaps.Text = "0";
                GapTeachersPanel.Children.Clear();
            }

            LoadDailyDuties(data.Teachers);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Hata: {ex.Message}";
        }
    }

    // ==================== License Events ====================
    
    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(RequestCodeBox.Text);
        MessageBox.Show("İstek kodu panoya kopyalandı!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    
    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        var code = LicenseCodeInput.Text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            LicenseStatusText.Text = "Lütfen aktivasyon kodunu giriniz";
            return;
        }
        
        var schoolName = SchoolNameInput.Text.Trim();
        if (!string.IsNullOrEmpty(schoolName))
        {
            new SchoolRepository().UpdateSchoolName(schoolName);
        }
        
        if (Services.LicenseManager.Shared.InstallLicense(code))
        {
            CheckLicense();
        }
        else
        {
            LicenseStatusText.Text = "Geçersiz aktivasyon kodu";
        }
    }
    
    // ==================== Database Selection ====================
    
    private void SelectDatabase_Click(object sender, RoutedEventArgs e)
    {
        var window = new DatabaseSelectorWindow();
        window.Owner = this;
        
        if (window.ShowDialog() == true && !string.IsNullOrEmpty(window.SelectedDatabase))
        {
            DatabaseManager.Shared.CloseDatabase();
            var newPath = ConfigManager.Shared.GetDatabasePath(window.SelectedDatabase);
            ConfigManager.Shared.SetActiveDatabase(newPath);
            Initialize();
        }
    }
    
    // ==================== Navigation ====================
    
    private void ShowPanel(string panel)
    {
        DashboardPanel.Visibility = panel == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        if (panel == "Dashboard") DashboardPanel.ScrollToTop();

        SettingsPanel.Visibility = panel == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        if (panel == "Settings") SettingsPanel.ScrollToTop();

        LessonsPanel.Visibility = panel == "Lessons" ? Visibility.Visible : Visibility.Collapsed;
        if (panel == "Lessons") LessonsPanel.ScrollToTop();

        TeachersPanel.Visibility = panel == "Teachers" ? Visibility.Visible : Visibility.Collapsed;
        DistributionPanel.Visibility = panel == "Distribution" ? Visibility.Visible : Visibility.Collapsed;
        ClassesPanel.Visibility = panel == "Classes" ? Visibility.Visible : Visibility.Collapsed;
        
        ManualDistributionPanel.Visibility = panel == "ManualDistribution" ? Visibility.Visible : Visibility.Collapsed;
        ValidationPanel.Visibility = panel == "Validation" ? Visibility.Visible : Visibility.Collapsed;
        ReportsPanel.Visibility = panel == "Reports" ? Visibility.Visible : Visibility.Collapsed;
        if (panel == "Reports") ReportsScrollViewer.ScrollToTop();
    }
    
    private void OpenTeacherAssignment_Click(object sender, RoutedEventArgs e)
    {
        var win = new Views.TeacherAssignmentWindow();
        win.Owner = this;
        win.ShowDialog();
        // Refresh data after window closes
        LoadClassesPanel();
        LoadTeachers();
    }

    private void OpenCombinedLessons_Click(object sender, RoutedEventArgs e)
    {
        var win = new Views.CombinedLessonsWindow();
        win.Owner = this;
        win.ShowDialog();
    }
    
    private void SetActiveNavButton(Button activeBtn)
    {
        var navButtons = new[] { NavBtnDashboard, NavBtnSettings, NavBtnTeachers,
                                  NavBtnClasses, NavBtnScheduling, /*NavBtnValidation,*/ NavBtnReports };
        
        foreach (var btn in navButtons)
        {
            if (btn == null) continue;
            
            if (btn == activeBtn)
            {
                // Classic Active State
                btn.Background = System.Windows.Media.Brushes.LightGray; 
                btn.Foreground = System.Windows.Media.Brushes.Black;
                btn.FontWeight = FontWeights.Bold;
            }
            else
            {
                // Classic Inactive State
                btn.Background = System.Windows.Media.Brushes.Transparent;
                btn.Foreground = System.Windows.Media.Brushes.DarkGray;
                btn.FontWeight = FontWeights.Normal;
            }
        }
    }
    
    private void Nav_Dashboard(object sender, RoutedEventArgs e) 
    { 
        ShowPanel("Dashboard"); 
        SetActiveNavButton(NavBtnDashboard);
        LoadDashboardData(); 
    }
    private void Nav_Settings(object sender, RoutedEventArgs e) 
    { 
        ShowPanel("Settings"); 
        SetActiveNavButton(NavBtnSettings);
        LoadSettingsData(); 
        LoadLicenseInfo(); 
    }
    private void Nav_Lessons(object sender, RoutedEventArgs e) 
    { 
        ShowPanel("Lessons"); 
        SetActiveNavButton(null);
        
        LoadLessons();
    }

    private void OpenLessonsFromSettings(object sender, RoutedEventArgs e)
    {
        Nav_Lessons(null, e);
    }

    private void OpenCurriculumImport_Click(object sender, RoutedEventArgs e)
    {
        var window = new Views.CurriculumImportWindow();
        window.Owner = this;
        window.ShowDialog();
        if (window.LessonsImported)
            LoadLessons(LessonSearchBox.Text);
    }
    private void Nav_Teachers(object sender, RoutedEventArgs e) 
    { 
        ShowPanel("Teachers"); 
        SetActiveNavButton(NavBtnTeachers);
        LoadTeachers(); 
    }
    private void Nav_Classes(object sender, RoutedEventArgs e) 
    { 
        ShowPanel("Classes"); 
        SetActiveNavButton(NavBtnClasses);
        LoadClassesPanel(); 
    }
    private void Nav_Scheduling(object sender, RoutedEventArgs e) 
    { 
        ShowPanel("Distribution"); 
        SetActiveNavButton(NavBtnScheduling);
        LoadDistributionCombos(); 
        UpdateStats();

        // Auto-select first items if nothing selected
        if (DistTeacherCombo.Items.Count > 0 && DistTeacherCombo.SelectedIndex == -1)
        {
            DistTeacherCombo.SelectedIndex = 0;
        }

        if (DistClassCombo.Items.Count > 0 && DistClassCombo.SelectedIndex == -1)
        {
            DistClassCombo.SelectedIndex = 0;
        }
    }

    private void Nav_Validation(object sender, RoutedEventArgs e) 
    { 
        ShowPanel("Validation"); 
    }




    private void Nav_Reports(object sender, RoutedEventArgs e) 
    { 
        ShowPanel("Reports"); 
        SetActiveNavButton(NavBtnReports);
    }
    

    // Dashboard Card Click Handlers for Navigation
    private void Dashboard_TeachersCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Nav_Teachers(sender, new RoutedEventArgs());
    }
    
    private void Dashboard_ClassesCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Nav_Classes(sender, new RoutedEventArgs());
    }
    
    private void Dashboard_LessonsCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Nav_Lessons(sender, new RoutedEventArgs());
    }
    
    private void Dashboard_DistributionCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Nav_Scheduling(sender, new RoutedEventArgs());
    }


    private int CalculateSingleTeacherGap(List<DistributionBlock> blocks)
    {
        int gap = 0;
        var days = blocks.GroupBy(b => b.Day);
        
        foreach(var dayGroup in days)
        {
            var filledHours = new HashSet<int>();
            foreach(var b in dayGroup)
            {
                for(int i=0; i<b.BlockDuration; i++) filledHours.Add(b.Hour + i);
            }
            
            var sorted = filledHours.OrderBy(h => h).ToList();
            if (sorted.Count > 1)
            {
                for(int i=0; i < sorted.Count - 1; i++)
                {
                    gap += (sorted[i+1] - sorted[i] - 1);
                }
            }
        }
        return gap;
    }
    
    private void LoadGapTeachersPanel(List<(Teacher Teacher, int Gap)> teacherGaps)
    {
        GapTeachersPanel.Children.Clear();
        var topGapTeachers = teacherGaps.OrderByDescending(x => x.Gap).Take(5).ToList();
        
        foreach(var item in topGapTeachers)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            
            // Avatar Circle
            var border = new Border 
            { 
                Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)), // red-100
                Margin = new Thickness(0, 0, 10, 0)
            };
            border.Child = new TextBlock 
            { 
                Text = item.Teacher.Initial, 
                Foreground = Brushes.Red, FontWeight = FontWeights.Bold, 
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize=10
            };
            
            sp.Children.Add(border);
            
            // Name
            sp.Children.Add(new TextBlock 
            { 
                Text = item.Teacher.Name, 
                Width = 150,
                VerticalAlignment = VerticalAlignment.Center
            });
            
            // Gap Badge
            var badge = new Border
            {
                Background = ThemeManager.GetBrush("ErrorLightBrush"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2)
            };
            badge.Child = new TextBlock 
            { 
                Text = $"{item.Gap} Saat", 
                Foreground = Brushes.Red, FontWeight = FontWeights.Bold, FontSize = 10 
            };
            
            sp.Children.Add(badge);
            GapTeachersPanel.Children.Add(sp);
        }
        
        if (topGapTeachers.Count == 0)
        {
            GapTeachersPanel.Children.Add(new TextBlock { Text = "Boşluk sorunu yok.", Foreground = Brushes.Gray, FontStyle = FontStyles.Italic });
        }
    }
    
    private void LoadDailyDuties(List<Teacher> teachers)
    {
        var dayPanels = new[] { MondayDutiesPanel, TuesdayDutiesPanel, WednesdayDutiesPanel, ThursdayDutiesPanel, FridayDutiesPanel };
        var dayCounts = new[] { MondayDutyCount, TuesdayDutyCount, WednesdayDutyCount, ThursdayDutyCount, FridayDutyCount };
        var activeCounts = new[] { MondayActiveCount, TuesdayActiveCount, WednesdayActiveCount, ThursdayActiveCount, FridayActiveCount };
        string[] dayNames = { "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma" };
        
        // Day indices for schedule check (typically 0=Monday, 4=Friday in logic, but keys might vary. 
        // Assuming slots are 1-based day index: 1.. for Mon?? 
        // Usually, the app logic uses day indices 1..5 for Mon..Fri or 0..4? 
        // Based on ReportGenerator logic: d = 1 to 5.
        // Slots are d_l (day_lesson). e.g. 1_1, 1_2 ... 
        
        for (int i = 0; i < 5; i++)
        {
            dayPanels[i].Children.Clear();
            var dayName = dayNames[i];
            
            // 1. Calculate Duty Count
            var dayTeachers = teachers.Where(t => 
                !string.IsNullOrEmpty(t.DutyDay) && 
                t.DutyDay.Trim().Equals(dayName, StringComparison.OrdinalIgnoreCase)).ToList();
                
            dayCounts[i].Text = dayTeachers.Count.ToString();

            // 2. Calculate Active Teacher Count (Teachers who have ANY lesson on this day)
            // Day index for schedule checking: i + 1 (1=Mon, 2=Tue...)
            int dayIndex = i + 1;
            int activeTeacherCount = 0;
            
            foreach(var t in teachers)
            {
                bool hasLesson = false;
                if (t.ScheduleInfo != null)
                {
                    // Check slots for this day (1..10 or 1..DailyLessonCount). Checking 1..15 to be safe.
                    for (int l = 1; l <= 15; l++)
                    {
                        var key = new TimeSlot(dayIndex, l);
                        if (t.ScheduleInfo.ContainsKey(key))
                        {
                            var content = t.ScheduleInfo[key];
                            // Check if content is not empty and not "Kapalı"
                            if (!string.IsNullOrEmpty(content) && 
                                content.IndexOf("Kapalı", StringComparison.OrdinalIgnoreCase) < 0 && 
                                content.IndexOf("KAPALI", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                hasLesson = true;
                                break;
                            }
                        }
                    }
                }
                if (hasLesson) activeTeacherCount++;
            }
            
            if (activeCounts[i] != null)
                activeCounts[i].Text = activeTeacherCount.ToString();
            
            // 3. Populate Duty List
            var grouped = dayTeachers.GroupBy(t => string.IsNullOrEmpty(t.DutyLocation) ? "Genel" : t.DutyLocation);
            foreach (var group in grouped)
            {
                var locHeader = new TextBlock
                {
                    Text = $"• {group.Key}",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeManager.GetBrush("PrimaryBrush"),
                    FontSize = 11,
                    Margin = new Thickness(0, 5, 0, 2)
                };
                dayPanels[i].Children.Add(locHeader);
                
                foreach (var t in group)
                {
                    dayPanels[i].Children.Add(new TextBlock
                    {
                        Text = $"  • {t.Name}",
                        FontSize = 10,
                        Foreground = ThemeManager.GetBrush("TextSecondaryBrush")
                    });
                }
            }
        }
    }
    
    private void LoadLicenseInfo()
    {
        try
        {
            var licenseManager = Services.LicenseManager.Shared;
            bool isDemo = licenseManager.IsDemoLicense();

            if (isDemo)
            {
                LicenseExpiryText.Text = "DEMO";
                LicenseExpiryText.Foreground = new SolidColorBrush(Color.FromRgb(234, 179, 8));
                LicenseDaysLeftText.Text = "-";
                LicenseActionButton.Content = "Lisans Al";
            }
            else
            {
                var expiryDate = licenseManager.GetExpiryDate();
                if (expiryDate.HasValue)
                {
                    LicenseExpiryText.Text = expiryDate.Value.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));
                    LicenseExpiryText.Foreground = new SolidColorBrush(Color.FromRgb(34, 34, 34));
                    int daysLeft = (expiryDate.Value - DateTime.Now).Days;

                    if (daysLeft > 0)
                    {
                        LicenseDaysLeftText.Text = $"{daysLeft} gün kaldı";
                        LicenseDaysLeftText.Foreground = daysLeft < 30 ? Brushes.Red : new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    }
                    else
                    {
                        LicenseDaysLeftText.Text = "Süresi dolmuş";
                        LicenseDaysLeftText.Foreground = Brushes.Red;
                    }
                }
                else
                {
                    LicenseExpiryText.Text = "Lisans bilgisi bulunamadı";
                    LicenseDaysLeftText.Text = "-";
                }
                LicenseActionButton.Content = "Lisans Yenile";
            }
        }
        catch
        {
            LicenseExpiryText.Text = "Lisans bilgisi bulunamadı";
            LicenseDaysLeftText.Text = "-";
            LicenseActionButton.Content = "Lisans Al";
        }
    }

    private void LicenseAction_Click(object sender, RoutedEventArgs e)
    {
        LicenseOverlay.Visibility = Visibility.Visible;
        MainContent.Visibility = Visibility.Collapsed;
        RequestCodeBox.Text = Services.LicenseManager.Shared.GetRequestCode();
        LicenseStatusText.Text = "";
        LicenseCloseButton.Visibility = Visibility.Visible;
    }

    private void LicenseClose_Click(object sender, RoutedEventArgs e)
    {
        LicenseOverlay.Visibility = Visibility.Collapsed;
        MainContent.Visibility = Visibility.Visible;
        LicenseCloseButton.Visibility = Visibility.Collapsed;
    }
    
    // ==================== Settings Screen ====================
    
    private void LoadSettingsData()
    {
        try
        {
            var repo = new SchoolRepository();
            _schoolInfo = repo.GetSchoolInfo();
            
            SettingsSchoolName.Text = _schoolInfo.Name ?? "";
            SettingsPrincipal.Text = _schoolInfo.Principal ?? "";
            SettingsStartDate.Text = _schoolInfo.Date ?? "";
            

            
            CheckEnableLoginPassword.IsChecked = SettingsManager.GetBool("AppPasswordActive", false);
            
            // Update version info
            AppVersionText.Text = _schoolInfo.Version ?? "1.0.0.0";
            LastCheckDateText.Text = string.IsNullOrEmpty(_schoolInfo.LastUpdateDate) ? "-" : _schoolInfo.LastUpdateDate;
            
            ReportDate.Text = DateTime.Now.ToString("dd/MM/yyyy");

            BuildTimetableGrid();
            LoadLists();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ayarlar yüklenemedi:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void BuildTimetableGrid()
    {
        TimetableContainer.Children.Clear();
        
        // Header Structure: Saat, Ders, Days...
        string[] dayHeaders = { "Saat", "Ders", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
        
        // Header Row
        var headerGrid = new Grid { Margin = new Thickness(0,0,0,5) };
        
        // Column Defs: [Saat: 80] [Ders: 40] [Days: * (Star)]
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Saat
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Ders
        for (int i = 0; i < 7; i++) headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Days
        
        for (int i = 0; i < 9; i++)
        {
            var border = new Border 
            { 
                Background = ThemeManager.GetBrush("BorderBrush"), // Gray-200 header background
                Padding = new Thickness(5),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(0,0,1,0) // Vertical separators
            };

            // Highlight Weekend Headers
            if (i >= 7) // Cmt, Paz (Indices 7, 8)
            {
                 border.Background = new SolidColorBrush(Color.FromRgb(255, 237, 213)); // Orange Tint
            }
            
            var tb = new TextBlock
            {
                Text = dayHeaders[i],
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.DimGray,
                FontSize = 11
            };
            border.Child = tb;

            // Gün başlığına çift tıklama ile günü komple aç/kapa
            if (i >= 2) // Gün sütunları (Pzt=2, Sal=3, ..., Paz=8)
            {
                border.Tag = i - 1; // day index: 1-7
                border.Cursor = Cursors.Hand;
                border.MouseLeftButtonDown += OnSchoolDayHeaderDoubleClick;
            }

            Grid.SetColumn(border, i);
            headerGrid.Children.Add(border);
        }
        TimetableContainer.Children.Add(headerGrid);
        
        // Data Rows
        for (int hour = 1; hour <= 12; hour++)
        {
            var rowGrid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            
            // Same columns
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            for (int i = 0; i < 7; i++) rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // 1. Time Input (Col 0) "08:10-08:50"
            var timeText = (_schoolInfo?.LessonHours != null && hour - 1 < _schoolInfo.LessonHours.Length) ? _schoolInfo.LessonHours[hour - 1] ?? "" : "";
            var timeBox = new TextBox 
            { 
                Text = timeText, 
                Padding = new Thickness(2), 
                Tag = hour - 1, 
                FontSize = 11, 
                HorizontalContentAlignment = HorizontalAlignment.Center,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.LightGray,
                Foreground = Brushes.Gray
            };
            if (string.IsNullOrEmpty(timeText)) timeBox.Text = "00:00-00:00"; // Placeholder hint
            
            timeBox.TextChanged += TimeBox_TextChanged;
            Grid.SetColumn(timeBox, 0);
            rowGrid.Children.Add(timeBox);

            // 2. Lesson Number Label (Col 1)
            var lessonNumBorder = new Border 
            {
                 Background = ThemeManager.GetBrush("SurfaceAltBrush"),
                 Child = new TextBlock { Text = hour.ToString(), HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, FontWeight=FontWeights.Bold, Foreground=Brushes.Gray }
            };
            Grid.SetColumn(lessonNumBorder, 1);
            rowGrid.Children.Add(lessonNumBorder);
            
            // 3. Day Cells (Col 2-8)
            for (int day = 1; day <= 7; day++)
            {
                var slot = new TimeSlot(day, hour);
                bool isOpen = true;
                if (_schoolInfo?.DefaultTimetable != null && _schoolInfo.DefaultTimetable.TryGetValue(slot, out var state))
                    isOpen = state == SlotState.Open;

                // Color Logic
                var bgBrush = isOpen ? new SolidColorBrush(Color.FromRgb(220, 252, 231)) : ThemeManager.GetBrush("SurfaceAltBrush"); // Green-100 check or Gray
                var textBrush = isOpen ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) : Brushes.Gray;
                string symbol = "A";

                // Weekend default handling for visualization
                if (day >= 6)
                {
                    // Weekend specific default look if closed
                     if (!isOpen)
                     {
                         bgBrush = new SolidColorBrush(Color.FromRgb(255, 247, 237)); // Orange-50
                         textBrush = Brushes.Orange;
                         symbol = "K";
                     }
                }
                else
                {
                    if (!isOpen) symbol = "K";
                }

                var cellBorder = new Border
                {
                    Background = bgBrush,
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1), // Grid lines
                    Cursor = Cursors.Hand,
                    Tag = $"{day}_{hour}",
                    Margin = new Thickness(1, 0, 1, 0)
                };

                // Add text content
                var text = new TextBlock 
                { 
                    Text = symbol, 
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = textBrush
                };
                cellBorder.Child = text;
                
                // Left Click Toggle
                cellBorder.MouseDown += (s, e) =>
                {
                    SlotButton_Click(s, e);
                };

                Grid.SetColumn(cellBorder, day + 1);
                rowGrid.Children.Add(cellBorder);
            }
            
            TimetableContainer.Children.Add(rowGrid);
        }
    }
    
    private void TimeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is int index && _schoolInfo?.LessonHours != null && index < 12)
        {
            _schoolInfo.LessonHours[index] = tb.Text;
            // Note: Saving happens on "Save" button, not on every keystroke
        }
    }
    
    private void SlotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_schoolInfo == null) return;
        if (sender is Border border && border.Tag is string tag)
        {
            var parts = tag.Split('_');
            if (parts.Length == 2 && int.TryParse(parts[0], out int day) && int.TryParse(parts[1], out int hour))
            {
                var slot = new TimeSlot(day, hour);
                if (_schoolInfo.DefaultTimetable == null) _schoolInfo.DefaultTimetable = new Dictionary<TimeSlot, SlotState>();
                
                if (!_schoolInfo.DefaultTimetable.ContainsKey(slot)) _schoolInfo.DefaultTimetable[slot] = SlotState.Open;
                
                // Toggle
                var current = _schoolInfo.DefaultTimetable[slot];
                _schoolInfo.DefaultTimetable[slot] = current == SlotState.Open ? SlotState.Closed : SlotState.Open;
                
                // Refresh Grid
                BuildTimetableGrid();
                new SchoolRepository().SaveSchoolTimetable(_schoolInfo.DefaultTimetable);
            }
        }
    }

    private void OnSchoolDayHeaderDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (_schoolInfo == null) return;
        if (sender is not Border border || border.Tag is not int dayIndex) return;

        if (_schoolInfo.DefaultTimetable == null)
            _schoolInfo.DefaultTimetable = new Dictionary<TimeSlot, SlotState>();

        // Tüm saatleri kontrol et: hepsi açık mı?
        bool allOpen = true;
        for (int h = 1; h <= 12; h++)
        {
            var slot = new TimeSlot(dayIndex, h);
            if (_schoolInfo.DefaultTimetable.TryGetValue(slot, out var state) && state == SlotState.Closed)
            {
                allOpen = false;
                break;
            }
        }

        // Hepsi açıksa kapat, değilse aç
        for (int h = 1; h <= 12; h++)
        {
            var slot = new TimeSlot(dayIndex, h);
            _schoolInfo.DefaultTimetable[slot] = allOpen ? SlotState.Closed : SlotState.Open;
        }

        BuildTimetableGrid();
        new SchoolRepository().SaveSchoolTimetable(_schoolInfo.DefaultTimetable);
    }

    private void LoadLists()
    {
        try
        {
            ClubsList.Items.Clear();
            foreach (var club in new ClubRepository().GetAll())
                ClubsList.Items.Add(club.Name);
            
            DutyLocationsList.Items.Clear();
            foreach (var loc in new DutyLocationRepository().GetAll())
                DutyLocationsList.Items.Add(loc.Name);
            
            OrtakMekanList.Items.Clear();
            foreach (var ortakMekan in new OrtakMekanRepository().GetAll())
                OrtakMekanList.Items.Add(ortakMekan.Name);
        }
        catch { }
    }
    
    private void AddClub_Click(object sender, RoutedEventArgs e)
    {
        var name = NewClubInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        
        new ClubRepository().Save(new Club { Name = name });
        NewClubInput.Text = "";
        LoadLists();
    }
    
    private void AddDutyLocation_Click(object sender, RoutedEventArgs e)
    {
        var name = NewDutyLocationInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        
        new DutyLocationRepository().Save(new DutyLocation { Name = name });
        NewDutyLocationInput.Text = "";
        LoadLists();
    }
    

    private void NewClubInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddClub_Click(sender, e);
    }

    private void NewDutyLocationInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddDutyLocation_Click(sender, e);
    }

    
    private void HelpVideo_Click(object sender, RoutedEventArgs e)
    {
        var window = new Views.VideoLibraryWindow();
        window.Owner = this;
        window.ShowDialog();
    }

    private void SaveSchoolInfo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_schoolInfo == null) _schoolInfo = new SchoolInfo();
            
            // Check if date changed (Reset optimization parameters)
            if (_schoolInfo.Date != SettingsStartDate.Text)
            {
                _schoolInfo.V3GapPenalty = 100; // Reset to default
            }

            _schoolInfo.Name = SettingsSchoolName.Text;
            _schoolInfo.Principal = SettingsPrincipal.Text;
            _schoolInfo.Date = SettingsStartDate.Text;
            // _schoolInfo.IsPasswordActive is deprecated in favor of SettingsManager
            
            SettingsManager.SetBool("AppPasswordActive", CheckEnableLoginPassword.IsChecked == true);
            
            // ALSO SAVE WEEKEND SETTINGS TO SABIT.SQLITE
            SettingsManager.SetBool("TeacherHideWeekend", CheckHideWeekend.IsChecked == true);
            SettingsManager.SetBool("DistHideWeekend", CheckDistHideWeekend.IsChecked == true);
            
            new SchoolRepository().SaveSchoolInfo(_schoolInfo);
            
            MessageBox.Show("Ayarlar kaydedildi!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme hatası:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Güncelleme indirilip yüklenecek.\nDevam etmek istiyor musunuz?",
            "Güncelleme", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var updateWindow = new Views.UpdateProgressWindow();
        updateWindow.Owner = this;
        updateWindow.ShowDialog();
    }
    
    private void DeleteClub_Click(object sender, RoutedEventArgs e)
    {
        var selected = ClubsList.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selected)) return;
        
        if (MessageBox.Show($"'{selected}' kulübünü silmek istediğinize emin misiniz?", 
            "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            var clubs = new ClubRepository().GetAll();
            var club = clubs.FirstOrDefault(c => c.Name == selected);
            if (club != null)
            {
                new ClubRepository().Delete(club.Id);
                LoadLists();
            }
        }
    }
    
    private void DeleteDutyLocation_Click(object sender, RoutedEventArgs e)
    {
        var selected = DutyLocationsList.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selected)) return;
        
        if (MessageBox.Show($"'{selected}' nöbet yerini silmek istediğinize emin misiniz?", 
            "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            var locations = new DutyLocationRepository().GetAll();
            var loc = locations.FirstOrDefault(l => l.Name == selected);
            if (loc != null)
            {
                new DutyLocationRepository().Delete(loc.Id);
                LoadLists();
            }
        }
    }
    
    
    private void AddOrtakMekan_Click(object sender, RoutedEventArgs e)
    {
        var name = NewOrtakMekanInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        
        new OrtakMekanRepository().Save(new OrtakMekan { Name = name });
        NewOrtakMekanInput.Text = "";
        LoadLists();
    }
    
    private void NewOrtakMekanInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddOrtakMekan_Click(sender, e);
    }
    
    private void DeleteOrtakMekan_Click(object sender, RoutedEventArgs e)
    {
        var selected = OrtakMekanList.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selected)) return;
        
        if (MessageBox.Show($"'{selected}' ortak mekanını silmek istediğinize emin misiniz?", 
            "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            var ortakMekanlar = new OrtakMekanRepository().GetAll();
            var ortakMekan = ortakMekanlar.FirstOrDefault(o => o.Name == selected);
            if (ortakMekan != null)
            {
                new OrtakMekanRepository().Delete(ortakMekan.Id);
                LoadLists();
            }
        }
    }
    
    // ==================== Dış Programdan Aktarım ====================

    private void ImportFromExternal_Click(object sender, RoutedEventArgs e)
    {
        // Available importers (add new ones here)
        var importers = new List<Services.Import.IScheduleImporter>
        {
            new Services.Import.HdpImporter()
        };

        // Build file filter from all importers
        var filters = importers.Select(i => $"{i.DisplayName} ({i.FileFilter.Split('|')[1]})|{i.FileFilter.Split('|')[1]}");
        var allFilter = string.Join("|", filters);

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Dış Program Dosyası Seçin",
            Filter = allFilter,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        // Find matching importer by extension
        var ext = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
        var importer = importers.FirstOrDefault(i => i.FileFilter.ToLowerInvariant().Contains(ext));
        if (importer == null)
        {
            MessageBox.Show("Bu dosya türü için uygun aktarıcı bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Generate new DB name
        var baseName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
        var targetPath = System.IO.Path.Combine(ConfigManager.Shared.DataDirectory, $"{baseName}.sqlite");

        // Check if target exists
        if (System.IO.File.Exists(targetPath))
        {
            var overwrite = MessageBox.Show(
                $"'{baseName}.sqlite' zaten mevcut. Üzerine yazılsın mı?",
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (overwrite != MessageBoxResult.Yes) return;
        }

        try
        {
            var result = importer.Import(dialog.FileName, targetPath);

            if (result.Success)
            {
                var switchDb = MessageBox.Show(
                    $"Aktarım başarılı!\n\n{result.Message}\n\nYeni veritabanına geçiş yapılsın mı?",
                    "Aktarım Tamamlandı", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (switchDb == MessageBoxResult.Yes)
                {
                    DatabaseManager.Shared.CloseDatabase();
                    ConfigManager.Shared.SetActiveDatabase(targetPath);
                    DatabaseManager.Shared.OpenDatabase(targetPath);
                    DatabaseSchema.Initialize(DatabaseManager.Shared);
                    DatabaseNameLabel.Text = baseName;
                    CheckLicense();
                    LoadDashboardData();
                }
            }
            else
            {
                MessageBox.Show($"Aktarım başarısız:\n{result.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Aktarım sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== Tehlikeli Bölge (Danger Zone) ====================
    
    private void ResetTeacherAssignments_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Bu işlem TÜM derslerdeki öğretmen atamalarını kaldıracaktır.\n\n" +
            "• Ders-öğretmen bağlantıları silinecek\n" +
            "• Dağıtım blokları etkilenmeyecek\n" +
            "• Bu işlem geri alınamaz\n\n" +
            "Devam etmek istiyor musunuz?",
            "Öğretmen Atamalarını Sıfırla",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            DatabaseManager.Shared.Execute("DELETE FROM atama");
            MessageBox.Show("Tüm öğretmen atamaları sıfırlandı.", "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void ResetDistribution_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Dağıtım temizlenecek. Elle kilitlenmiş/yerleştirilmiş dersler KORUNSUN MU?\n\n" +
            "• EVET: Elle yerleştirilenler kalır, diğerleri silinir.\n" +
            "• HAYIR: Tüm dağıtım tamamen silinir.\n" +
            "• İPTAL: İşlem iptal edilir.",
            "Dağıtımı Sıfırla",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
            
        if (result == MessageBoxResult.Cancel) return;
        
        bool keepManual = (result == MessageBoxResult.Yes);
        bool wipeAll = (result == MessageBoxResult.No);

        // Use proper repository method to clean all tables
        var repo = new DistributionRepository();
        repo.ResetAllDistributions(keepManual, wipeAll); 
        
        RefreshDistribution_Click(null, null);
    }
    
    // --- Lessons Logic ---
    
    private void LoadLessons(string query = "")
    {
        try
        {
            LessonsListContainer.Children.Clear();
            var repo = new LessonRepository();
            var lessons = string.IsNullOrEmpty(query) ? repo.GetAll() : repo.Search(query);
            
            // Sort by name alphabetically
            var sortedLessons = lessons.OrderBy(l => l.Name).ToList();

            foreach (var lesson in sortedLessons)
            {
                var row = CreateLessonRow(lesson);
                LessonsListContainer.Children.Add(row);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dersler yüklenirken hata: {ex.Message}");
        }
    }
    
    private Border CreateLessonRow(Lesson lesson)
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = ThemeManager.GetBrush("BorderBrush"), // gray-200
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 8, 10, 8) // Reduced padding
        };
        
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Initial
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name/Code
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Tags & Actions
        
        // 1. Initial Circle
        var initialBorder = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(16), // Smaller circle
            Background = new BrushConverter().ConvertFrom(lesson.InitialColor) as Brush ?? Brushes.LightGray,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var initialText = new TextBlock
        {
            Text = lesson.Initial,
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = new BrushConverter().ConvertFrom(lesson.InitialTextColor) as Brush ?? Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        initialBorder.Child = initialText;
        Grid.SetColumn(initialBorder, 0);
        grid.Children.Add(initialBorder);
        
        // 2. Name & Code
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
        infoStack.Children.Add(new TextBlock { Text = lesson.Name, FontWeight = FontWeights.SemiBold, FontSize = 13 });
        infoStack.Children.Add(new TextBlock { Text = $"Kod: {lesson.Code}", Foreground = Brushes.Gray, FontSize = 11 });
        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);
        
        // 3. Right Side (Block, Actions)
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        
        // Default Block
        rightStack.Children.Add(new TextBlock { Text = $"Blok: {lesson.DefaultBlock}", Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center });
        
        // Edit Button
        var editBtn = new Button 
        { 
            Content = "\U0001F4DD", 
            Background = ThemeManager.GetBrush("SurfaceAltBrush"), 
            BorderBrush = ThemeManager.GetBrush("BorderBrush"),
            BorderThickness = new Thickness(1), 
            Foreground = Brushes.Black,
            Margin = new Thickness(0, 0, 5, 0), 
            Padding = new Thickness(6, 2, 6, 2),
            Tag = lesson, 
            Cursor = Cursors.Hand, 
            FontSize = 12 
        };
        editBtn.Resources.Add(typeof(Border), new Style(typeof(Border)) { Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(4)) } }); // Rounded corners hack or use Template
        
        // Use a simpler approach for rounded corners if Resources doesn't work on default button template easily in code-behind without full template. 
        // Actually, standard WPF button is rectangular. Let's just keep it simple or use a Border wrapper if needed. 
        // For now, standard rectangular button with border is fine.
        
        editBtn.Click += EditLesson_Click;
        rightStack.Children.Add(editBtn);
        
        // Delete Button
        var delBtn = new Button 
        { 
            Content = "\U0001F5D1", 
            Background = ThemeManager.GetBrush("ErrorLightBrush"), 
            BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202)),
            BorderThickness = new Thickness(1), 
            Foreground = Brushes.Red, 
            Padding = new Thickness(6, 2, 6, 2),
            Tag = lesson, 
            Cursor = Cursors.Hand, 
            FontSize = 12 
        };
        delBtn.Click += DeleteLesson_Click;
        rightStack.Children.Add(delBtn);
        
        Grid.SetColumn(rightStack, 2);
        grid.Children.Add(rightStack);
        
        border.Child = grid;
        return border;
    }
    
    private void LessonSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (LessonSearchPlaceholder != null)
            LessonSearchPlaceholder.Visibility = string.IsNullOrEmpty(LessonSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        
        LoadLessons(LessonSearchBox.Text);
    }
    
    private void AddLesson_Click(object sender, RoutedEventArgs e)
    {
        var window = new Views.LessonEditorWindow();
        window.Owner = this;
        if (window.ShowDialog() == true)
        {
            LoadLessons(LessonSearchBox.Text);
        }
    }
    
    private void EditLesson_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Lesson lesson)
        {
            var window = new Views.LessonEditorWindow(lesson);
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                LoadLessons(LessonSearchBox.Text);
            }
        }
    }
    
    private void DeleteLesson_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Lesson lesson)
        {
            var msg = $"DERS SİLİNECEK!\n\n" +
                      $"Ders Adı: {lesson.Name}\n" +
                      $"Kodu: {lesson.Code}\n\n" +
                      "Bu dersi silmek istediğinize emin misiniz?";
                      
            if (MessageBox.Show(msg, "Ders Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                new LessonRepository().Delete(lesson.Id);
                LoadLessons(LessonSearchBox.Text);
            }
        }
    }
    
    // --- Teachers Logic ---
    
    private void LoadTeachers(string query = "")
    {
        try
        {
            // Clear current list logic for data binding
            TeachersList.ItemsSource = null;
            
            var repo = new TeacherRepository();
            repo.SyncAllTeacherHours(); // Recalculate from dagitim_bloklari
            
            var teachers = (string.IsNullOrEmpty(query) ? repo.GetAll() : repo.Search(query))
                            .OrderBy(t => t.Name).ToList();
            
            TeachersList.ItemsSource = teachers;
            
            // Auto Select First Teacher
            if (teachers.Count > 0)
            {
                TeachersList.SelectedIndex = 0;
            }
            
            // Populate ComboBoxes if not already
            if (ComboDutyDay.Items.Count == 0)
            {
                ComboDutyDay.ItemsSource = new[] { "", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };
                
                try 
                {
                    var dutyPlaces = new DutyLocationRepository().GetAll();
                    dutyPlaces.Insert(0, new DutyLocation { Id = 0, Name = "" });
                    ComboDutyPlace.ItemsSource = dutyPlaces;

                    var clubs = new ClubRepository().GetAll();
                    clubs.Insert(0, new Club { Id = 0, Name = "" });
                    ComboClub.ItemsSource = clubs;
                    
                    var classes = new ClassRepository().GetAll();
                    classes.Insert(0, new SchoolClass { Id = 0, Name = "" });
                    ComboGuidanceClass.ItemsSource = classes;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Öğretmenler yüklenirken hata: {ex.Message}");
        }
    }
    
    // CreateTeacherRow deleted - replaced by ItemTemplate in XAML
    
    private void TeacherSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TeacherSearchPlaceholder != null)
            TeacherSearchPlaceholder.Visibility = string.IsNullOrEmpty(TeacherSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            
        LoadTeachers(TeacherSearchBox.Text);
    }
    
    private Teacher? _selectedTeacher;
    private bool _isLoadingTeacher = false;
    
    private void TeachersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _isLoadingTeacher = true;
        try
        {
            // Updated cast for Data Binding (direct Teacher object)
            if (TeachersList.SelectedItem is Teacher teacher)
            {
                _selectedTeacher = teacher;
                TeacherDetailPanel.DataContext = teacher;
                TeacherDetailPanel.Visibility = Visibility.Visible;
                TeacherTimetableArea.Visibility = Visibility.Visible;
                TeacherSelectMessage.Visibility = Visibility.Collapsed;
                
                SetupTeacherTimetable(teacher);
                LoadTeacherAssignments(teacher);
                TxtTeacherNotes.Text = teacher.Notes ?? "";
            }
            else
            {
                _selectedTeacher = null;
                TeacherDetailPanel.Visibility = Visibility.Collapsed;
                TeacherTimetableArea.Visibility = Visibility.Collapsed;
                TeacherSelectMessage.Visibility = Visibility.Visible;
                TxtTeacherNotes.Text = "";
            }
        }
        finally
        {
            _isLoadingTeacher = false;
        }
    }

    private void LoadTeacherAssignments(Teacher teacher)
    {
        try
        {
            var repo = new TeacherRepository();
            var assignments = repo.GetAssignments(teacher.Id);
            TeacherAssignmentsGrid.ItemsSource = assignments;
            
            int total = assignments.Sum(a => a.TotalHours);
            TxtTeacherAssignmentTotal.Text = total.ToString();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dersler yüklenirken hata oluştu: {ex.Message}");
        }
    }

    private void TxtTeacherNotes_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedTeacher == null) return;
        _selectedTeacher.Notes = TxtTeacherNotes.Text;
        new TeacherRepository().Save(_selectedTeacher);
    }

    private void TeacherInfo_AutoSaveGeneric(object sender, RoutedEventArgs e)
    {
        if (_isLoadingTeacher || _selectedTeacher == null) return;
        try 
        {
             new TeacherRepository().Save(_selectedTeacher);
        }
        catch (Exception ex)
        {
            // System.Diagnostics.Debug.WriteLine($"AutoSave Error: {ex.Message}");
        }
    }

    private void TeacherInfo_AutoSave(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingTeacher || _selectedTeacher == null) return;
        
        // Ensure the binding has propagated the value to the object
        // For ComboBox, the binding usually updates on PropertyChanged (selection change). 
        // We defer slightly or just trust the binding engine has fired before this event handler finishes or concurrent with it.
        // Actually, in WPF, source update happens usually before the event bubbles up if UpdateSourceTrigger is PropertyChanged (default for Selector.SelectedValue).
        
        try 
        {
             // Force update source for the sender if possible, to be absolutely sure? 
             // Not strictly necessary if default binding holds. 
             // Let's just Save.
             
             new TeacherRepository().Save(_selectedTeacher);
             
             // Optional: visual feedback? Maybe too distracting.
        }
        catch (Exception ex)
        {
            // Silent fail or log
            // System.Diagnostics.Debug.WriteLine($"AutoSave Error: {ex.Message}");
        }
    }
    
    private void AddTeacher_Click(object sender, RoutedEventArgs e)
    {
        var addWindow = new Views.AddTeacherWindow();
        if (addWindow.ShowDialog() == true)
        {
            if (TeacherSearchBox != null) TeacherSearchBox.Text = "";
            LoadTeachers("");
            
            // Select the newly created teacher
            if (addWindow.CreatedTeacher != null)
            {
                // Find and select the new teacher in the list
                foreach (var item in TeachersList.Items)
                {
                    if (item is Teacher t && t.Name == addWindow.CreatedTeacher.Name)
                    {
                        TeachersList.SelectedItem = item;
                        break;
                    }
                }
            }
        }
    }
    
    private void SaveTeacher_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTeacher != null)
        {
            try
            {
                new TeacherRepository().Save(_selectedTeacher);
                MessageBox.Show("Öğretmen kaydedildi!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Refresh list but keep selection
                int selectedId = _selectedTeacher.Id;
                LoadTeachers(TeacherSearchBox.Text);
                
                // Re-select logic
                foreach(var item in TeachersList.Items)
                {
                    if (item is Teacher t && t.Id == selectedId)
                    {
                        TeachersList.SelectedItem = item;
                        TeachersList.ScrollIntoView(item);
                        break;
                    }
                    else if (item is ListBoxItem lbItem && lbItem.Tag is Teacher t2 && t2.Id == selectedId)
                    {
                        TeachersList.SelectedItem = lbItem;
                        TeachersList.ScrollIntoView(lbItem);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaydetme hatası: {ex.Message}");
            }
        }
    }
    
    private void EditTeacherContext_Click(object sender, RoutedEventArgs e)
    {
        // Try to get teacher from context menu's data context
        var teacher = (sender as MenuItem)?.DataContext as Teacher;
        if (teacher == null) teacher = _selectedTeacher;

        if (teacher != null)
        {
            var editor = new Views.TeacherEditorWindow(teacher);
            if (editor.ShowDialog() == true)
            {
                LoadTeachers(TeacherSearchBox.Text);
                // Selection logic handled by LoadTeachers or could re-select here
            }
        }
    }
    
    private void DeleteTeacher_Click(object sender, RoutedEventArgs e)
    {
        var teacher = (sender as MenuItem)?.DataContext as Teacher;
        if (teacher == null) teacher = _selectedTeacher;

        if (teacher != null)
        {
            if (MessageBox.Show($"'{teacher.Name}' öğretmenini silmek istediğinize emin misiniz?", 
                "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                new TeacherRepository().Delete(teacher.Id);
                LoadTeachers(TeacherSearchBox.Text);
                
                if (_selectedTeacher != null && _selectedTeacher.Id == teacher.Id)
                {
                    _selectedTeacher = null;
                    TeacherDetailPanel.Visibility = Visibility.Collapsed;
                    TeacherTimetableArea.Visibility = Visibility.Collapsed;
                    TeacherSelectMessage.Visibility = Visibility.Visible;
                }
            }
        }
    }
    
    private void SetupTeacherTimetable(Teacher teacher)
    {
        TeacherTimetableGrid.Children.Clear();
        
        bool hideWeekend = CheckHideWeekend.IsChecked == true;
        
        // Header
        var headerGrid = new Grid();
        string[] days = { "Ders", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
        int colIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (hideWeekend && (i == 6 || i == 7)) continue; // Skip Sat/Sun
            
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 0 ? new GridLength(65) : new GridLength(1, GridUnitType.Star) });
            
            if (i == 0)
            {
                var tb = new TextBlock { Text = days[i], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(5) };
                Grid.SetColumn(tb, colIndex++);
                headerGrid.Children.Add(tb);
            }
            else
            {
                var border = new Border 
                { 
                    Background = Brushes.Transparent, // Hit test visible
                    Tag = i, // Day index (1..7)
                    Cursor = Cursors.Hand,
                    ToolTip = "Bu güne çift tıklayarak tümünü Aç/Kapat"
                };
                border.MouseLeftButtonDown += TeacherDayHeader_Click;
                
                var tb = new TextBlock { Text = days[i], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(5) };
                border.Child = tb;
                
                Grid.SetColumn(border, colIndex++);
                headerGrid.Children.Add(border);
            }
        }
        TeacherTimetableGrid.Children.Add(headerGrid);
        
        // Rows
        for (int hour = 1; hour <= 12; hour++)
        {
            var rowGrid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            
            // Re-calculate column definitions for each row
            colIndex = 0;
            if (hideWeekend)
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) }); // Hour
                for (int i = 0; i < 5; i++) rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            else
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) }); // Hour (Widened for times)
                for (int i = 0; i < 7; i++) rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            
            // Hour Label
            // Hour Label with Time
            var headerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            headerStack.Children.Add(new TextBlock { Text = hour.ToString(), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            
            if (_schoolInfo != null && _schoolInfo.LessonHours != null && _schoolInfo.LessonHours.Length >= hour)
            {
                var timeStr = _schoolInfo.LessonHours[hour - 1];
                if (!string.IsNullOrEmpty(timeStr))
                {
                     headerStack.Children.Add(new TextBlock 
                     { 
                         Text = timeStr, 
                         FontSize = 8, 
                         Foreground = Brushes.Gray, 
                         HorizontalAlignment = HorizontalAlignment.Center, 
                         Margin = new Thickness(0,1,0,0) 
                     });
                }
            }

            var hourLabel = new Border 
            { 
                Background = ThemeManager.GetBrush("SurfaceAltBrush"), 
                CornerRadius = new CornerRadius(4),
                Child = headerStack
            };
            Grid.SetColumn(hourLabel, 0);
            rowGrid.Children.Add(hourLabel);
            
            // Day Cells
            int currentGridCol = 1;
            for (int day = 1; day <= 7; day++)
            {
                if (hideWeekend && (day == 6 || day == 7)) continue;

                int h = hour;
                int d = day;
                var slot = new TimeSlot(d, h);
                bool isClosed = teacher.Constraints.ContainsKey(slot) && teacher.Constraints[slot] == SlotState.Closed;
                
                var btn = new Button
                {
                    Tag = slot,
                    Content = isClosed ? "âœ•" : "",
                    Background = isClosed ? Brushes.MistyRose : Brushes.White,
                    BorderBrush = ThemeManager.GetBrush("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    Height = 35,
                    Margin = new Thickness(1),
                    Cursor = Cursors.Hand
                };
                
                // Priority 1: If there is lesson info, show it!
                if (teacher.ScheduleInfo != null && teacher.ScheduleInfo.ContainsKey(slot))
                {
                    // Ders programı varsa göster
                    var info = teacher.ScheduleInfo[slot];
                    if (info != "KAPALI")
                    {
                        // Format: "10-M Mat" -> Split
                        var parts = info.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        string line1 = parts.Length > 0 ? parts[0] : info;
                        string line2 = parts.Length > 1 ? parts[1] : "";

                        if (hideWeekend)
                        {
                            // Side-by-side: "10-M Mat"
                            var textBlock = new TextBlock 
                            { 
                                Text = $"{line1}  {line2}".Trim(), 
                                FontWeight = FontWeights.Bold, 
                                FontSize = 10,
                                Foreground = Brushes.Black, 
                                TextAlignment = TextAlignment.Center,
                                TextWrapping = TextWrapping.NoWrap,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            };
                            btn.Content = textBlock;
                            btn.ToolTip = $"{line1} {line2}\n{ThemeManager.GetLessonName(line1)}";
                        }
                        else
                        {
                            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                            stack.Children.Add(new TextBlock 
                            { 
                                Text = line1, 
                                FontWeight = FontWeights.Bold, 
                                FontSize = 10,
                                Foreground = Brushes.Black, 
                                TextAlignment = TextAlignment.Center 
                            });
                            
                            if (!string.IsNullOrEmpty(line2))
                            {
                                stack.Children.Add(new TextBlock 
                                { 
                                    Text = line2, 
                                    FontSize = 9, 
                                    TextAlignment = TextAlignment.Center,
                                    Foreground = Brushes.DarkSlateGray
                                });
                            }
                            btn.Content = stack;
                        }
                        
                        // Simple Color Hash based on Class Name (Line 1)
                        int colorIndex = Math.Abs(line1.GetHashCode()) % ThemeManager.LightPastelColors.Count;
                        btn.Background = ThemeManager.LightPastelColors[colorIndex];
                        btn.BorderBrush = ThemeManager.DeepPastelColors[colorIndex];
                    }
                }
                // Priority 2: If it's closed (and no lesson), show "--"
                else if (isClosed)
                {
                    btn.Content = "--";
                    btn.FontSize = 12;
                    btn.FontWeight = FontWeights.Bold;
                    btn.Background = ThemeManager.GetClosedSlotBrush();
                    btn.Foreground = ThemeManager.GetClosedSlotTextBrush();
                }
                
                btn.PreviewMouseDoubleClick += (s, args) => 
                {
                    if (s is Button b && b.Tag is TimeSlot ts)
                    {
                        // Toggle constraint
                        if (teacher.Constraints.ContainsKey(ts))
                            teacher.Constraints.Remove(ts);
                        else
                            teacher.Constraints[ts] = SlotState.Closed;
                        
                        // Persist teacher changes
                        new TeacherRepository().Save(teacher);
                        
                        SetupTeacherTimetable(teacher);
                    }
                    args.Handled = true;
                };
                
                Grid.SetColumn(btn, currentGridCol++);
                rowGrid.Children.Add(btn);
            }
            
            TeacherTimetableGrid.Children.Add(rowGrid);
        }
        
    }
    
    private void TeacherDayHeader_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (_selectedTeacher == null) return;
        if (sender is Border border && border.Tag is int dayIndex)
        {
            // Logic: "Okulun o günkü açık dersi kadar açık/kapalı"
            // Usually school has standard 8 or so lessons. 
            // We'll toggle 1..MaxDailyLessons. 
            
            int maxConfigured = _schoolInfo?.DailyLessonCount ?? 8;
            if (maxConfigured < 1) maxConfigured = 8;
            
            var validHours = new List<int>();
            
            for (int h = 1; h <= 12; h++)
            {
                var slot = new TimeSlot(dayIndex, h);
                bool inMap = _schoolInfo != null && _schoolInfo.DefaultTimetable.ContainsKey(slot);
                SlotState state = inMap ? _schoolInfo.DefaultTimetable[slot] : SlotState.Open;
                
                if (inMap)
                {
                    // Explicit setting in map takes precedence
                    if (state == SlotState.Open) validHours.Add(h);
                    // If Closed, we skip
                }
                else
                {
                    // Not in map -> Use start/count config
                    if (h <= maxConfigured) validHours.Add(h);
                }
            }
            
            if (validHours.Count == 0) return;

            // Check State of Valid Hours for this teacher
            bool isAllOpen = true;
            foreach (int h in validHours)
            {
                var slot = new TimeSlot(dayIndex, h);
                if (_selectedTeacher.Constraints.ContainsKey(slot) && _selectedTeacher.Constraints[slot] == SlotState.Closed)
                {
                    isAllOpen = false;
                    break;
                }
            }
            
            // Action
            foreach (int h in validHours)
            {
                var slot = new TimeSlot(dayIndex, h);
                if (isAllOpen)
                {
                    // Close
                    _selectedTeacher.Constraints[slot] = SlotState.Closed;
                }
                else
                {
                    // Open
                    if (_selectedTeacher.Constraints.ContainsKey(slot))
                        _selectedTeacher.Constraints.Remove(slot);
                }
            }
            
            // Persist
             new TeacherRepository().Save(_selectedTeacher);
             
             // Refresh
             SetupTeacherTimetable(_selectedTeacher);
        }
    }

    private void CheckHideWeekend_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.SetBool("TeacherHideWeekend", CheckHideWeekend.IsChecked == true);
        if (_selectedTeacher != null)
        {
            SetupTeacherTimetable(_selectedTeacher);
        }
    }

    
    
    


    // ==================== ABACUS DISTRIBUTION LOGIC ====================
    
    private void LoadDistributionCombos()
    {
        try
        {
            var teacherRepo = new TeacherRepository();
            teacherRepo.SyncAllTeacherHours(); // Ensure totals are up to date
            
            DistTeacherCombo.ItemsSource = teacherRepo.GetAll();
            DistClassCombo.ItemsSource = new ClassRepository().GetAll();
        }
        catch { }
    }
    
    private void StartDistribution_Click(object sender, RoutedEventArgs e)
    {
        var win = new DistributionParametersWindow();
        win.Owner = this;
        // Pre-set for full rebuild
        win.Parameters.OperationMode = OperationMode.Rebuild;
        win.Parameters.PlacementMode = PlacementMode.ClearAll;
        
        if (win.ShowDialog() == true)
        {
            StartSolver(win.Parameters);
        }
    }
    
    private void StartOrToolsDistribution_Click(object sender, RoutedEventArgs e)
    {
        // V3 (OR-Tools) doğrudan çalışsın
        var schoolInfo = new SchoolRepository().GetSchoolInfo();
        var parameters = new DistributionParameters
        {
            OperationMode = OperationMode.Rebuild,
            PlacementMode = PlacementMode.ClearAll,
            EngineVersion = 3,
            MaxDays = schoolInfo?.Days ?? 5,
            MaxTimeInSeconds = 900, // Varsayılan 15 dakika
            V3GapPenalty = schoolInfo?.V3GapPenalty ?? 100
        };
        
        StartSolver(parameters);
    }

    private void StartOrToolsAdvancedDistribution_Click(object sender, RoutedEventArgs e)
    {
        // V7 (Advanced OR-Tools)
        var schoolInfo = new SchoolRepository().GetSchoolInfo();
        var parameters = new DistributionParameters
        {
            OperationMode = OperationMode.Rebuild,
            PlacementMode = PlacementMode.ClearAll,
            EngineVersion = 7,
            MaxDays = schoolInfo?.Days ?? 5,
            MaxTimeInSeconds = 600, // V7 gets double time
            V3GapPenalty = schoolInfo?.V3GapPenalty ?? 100
        };
        
        StartSolver(parameters);
    }
    
    // Store unplaced blocks for UI access
    private List<(DersDagitim.Models.DistributionBlock block, string reason)> _lastUnplacedBlocks = new();

    private void DeleteDistribution_Click(object sender, RoutedEventArgs e)
    {
        // Simply call the existing function in Settings
        ResetDistribution_Click(sender, e);
    }

    private void ScheduleEdit_Click(object sender, RoutedEventArgs e)
    {
        var editWindow = new Views.TeacherScheduleEditWindow();
        editWindow.Owner = this;
        editWindow.ShowDialog();
        // Pencere kapandıktan sonra dağıtım grid'ini yenile
        RefreshDistribution_Click(null, null);
    }

    private void RefreshDistribution_Click(object? sender, RoutedEventArgs? e)
    {
        UpdateStats();
        // Reload currently valid grids
        if (DistTeacherCombo.SelectedValue is int tid) RenderDistributionGrid(tid, true);
        if (DistClassCombo.SelectedValue is int cid) RenderDistributionGrid(cid, false);
    }
    
    private async void StartSolver(DistributionParameters cx)
    {
        this.IsEnabled = false; // Lock main window

        // Create and show progress window
        var progressWindow = new Views.DistributionProgressWindow();
        progressWindow.Owner = this;
        progressWindow.Show();

        try
        {
            bool engineSuccess = false;
            var engineErrors = new List<string>();

            // ENGINE SELECTION V3
            if (cx.EngineVersion == 3)
            {
                var v3engine = new OrToolsSchedulerEngine();

                await System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var result = await v3engine.Run(cx, (status) =>
                        {
                            try { Dispatcher.Invoke(() => progressWindow.UpdateStatus(status)); } catch { }
                        });
                        engineSuccess = result.success;
                        engineErrors = result.errors;
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => progressWindow.SetError(ex.Message));
                        throw;
                    }
                });

                // Gap penalty otomatik azaltma (sessiz, kullanıcıya ayrı MessageBox gösterilmez)
                if (!engineSuccess)
                {
                    try
                    {
                        var sRepo = new SchoolRepository();
                        var info = sRepo.GetSchoolInfo();
                        info.V3GapPenalty = (int)(info.V3GapPenalty * 0.9);
                        if (info.V3GapPenalty < 1) info.V3GapPenalty = 1;
                        sRepo.SaveSchoolInfo(info);
                        if (_schoolInfo != null) _schoolInfo.V3GapPenalty = info.V3GapPenalty;
                    }
                    catch { }
                }
            }
            else if (cx.EngineVersion == 7)
            {
                progressWindow.Title = "V7 Gelişmiş AI Dağıtım Motoru";
                var v7engine = new OrToolsAdvancedEngine();

                await System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await v7engine.Run(cx, (status) =>
                        {
                            try { Dispatcher.Invoke(() => progressWindow.UpdateStatus(status)); } catch { }
                        });
                        engineSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => progressWindow.SetError(ex.Message));
                        throw;
                    }
                });
            }
            else
            {
                var engine = new DistributionEngine();
                await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        engine.Run(cx, (status) =>
                        {
                            Dispatcher.Invoke(() => progressWindow.UpdateStatus(status));
                        });
                        engineSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => progressWindow.SetError(ex.Message));
                        throw;
                    }
                });
            }

            // Get final counts
            var repo = new DistributionRepository();
            var blocks = repo.GetAllBlocks();
            int placed = blocks.Count(b => b.Day > 0);
            int total = blocks.Count;

            if (engineSuccess)
            {
                progressWindow.SetComplete(placed, total);
            }
            else
            {
                progressWindow.SetFailed(placed, total, engineErrors);
            }

            RefreshDistribution_Click(null, null);
        }
        catch (Exception ex)
        {
            progressWindow.SetError(ex.Message);
        }
        finally
        {
            this.IsEnabled = true; // Unlock main window
        }
    }
    
    private void UpdateStats()
    {
        try
        {
             var repo = new DistributionRepository();
             var all = repo.GetAllBlocks();
             int placed = all.Count(b => b.Day > 0);
             int total = all.Count;
             double pct = total > 0 ? (placed * 100.0 / total) : 0;
             StatPlaced.Text = $"%{pct:F1}";
             
             // Unplaced logic - get from database
             var unplacedBlocks = all.Where(b => b.Day == 0 || b.Hour == 0).ToList();
             int unplaced = unplacedBlocks.Count;
             StatUnplaced.Text = unplaced.ToString();
             StatUnplaced.Foreground = unplaced > 0 ? Brushes.Red : Brushes.Green;
             
             // Update _lastUnplacedBlocks from database if not already set by engine
             if (_lastUnplacedBlocks.Count == 0 && unplacedBlocks.Count > 0)
             {
                 _lastUnplacedBlocks = unplacedBlocks.Select(b => (b, "Yerleştirilmemiş (veritabanından)")).ToList();
             }
             else if (unplacedBlocks.Count > 0 && _lastUnplacedBlocks.Count != unplacedBlocks.Count)
             {
                 // Sync with database - add any blocks that are unplaced but not in our list
                 var existingIds = _lastUnplacedBlocks.Select(x => x.block.Id).ToHashSet();
                 foreach (var block in unplacedBlocks)
                 {
                     if (!existingIds.Contains(block.Id))
                     {
                         _lastUnplacedBlocks.Add((block, "Yerleştirilmemiş"));
                     }
                 }
                 // Remove any that are now placed
                 _lastUnplacedBlocks = _lastUnplacedBlocks.Where(x => unplacedBlocks.Any(b => b.Id == x.block.Id)).ToList();
             }
             else if (unplacedBlocks.Count == 0)
             {
                 _lastUnplacedBlocks.Clear();
             }
             
             // Gap Calculation Logic
             int totalGaps = 0;
             
             // 1. Group by Teacher
             var teacherBlocks = new Dictionary<int, List<DistributionBlock>>();
             foreach(var b in all)
             {
                 if (b.Day == 0) continue;
                 foreach(var tid in b.TeacherIds)
                 {
                     if (!teacherBlocks.ContainsKey(tid)) teacherBlocks[tid] = new List<DistributionBlock>();
                     teacherBlocks[tid].Add(b);
                 }
             }

             // 2. Calculate Gap for each teacher/day
             foreach(var kvp in teacherBlocks)
             {
                 var blocks = kvp.Value;
                 var dayGroups = blocks.GroupBy(b => b.Day);
                 
                 foreach(var group in dayGroups)
                 {
                     int minH = 100;
                     int maxH = 0;
                     int durationSum = 0;
                     
                     foreach(var b in group)
                     {
                         if(b.Hour < minH) minH = b.Hour;
                         int end = b.Hour + b.BlockDuration - 1;
                         if(end > maxH) maxH = end;
                         durationSum += b.BlockDuration;
                     }
                     
                     if (maxH >= minH)
                     {
                         // Span = LastSlot - FirstSlot + 1
                         // Gap = Span - ActualDuration
                         int span = maxH - minH + 1;
                         int gap = span - durationSum;
                         if (gap > 0) totalGaps += gap;
                     }
                 }
             }
             
             StatGaps.Text = totalGaps.ToString();
        }
        catch {}
    }
    
    /// <summary>
    /// Show unplaced blocks popup when clicking on the unplaced count
    /// </summary>
    private void StatUnplaced_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_lastUnplacedBlocks.Count == 0)
        {
            MessageBox.Show("Tüm bloklar başarıyla dağıtıldı!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        // Create popup window
        var popup = new Window
        {
            Title = $"Dağıtılamayan Bloklar ({_lastUnplacedBlocks.Count})",
            Width = 700,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = ThemeManager.GetBrush("BackgroundBrush")
        };
        
        var mainPanel = new StackPanel { Margin = new Thickness(16) };
        
        // Header
        mainPanel.Children.Add(new TextBlock
        {
            Text = "âš ï¸ Dağıtılamayan Bloklar",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = ThemeManager.GetBrush("ErrorBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        });
        
        mainPanel.Children.Add(new TextBlock
        {
            Text = "Bir bloğa tıklayarak öğretmen ve sınıf programlarını görüntüleyebilirsiniz.",
            Foreground = ThemeManager.GetBrush("TextSecondaryBrush"),
            Margin = new Thickness(0, 0, 0, 16)
        });
        
        // Create scrollable list
        var scrollViewer = new ScrollViewer { MaxHeight = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var listPanel = new StackPanel();
        
        var teacherRepo = new TeacherRepository();
        var classRepo = new ClassRepository();
        var allTeachers = teacherRepo.GetAll().ToDictionary(t => t.Id);
        var allClasses = classRepo.GetAll().ToDictionary(c => c.Id);
        
        foreach (var (block, reason) in _lastUnplacedBlocks)
        {
            var teacherNames = string.Join(", ", block.TeacherIds.Select(tid => 
                allTeachers.TryGetValue(tid, out var t) ? t.Name : tid.ToString()));
            var className = allClasses.TryGetValue(block.ClassId, out var c) ? c.Name : block.ClassId.ToString();
            
            var itemBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = ThemeManager.GetBrush("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            
            var itemPanel = new StackPanel();
            itemPanel.Children.Add(new TextBlock
            {
                Text = $"{block.LessonCode} (Blok: {block.BlockDuration} saat)",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            });
            itemPanel.Children.Add(new TextBlock
            {
                Text = $"Sınıf: {className}  |  Öğretmen: {teacherNames}",
                Foreground = ThemeManager.GetBrush("TextSecondaryBrush"),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 4)
            });
            itemPanel.Children.Add(new TextBlock
            {
                Text = $"Sebep: {reason}",
                Foreground = ThemeManager.GetBrush("ErrorBrush"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
            
            itemBorder.Child = itemPanel;
            
            // Click handler to show teacher/class schedules
            var capturedBlock = block;
            itemBorder.MouseLeftButtonUp += (s, args) =>
            {
                popup.Close();
                
                // Select teacher in distribution view
                if (capturedBlock.TeacherIds.Count > 0)
                {
                    var firstTeacherId = capturedBlock.TeacherIds[0];
                    DistTeacherCombo.SelectedValue = firstTeacherId;
                }
                
                // Select class in distribution view
                DistClassCombo.SelectedValue = capturedBlock.ClassId;
                
                // No message box - just show the schedules directly
            };
            
            listPanel.Children.Add(itemBorder);
        }
        
        scrollViewer.Content = listPanel;
        mainPanel.Children.Add(scrollViewer);
        
        // Close button
        var closeButton = new Button
        {
            Content = "Kapat",
            Width = 100,
            Padding = new Thickness(12, 8, 12, 8),
            Background = ThemeManager.GetBrush("PrimaryBrush"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        closeButton.Click += (s, args) => popup.Close();
        mainPanel.Children.Add(closeButton);
        
        popup.Content = mainPanel;
        popup.ShowDialog();
    }

    private void DistTeacherCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DistTeacherCombo.SelectedItem is Teacher t)
        {
            RenderDistributionGrid(t.Id, true);
            
            // Update Header Info: Name, Hours, Guidance, Duty
            string guidance = "Rehberlik: Yok";
            if (t.Guidance != 0)
            {
                var cls = _allClasses.FirstOrDefault(c => c.Id == t.Guidance);
                if (cls != null) guidance = $"Rehberlik: {cls.Name}";
            }
            
            string duty = string.IsNullOrEmpty(t.DutyDay) ? "Nöbet: -" : $"Nöbet: {t.DutyDay}";
            int hours = t.TotalAssignedHours;
            
            DistTeacherHeaderInfo.Text = $"{t.Name} | {hours} Saat | {guidance} | {duty}";
        }
    }
    
    private void DistClassCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
         if (DistClassCombo.SelectedValue is int id) RenderDistributionGrid(id, false);
    }
    
    // Dictionary to track entity colors consistently
    private Dictionary<string, SolidColorBrush> _scheduleColorCache = new();
    
    private void CheckDistHideWeekend_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.SetBool("DistHideWeekend", CheckDistHideWeekend.IsChecked == true);
        RefreshDistribution_Click(null, null);
    }

    private void RenderDistributionGrid(int id, bool isTeacher)
    {
        // Target Panel
        StackPanel target = isTeacher ? DistTeacherGrid : DistClassGrid;
        target.Children.Clear();
        _scheduleColorCache.Clear();
        
        bool hideWeekend = CheckDistHideWeekend.IsChecked == true;
        
        var db = DatabaseManager.Shared;
        
        // Fetch blocks for marker logic (Multiple teacher icon)
        List<DistributionBlock> blocksForEntity = new();
        if (isTeacher)
        {
             blocksForEntity = new DistributionRepository().GetByTeacher(id);
        }

        // Fetch Schedule (d_1_1 ... d_7_12)
        string tableName = isTeacher ? "ogretmen" : "sinif";
        var rows = db.Query($"SELECT * FROM {tableName} WHERE id = {id}");
        if (rows.Count == 0) return;
        var row = rows[0];
        
        // Build Grid
        var mainGrid = new Grid { Background = ThemeManager.GetBrush("SurfaceBrush") };
        
        // Determine Columns
        int daysToShow = hideWeekend ? 5 : 7;
        
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) }); // Hour (Widened for times)
        for(int i=0; i<daysToShow; i++) mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        // Header
        string[] days = { "Saat", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
        var headerBg = ThemeManager.GetBrush("SurfaceAltBrush");
        var headerFg = ThemeManager.GetBrush("TextPrimaryBrush");
        
        int gridColIndex = 0;
        for(int i=0; i<8; i++)
        {
            if (hideWeekend && (i == 6 || i == 7)) continue; // Skip Sat/Sun
            
            var headerCell = new Border
            {
                Background = headerBg,
                Padding = new Thickness(4),
                Child = new TextBlock 
                { 
                    Text = days[i], 
                    FontWeight = FontWeights.SemiBold, 
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = headerFg
                }
            };
            Grid.SetColumn(headerCell, gridColIndex++);
            mainGrid.Children.Add(headerCell);
        }
        
        // Rows
        for (int h=1; h<=12; h++)
        {
             mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
             
             // Hour Label with Time
             var headerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
             headerStack.Children.Add(new TextBlock 
             { 
                 Text = h.ToString(), 
                 FontWeight = FontWeights.Bold, 
                 HorizontalAlignment = HorizontalAlignment.Center,
                 Foreground = headerFg
             });
            
             if (_schoolInfo != null && _schoolInfo.LessonHours != null && _schoolInfo.LessonHours.Length >= h)
             {
                 var timeStr = _schoolInfo.LessonHours[h - 1];
                 if (!string.IsNullOrEmpty(timeStr))
                 {
                      headerStack.Children.Add(new TextBlock 
                      { 
                          Text = timeStr, 
                          FontSize = 9, 
                          FontWeight = FontWeights.Normal,
                          Foreground = Brushes.DimGray, 
                          HorizontalAlignment = HorizontalAlignment.Center, 
                          Margin = new Thickness(0,2,0,0) 
                      });
                 }
             }

             var hourCell = new Border
             {
                 Background = headerBg,
                 Child = headerStack
             };
             Grid.SetRow(hourCell, h);
             Grid.SetColumn(hourCell, 0);
             mainGrid.Children.Add(hourCell);
             
             // Cells
             int gridCol = 1;
             for (int d=1; d<=7; d++)
             {
                 if (hideWeekend && (d == 6 || d == 7)) continue;
             
                 string val = DatabaseManager.GetString(row, $"d_{d}_{h}");
                 
                 var border = new Border 
                 { 
                     BorderBrush = ThemeManager.GetBrush("BorderBrush"),
                     BorderThickness = new Thickness(0.5),
                     Margin = new Thickness(1),
                     CornerRadius = new CornerRadius(4)
                 };
                 
                 // Handle KAPALI -> "--"
                 bool isClosed = val == "KAPALI";
                  // Handle Multiple Teachers Icon
                  string displayText = val;
                  if (isTeacher && !string.IsNullOrEmpty(val) && val != "--" && val != "KAPALI")
                  {
                      var block = blocksForEntity.FirstOrDefault(b => b.Day == d && b.Hour <= h && (b.Hour + b.BlockDuration) > h);
                      if (block != null && block.TeacherIds.Count > 1)
                      {
                          // "9-R    DERS" -> "9-R 👥    DERS"
                          if (val.Contains("    ")) displayText = val.Replace("    ", " 👥    ");
                          else displayText = val + " 👥";
                      }
                  }
                  
                  if (isClosed) displayText = "--";
                 
                 var txt = new TextBlock 
                 { 
                     Text = displayText, 
                     FontSize = 9, // Reduced from 10
                     TextWrapping = TextWrapping.Wrap, 
                     Margin = new Thickness(2),
                     VerticalAlignment = VerticalAlignment.Center,
                     HorizontalAlignment = HorizontalAlignment.Center,
                     TextAlignment = TextAlignment.Center
                 };
                 
                 // Style based on content
                 if (isClosed)
                 {
                     border.Background = ThemeManager.GetClosedSlotBrush();
                     txt.Foreground = ThemeManager.GetClosedSlotTextBrush();
                 }
                 else if (!string.IsNullOrEmpty(val))
                 {
                     // Color by entity
                     var colorKey = ExtractColorKey(val, isTeacher);
                     var bgBrush = GetOrCreateScheduleColor(colorKey);
                     border.Background = bgBrush;
                     txt.Foreground = ThemeManager.GetContrastTextColor(bgBrush.Color);
                     txt.FontWeight = FontWeights.Bold;
                 }
                 else
                 {
                     border.Background = ThemeManager.GetBrush("SurfaceBrush");
                     txt.Foreground = ThemeManager.GetBrush("TextMutedBrush");
                 }
                 
                  border.Child = txt;
                  
                  // Etkileşim için Tag ve Olayları ekle
                  border.Tag = new Tuple<int, int, bool>(d, h, isTeacher);
                  border.Cursor = Cursors.Hand;
                  border.MouseLeftButtonUp += OnDistributionCellClick;
                  if (isTeacher) border.MouseRightButtonUp += OnDistributionCellRightClick;
                  Grid.SetRow(border, h);
                  Grid.SetColumn(border, gridCol++);
                  mainGrid.Children.Add(border);
        }
              }
        
        mainGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });
        target.Children.Add(mainGrid);
    }
    
    /// <summary>
    /// Extract the color key from schedule cell value.
    /// For teacher view: color by class name
    /// For class view: color by teacher name
    /// </summary>
    private string ExtractColorKey(string cellValue, bool isTeacherView)
    {
        if (string.IsNullOrEmpty(cellValue)) return "";
        
        // Cell format might be "ClassName - LessonName" or "TeacherName\nLessonName" or just text
        // Try to extract first part as the key
        var parts = cellValue.Split(new[] { '\n', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            return parts[0].Trim();
        }
        return cellValue;
    }
    
    private void OnDistributionCellClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is Tuple<int, int, bool> tag)
        {
            int day = tag.Item1;
            int hour = tag.Item2;
            bool isTeacherView = tag.Item3;
            
            string val = "";
            if (border.Child is TextBlock tb) val = tb.Text;
            if (string.IsNullOrEmpty(val) || val == "--") return;

            // Take first part if multiple lines
            string firstLine = val.Split('\n')[0];

            if (isTeacherView)
            {
                // Teacher view value: "ClassName    LessonCode" (4 spaces)
                var parts = firstLine.Split(new[] { "    " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    string className = parts[0].Replace("👥", "").Trim();
                    // Find in DistClassCombo
                    foreach (var item in DistClassCombo.Items)
                    {
                        if (item is SchoolClass c && c.Name == className)
                        {
                            DistClassCombo.SelectedItem = c;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Class view value: "LessonCode - TeacherName1, TeacherName2"
                var parts = firstLine.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    string teacherNames = parts[1].Trim();
                    string firstTeacherName = teacherNames.Split(',')[0].Trim();
                    
                    // Find in DistTeacherCombo
                    foreach (var item in DistTeacherCombo.Items)
                    {
                        if (item is Teacher t && t.Name == firstTeacherName)
                        {
                            DistTeacherCombo.SelectedItem = t;
                            break;
                        }
                    }
                }
            }
        }
    }

    private void OnDistributionCellRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is Tuple<int, int, bool> tag)
        {
            int day = tag.Item1;
            int hour = tag.Item2;
            bool isTeacherView = tag.Item3;
            
            if (!isTeacherView) return;

            if (DistTeacherCombo.SelectedItem is Teacher currentTeacher)
            {
                var db = DatabaseManager.Shared;
                // Find block for this teacher at this time
                string sql = $@"SELECT * FROM dagitim_bloklari 
                                WHERE gun = {day} AND saat = {hour}
                                AND (ogretmen_1_id = {currentTeacher.Id} OR ogretmen_2_id = {currentTeacher.Id} 
                                     OR ogretmen_3_id = {currentTeacher.Id} OR ogretmen_4_id = {currentTeacher.Id} 
                                     OR ogretmen_5_id = {currentTeacher.Id})";
                
                var rows = db.Query(sql);
                if (rows.Count > 0)
                {
                    var block = rows[0];
                    var teacherIds = new List<int>();
                    for(int i=1; i<=5; i++) {
                        int tid = DatabaseManager.GetInt(block, $"ogretmen_{i}_id");
                        if (tid > 0) teacherIds.Add(tid);
                    }

                    if (teacherIds.Count > 0)
                    {
                        var tRows = db.Query($"SELECT id, ad_soyad FROM ogretmen WHERE id IN ({string.Join(",", teacherIds)})");
                        
                        var cm = new ContextMenu();
                        var header = new MenuItem { Header = "Bu Dersteki Öğretmenler:", IsEnabled = false, FontWeight = FontWeights.Bold };
                        cm.Items.Add(header);
                        cm.Items.Add(new Separator());

                        foreach (var tRow in tRows)
                        {
                            string tName = DatabaseManager.GetString(tRow, "ad_soyad");
                            int tId = DatabaseManager.GetInt(tRow, "id");
                            
                            var mi = new MenuItem { Header = tName };
                            
                            mi.Click += (s, ev) => {
                                // Switch to this teacher
                                foreach (var item in DistTeacherCombo.Items)
                                {
                                    if (item is Teacher t && t.Id == tId)
                                    {
                                        DistTeacherCombo.SelectedItem = item;
                                        break;
                                    }
                                }
                            };
                            cm.Items.Add(mi);
                        }
                        
                        border.ContextMenu = cm;
                        cm.IsOpen = true;
                    }
                }
            }
        }
    }

    private SolidColorBrush GetOrCreateScheduleColor(string key)
    {
        if (string.IsNullOrEmpty(key))
            return ThemeManager.GetBrush("SurfaceBrush");
            
        if (!_scheduleColorCache.ContainsKey(key))
        {
            _scheduleColorCache[key] = ThemeManager.GetScheduleColorByName(key);
        }
        return _scheduleColorCache[key];
    }
    
    // ==================== Classes Panel (Sınıf Müfredat ve Atama) ====================
    
    private List<SchoolClass> _allClasses = new();
    private List<Lesson> _allLessonsForClasses = new();
    private List<Teacher> _allTeachersForClasses = new();
    private int _selectedClassId = 0;
    private int _lastAssignedTeacherId = 0;
    private readonly ClassLessonRepository _classLessonRepo = new ClassLessonRepository();
    
    private void LoadClassesPanel()
    {
        var classRepo = new ClassRepository();
        var lessonRepo = new LessonRepository();
        var teacherRepo = new TeacherRepository();
        
        _allClasses = classRepo.GetAll().OrderBy(c => c.Name).ToList();
        _allLessonsForClasses = lessonRepo.GetAll().OrderBy(l => l.Name).ToList();
        _allTeachersForClasses = teacherRepo.GetAll().OrderBy(t => t.Name).ToList();
        _allOrtakMekans = new OrtakMekanRepository().GetAll();
        
        // Calculate total hours for each class
        var classViewModels = _allClasses.Select(c => new ClassViewModel
        {
            Id = c.Id,
            Name = c.Name,
            TotalHours = _classLessonRepo.GetTotalHoursForClass(c.Id),
            UnassignedCount = _classLessonRepo.GetUnassignedTeacherCount(c.Id)
        }).ToList();
        
        ClassesList.ItemsSource = classViewModels;
        
        // Restore selection
        if (_selectedClassId != 0)
        {
            ClassesList.SelectedValue = _selectedClassId;
        }
        
        // Load lesson pool (assigned lessons on top)
        RefreshLessonPool();
        
        // Only clear and show hint if no class is selected
        if (_selectedClassId == 0)
        {
            ClassLessonsContainer.Children.Clear();
            ClassAssignmentHint.Visibility = Visibility.Visible;
        }
    }

    private void RefreshLessonPool()
    {
        var classLessons = _selectedClassId > 0
            ? _classLessonRepo.GetClassLessons(_selectedClassId)
            : new List<ClassLesson>();
        var assignedIds = new HashSet<int>(classLessons.Select(cl => cl.LessonId));

        // Build lessonId -> teacher names lookup
        var teachersByLessonId = new Dictionary<int, List<string>>();
        foreach (var cl in classLessons)
        {
            var assignments = _classLessonRepo.GetTeacherAssignments(cl.Id);
            foreach (var a in assignments)
            {
                var teacher = _allTeachersForClasses.FirstOrDefault(t => t.Id == a.TeacherId);
                if (teacher != null)
                {
                    if (!teachersByLessonId.ContainsKey(cl.LessonId))
                        teachersByLessonId[cl.LessonId] = new List<string>();
                    var parts = teacher.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var shortName = parts.Length > 1
                        ? string.Join(" ", parts.Take(parts.Length - 1)) + " " + parts.Last()[0] + "."
                        : teacher.Name;
                    if (!teachersByLessonId[cl.LessonId].Contains(shortName))
                        teachersByLessonId[cl.LessonId].Add(shortName);
                }
            }
        }

        var query = ClassLessonSearchBox?.Text?.ToLower() ?? "";
        var source = string.IsNullOrWhiteSpace(query)
            ? _allLessonsForClasses
            : _allLessonsForClasses.Where(l => l.Name.ToLower().Contains(query) || l.Code.ToLower().Contains(query));

        var lessonViewModels = source
            .Select(l => new LessonViewModel
            {
                Id = l.Id,
                Name = l.Name,
                Code = l.Code,
                DefaultBlock = l.DefaultBlock,
                Initial = GetIconKindForLesson(l),
                IsAssigned = assignedIds.Contains(l.Id),
                AssignedTeachers = teachersByLessonId.ContainsKey(l.Id)
                    ? string.Join(", ", teachersByLessonId[l.Id])
                    : ""
            })
            .OrderByDescending(l => l.IsAssigned)
            .ThenBy(l => l.Name)
            .ToList();

        LessonPoolList.ItemsSource = lessonViewModels;
    }

    private void RefreshClassListBadges()
    {
        if (ClassesList.ItemsSource is List<ClassViewModel> list)
        {
            foreach (var vm in list)
                vm.UnassignedCount = _classLessonRepo.GetUnassignedTeacherCount(vm.Id);
            ClassesList.Items.Refresh();
        }
    }

    private void LessonPoolList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LessonPoolList.SelectedItem is LessonViewModel vm && vm.IsAssigned)
        {
            // Find the matching card in ClassLessonsContainer and scroll to it
            foreach (var child in ClassLessonsContainer.Children)
            {
                if (child is Border card && card.Tag is int lessonId && lessonId == vm.Id)
                {
                    card.BringIntoView();

                    // Flash highlight
                    var originalBrush = card.BorderBrush;
                    var originalThickness = card.BorderThickness;
                    card.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
                    card.BorderThickness = new Thickness(2);

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                    timer.Tick += (s, args) =>
                    {
                        card.BorderBrush = originalBrush;
                        card.BorderThickness = originalThickness;
                        timer.Stop();
                    };
                    timer.Start();
                    break;
                }
            }
        }
        LessonPoolList.SelectedItem = null;
    }

    private string GetIconKindForLesson(Lesson l)
    {
        string text = (l.Name).ToLowerInvariant();
        if (text.Contains("lab") || text.Contains("fizik") || text.Contains("kimya") || text.Contains("biyo") || text.Contains("fen")) return "\U0001F52C";
        if (text.Contains("spor") || text.Contains("beden") || text.Contains("salon") || text.Contains("gym")) return "\u26BD";
        if (text.Contains("müzik") || text.Contains("resim") || text.Contains("sanat") || text.Contains("görsel") || text.Contains("atölye")) return "\U0001F3A8";
        if (text.Contains("bilgisayar") || text.Contains("bilişim") || text.Contains("kodlama") || text.Contains("yazılım")) return "\U0001F4BB";
        if (text.Contains("teknoloji") || text.Contains("tasarÄ±m")) return "\U0001F6E0\uFE0F";
        
        return l.Name.Length > 0 ? l.Name.Substring(0, 1).ToUpper() : "?";
    }
    
    private void ClassesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClassesList.SelectedItem is ClassViewModel cls)
        {
            _selectedClassId = cls.Id;
            ClassAssignmentHint.Visibility = Visibility.Collapsed;
            LoadClassLessons();
            RefreshLessonPool();
            
            // NEW: Setup Class Timetable
            var schoolClass = _allClasses.FirstOrDefault(c => c.Id == cls.Id);
            if (schoolClass != null)
            {
                ClassTimetableArea.Visibility = Visibility.Visible;
                SetupClassTimetable(schoolClass);
            }
        }
    }


    private void PageLessonName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { PageLessonCode.Focus(); e.Handled = true; }
    }

    private void PageLessonCode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { PageLessonBlock.Focus(); e.Handled = true; }
    }

    private void PageLessonBlock_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { BtnQuickAddPageLesson.Focus(); e.Handled = true; }
    }

    private void BtnQuickAddPageLesson_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            QuickAddPageLesson_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void QuickAddPageLesson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PageLessonName.Text))
            {
                MessageBox.Show("Lütfen ders adını giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                PageLessonName.Focus();
                return;
            }

            var lesson = new Lesson
            {
                Name = PageLessonName.Text.Trim(),
                Code = PageLessonCode.Text.Trim(),
                DefaultBlock = string.IsNullOrWhiteSpace(PageLessonBlock.Text) ? "2" : PageLessonBlock.Text,
            };

            new LessonRepository().Save(lesson);

            // Clear search to show new lesson in alphabetical order
            if (LessonSearchBox != null) LessonSearchBox.Text = "";

            // Refresh main lesson list
            LoadLessons("");

            // Sync with Classes tab: update _allLessonsForClasses and refresh its pool
            LoadClassesPanel();

            // Clear input fields and focus
            PageLessonName.Clear();
            PageLessonCode.Clear();
            PageLessonBlock.Text = "2";

            PageLessonName.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ders ekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CheckClassHideWeekend_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.SetBool("ClassHideWeekend", CheckClassHideWeekend.IsChecked == true);
        if (_selectedClassId != 0)
        {
            var schoolClass = _allClasses.FirstOrDefault(c => c.Id == _selectedClassId);
            if (schoolClass != null) SetupClassTimetable(schoolClass);
        }
    }

    private void SetupClassTimetable(SchoolClass schoolClass)
    {
        ClassTimetableGrid.Children.Clear();
        
        bool hideWeekend = CheckClassHideWeekend.IsChecked == true;
        
        // Header
        var headerGrid = new Grid();
        string[] days = { "Ders", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
        int colIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (hideWeekend && (i == 6 || i == 7)) continue;
            
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 0 ? new GridLength(65) : new GridLength(1, GridUnitType.Star) });
            var tb = new TextBlock { Text = days[i], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(5), FontSize = 11 };
            Grid.SetColumn(tb, colIndex++);
            headerGrid.Children.Add(tb);
        }
        ClassTimetableGrid.Children.Add(headerGrid);
        
        // Rows
        for (int hour = 1; hour <= 12; hour++)
        {
            var rowGrid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            colIndex = 0;
            if (hideWeekend)
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });
                for (int i = 0; i < 5; i++) rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            else
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });
                for (int i = 0; i < 7; i++) rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            
            // Hour Label
            var headerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            headerStack.Children.Add(new TextBlock { Text = hour.ToString(), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 10 });
            
            if (_schoolInfo != null && _schoolInfo.LessonHours != null && _schoolInfo.LessonHours.Length >= hour)
            {
                 var timeStr = _schoolInfo.LessonHours[hour-1];
                 if (!string.IsNullOrEmpty(timeStr))
                 {
                      headerStack.Children.Add(new TextBlock { Text = timeStr, FontSize = 8, Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center });
                 }
            }

            var hourLabel = new Border { Background = ThemeManager.GetBrush("SurfaceAltBrush"), CornerRadius = new CornerRadius(4), Child = headerStack };
            Grid.SetColumn(hourLabel, 0);
            rowGrid.Children.Add(hourLabel);
            
            // Day Cells
            int currentGridCol = 1;
            for (int day = 1; day <= 7; day++)
            {
                if (hideWeekend && (day == 6 || day == 7)) continue;

                int h = hour;
                int d = day;
                var slot = new TimeSlot(d, h);
                var scheduleKey = $"d_{d}_{h}";
                
                // Priority: 1. Hard constraints (Constraints dict), 2. Redundant database field (Schedule dict)
                bool isClosed = false;
                if (schoolClass.Constraints.TryGetValue(slot, out var constraintState))
                {
                    isClosed = constraintState == SlotState.Closed;
                }
                else if (schoolClass.Schedule != null && schoolClass.Schedule.TryGetValue(scheduleKey, out var val))
                {
                    isClosed = val.Trim().Equals("KAPALI", StringComparison.OrdinalIgnoreCase);
                }
                
                var btn = new Button
                {
                    Tag = slot, // Store slot in Tag for absolute safety
                    Background = isClosed ? ThemeManager.GetClosedSlotBrush() : Brushes.White,
                    BorderBrush = ThemeManager.GetBrush("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    Height = 30,
                    Margin = new Thickness(1),
                    Cursor = Cursors.Hand
                };
                
                // --- UI Display Logic ---
                // Priority 1: If there's lesson content, show it
                if (schoolClass.Schedule != null && schoolClass.Schedule.TryGetValue(scheduleKey, out var valContent) && !string.IsNullOrEmpty(valContent) && valContent != "KAPALI")
                {
                    var textBlock = new TextBlock 
                    { 
                        Text = valContent, 
                        FontWeight = FontWeights.SemiBold, 
                        FontSize = 9, 
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };
                    btn.Content = textBlock;
                    
                    // Pastel Background
                    int colorIndex = Math.Abs(valContent.GetHashCode()) % ThemeManager.LightPastelColors.Count;
                    btn.Background = ThemeManager.LightPastelColors[colorIndex];
                }
                // Priority 2: If it's closed and empty, show "--"
                else if (isClosed)
                {
                    btn.Content = new TextBlock { Text = "--", FontWeight = FontWeights.Bold, Foreground = ThemeManager.GetClosedSlotTextBrush(), HorizontalAlignment = HorizontalAlignment.Center };
                    btn.Background = ThemeManager.GetClosedSlotBrush();
                }
                
                btn.PreviewMouseDoubleClick += (s, args) => 
                {
                    if (s is Button b && b.Tag is TimeSlot ts)
                    {
                        if (schoolClass.Constraints.ContainsKey(ts))
                            schoolClass.Constraints.Remove(ts);
                        else
                            schoolClass.Constraints[ts] = SlotState.Closed;
                        
                        // Sync memory Schedule to avoid fallback issues
                        string key = $"d_{ts.Day}_{ts.Hour}";
                        if (schoolClass.Schedule != null)
                        {
                            schoolClass.Schedule[key] = schoolClass.Constraints.ContainsKey(ts) ? "KAPALI" : "";
                        }
                        
                        // Save the entire class object to ensure all tables (sinif and zaman_tablosu) are synced
                        new ClassRepository().Save(schoolClass);
                        
                        SetupClassTimetable(schoolClass);
                    }
                    args.Handled = true;
                };
                
                Grid.SetColumn(btn, currentGridCol++);
                rowGrid.Children.Add(btn);
            }
            ClassTimetableGrid.Children.Add(rowGrid);
        }
    }
    
    private void LoadClassLessons()
    {
        if (_selectedClassId == 0) return;
        
        // Load Common Areas
        try { _allOrtakMekans = new OrtakMekanRepository().GetAll(); } catch { _allOrtakMekans = new List<OrtakMekan>(); }

        ClassLessonsContainer.Children.Clear();
        
        var classLessonsRaw = _classLessonRepo.GetClassLessons(_selectedClassId);
        // Sort by Lesson Name
        var classLessons = classLessonsRaw
            .Select(cl => new { Item = cl, LessonName = _allLessonsForClasses.FirstOrDefault(l => l.Id == cl.LessonId)?.Name ?? "" })
            .OrderBy(x => x.LessonName)
            .Select(x => x.Item)
            .ToList();
        
        foreach (var cl in classLessons)
        {
            var lesson = _allLessonsForClasses.FirstOrDefault(l => l.Id == cl.LessonId);
            if (lesson == null) continue;
            
            var assignments = _classLessonRepo.GetTeacherAssignments(cl.Id);
            
            // Create card for this class-lesson
            var card = CreateClassLessonCard(cl, lesson, assignments);
            ClassLessonsContainer.Children.Add(card);
        }
    }
    
    private Border CreateClassLessonCard(ClassLesson cl, Lesson lesson, List<TeacherAssignment> assignments)
    {
        var card = new Border
        {
            Background = ThemeManager.GetBrush("SurfaceBrush"),
            BorderBrush = ThemeManager.GetBrush("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10),
            Tag = cl.LessonId
        };
        
        var stack = new StackPanel();
        
        // Header
        var header = new DockPanel();
        
        // 1. Delete Button (Right)
        var deleteBtn = new Button 
        { 
            Content = "\U0001F5D1",
            Tag = cl.Id,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(5, 0, 0, 0)
        };
        deleteBtn.Click += DeleteClassLesson_Click;
        DockPanel.SetDock(deleteBtn, Dock.Right);
        header.Children.Add(deleteBtn);

        // 2. Hours Text (Right)
        var hoursText = new TextBlock 
        { 
            Text = $"{cl.TotalHours} Saat", 
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(hoursText, Dock.Right);
        header.Children.Add(hoursText);
        
        // 3. Name and Block (Left/Fill)
        var nameStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(new TextBlock 
        { 
            Text = lesson.Name, 
            FontWeight = FontWeights.SemiBold, 
            FontSize = 14 
        });

        // Add Lesson Code
        var codeBorder = new Border
        {
            Background = ThemeManager.GetBrush("SurfaceAltBrush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        codeBorder.Child = new TextBlock
        {
            Text = lesson.Code,
            FontSize = 10,
            Foreground = ThemeManager.GetBrush("TextSecondaryBrush"),
            FontWeight = FontWeights.Bold
        };
        nameStack.Children.Add(codeBorder);
        
        nameStack.Children.Add(new TextBlock 
        { 
            Text = $"Blok: {lesson.DefaultBlock}", 
            FontSize = 11, 
            Foreground = Brushes.Gray,
            Margin = new Thickness(8, 2, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        // ğŸ”— Unified Lesson Icon
        if (cl.KardesId > 0)
        {
            nameStack.Children.Add(new TextBlock 
            { 
                Text = " \U0001F517", 
                FontSize = 14, 
                Foreground = new SolidColorBrush(Color.FromRgb(249, 115, 22)), // Orange-500
                ToolTip = "Bu ders birleştirilmiş (Kardeş Ders) grubuna dahildir.",
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        
        // Ortak Mekan removed from header header.Children.Add(nameStack);

        header.Children.Add(nameStack);
        
        stack.Children.Add(header);
        
        // Spacer
        stack.Children.Add(new Border { Height = 4 });
        
        // Teacher assignments
        foreach (var assignment in assignments)
        {
            var teacher = _allTeachersForClasses.FirstOrDefault(t => t.Id == assignment.TeacherId);
            var teacherRow = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
            
            teacherRow.Children.Add(new TextBlock { Text = "\u251C\u2500 \U0001F464 " });
            
            var teacherCombo = new ComboBox 
            { 
                Width = 200,
                Tag = assignment.Id
            };
            foreach (var t in _allTeachersForClasses)
                teacherCombo.Items.Add(new ComboBoxItem { Content = t.Name, Tag = t.Id });
            
            // Select current teacher
            foreach (ComboBoxItem item in teacherCombo.Items)
            {
                if ((int)item.Tag == assignment.TeacherId)
                {
                    teacherCombo.SelectedItem = item;
                    break;
                }
            }
            teacherCombo.SelectionChanged += TeacherCombo_SelectionChanged;
            teacherRow.Children.Add(teacherCombo);
            
            // Per-Teacher Room Combo (Ortak Mekan)
            var roomCombo = new ComboBox 
            { 
                Width = 140,
                Margin = new Thickness(5, 0, 5, 0),
                Tag = new HelperTuple(cl.Id, assignment.TeacherId) // Custom helper or simple object
            };
            
            roomCombo.Items.Add(new ComboBoxItem { Content = "--- Mekan Yok ---", Tag = 0 });
            
            if (_allOrtakMekans != null)
            {
                foreach(var m in _allOrtakMekans)
                    roomCombo.Items.Add(new ComboBoxItem { Content = m.Name, Tag = m.Id });
            }
            
            // Select current room
            int currentRoomId = GetOrtakMekanIdForTeacher(cl.Id, assignment.TeacherId);
            bool foundRoom = false;
            foreach(ComboBoxItem item in roomCombo.Items)
            {
                if (item.Tag is int id && id == currentRoomId)
                {
                    roomCombo.SelectedItem = item;
                    foundRoom = true;
                    break;
                }
            }
            if (!foundRoom) roomCombo.SelectedIndex = 0;
            
            roomCombo.SelectionChanged += OrtakMekanCombo_SelectionChanged_New;
            teacherRow.Children.Add(roomCombo);
            
            var removeTeacherBtn = new Button 
            { 
                Content = "Sil", 
                Tag = assignment.Id,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.Gray,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            removeTeacherBtn.Click += RemoveTeacher_Click;
            DockPanel.SetDock(removeTeacherBtn, Dock.Right);
            teacherRow.Children.Add(removeTeacherBtn);
            
            stack.Children.Add(teacherRow);
        }
        
        // Add teacher button
        var addTeacherBtn = new Button 
        { 
            Content = "+ Öğretmen Ekle",
            Tag = cl.Id,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = ThemeManager.GetBrush("PrimaryBrush"),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 2, 0, 0),
            FontSize = 11
        };
        addTeacherBtn.Click += AddTeacherToLesson_Click;
        stack.Children.Add(addTeacherBtn);
        
        card.Child = stack;
        return card;
    }
    
    private void AddLessonToClass_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedClassId == 0)
        {
            MessageBox.Show("Lütfen önce bir sınıf seçin.", "Uyarı");
            return;
        }
        
        object? tag = null;
        if (sender is Button btn) tag = btn.Tag;
        else if (sender is MenuItem mi) tag = mi.Tag;

        if (tag != null)
        {
            int lessonId = 0;
            if (tag is int id)
            {
                lessonId = id;
            }
            else if (int.TryParse(tag.ToString(), out int parsedId))
            {
                lessonId = parsedId;
            }
            
            if (lessonId == 0) return;
            
            var lesson = _allLessonsForClasses.FirstOrDefault(l => l.Id == lessonId);
            if (lesson == null) return;
            
            // Calculate hours from block structure
            int hours = lesson.DefaultBlock.Split('+')
                .Select(p => int.TryParse(p.Trim(), out int v) ? v : 0)
                .Sum();
            if (hours == 0) hours = 2;
            
            var classLessonId = _classLessonRepo.AddLessonToClass(_selectedClassId, lessonId, hours);
            
            // Auto-assign teacher if enabled
            if (CheckAutoAssignTeacher.IsChecked == true && _lastAssignedTeacherId > 0 && classLessonId.HasValue)
            {
                _classLessonRepo.AddTeacherAssignment(classLessonId.Value, _lastAssignedTeacherId, hours);
            }
            
            LoadClassLessons();
            LoadClassesPanel(); // Refresh class list to update hours
        }
    }
    
    private void AddTeacherToLesson_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int classLessonId)
        {
            // Get last assigned teacher or first teacher as default
            var teacherId = _lastAssignedTeacherId;
            if (teacherId == 0)
            {
                var firstTeacher = _allTeachersForClasses.FirstOrDefault();
                if (firstTeacher == null)
                {
                    MessageBox.Show("Sistemde öğretmen bulunamadı.", "Uyarı");
                    return;
                }
                teacherId = firstTeacher.Id;
            }
            
            // Get class lesson hours
            var classLessons = _classLessonRepo.GetByClassId(_selectedClassId);
            var cl = classLessons.FirstOrDefault(c => c.Id == classLessonId);
            int hours = cl?.TotalHours ?? 2;
            
            _classLessonRepo.AddTeacherAssignment(classLessonId, teacherId, hours);

            if (teacherId > 0) _lastAssignedTeacherId = teacherId;

            LoadClassLessons();
            RefreshClassListBadges();
            RefreshLessonPool();
        }
    }
    
    private void TeacherRoom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Legacy room assignment - rooms are now managed via dagitim_bloklari.ortak_mekan_1_id
        /*
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item && combo.Tag is int assignmentId)
        {
            int roomId = (int)item.Tag;
            
            var assignments = new Persistence.AssignmentRepository().GetAll(); 
            var current = assignments.FirstOrDefault(a => a.Id == assignmentId);
            
            if (current != null)
            {
                _classLessonRepo.UpdateTeacherAssignment(current.Id, current.TeacherId, current.AssignedHours);
                UpdateStats();
            }
        }
        */
    }



    private void TeacherCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.Tag is int assignmentId)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is int teacherId)
            {
                var classLessons = _classLessonRepo.GetByClassId(_selectedClassId);
                // Find assignment to get hours AND CURRENT ROOM
                foreach (var cl in classLessons)
                {
                    var assignments = _classLessonRepo.GetTeacherAssignments(cl.Id);
                    var assignment = assignments.FirstOrDefault(a => a.Id == assignmentId);
                    if (assignment != null)
                    {
                        _classLessonRepo.UpdateTeacherAssignment(assignmentId, teacherId, assignment.AssignedHours);
                        if (teacherId > 0) _lastAssignedTeacherId = teacherId;
                        RefreshClassListBadges();
                        RefreshLessonPool();
                        break;
                    }
                }
            }
        }
    }
    
    private int GetOrtakMekanIdForClassLesson(int classLessonId)
    {
        try
        {
            var db = DatabaseManager.Shared;
            var result = db.Query($"SELECT ortak_mekan_1_id FROM dagitim_bloklari WHERE sinif_ders_id = {classLessonId} LIMIT 1");
            if (result.Count > 0)
                return DatabaseManager.GetInt(result[0], "ortak_mekan_1_id");
        }
        catch { }
        return 0;
    }
    

    
    // Helper Class for tagging
    private class HelperTuple
    {
        public int CID;
        public int TID;
        public HelperTuple(int c, int t) { CID = c; TID = t; }
    }

    private void OrtakMekanCombo_SelectionChanged_New(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.Tag is HelperTuple info)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is int roomId)
            {
                SetOrtakMekanIdForTeacher(info.CID, info.TID, roomId);
            }
        }
    }

    private int GetOrtakMekanIdForTeacher(int classLessonId, int teacherId)
    {
        try
        {
            var db = DatabaseManager.Shared;
            var result = db.Query($"SELECT * FROM dagitim_bloklari WHERE sinif_ders_id = {classLessonId} LIMIT 1");
            if (result.Count > 0)
            {
                var row = result[0];
                for (int i = 1; i <= 7; i++)
                {
                    if (DatabaseManager.GetInt(row, $"ogretmen_{i}_id") == teacherId)
                    {
                        return DatabaseManager.GetInt(row, $"ortak_mekan_{i}_id");
                    }
                }
            }
        }
        catch { }
        return 0;
    }

    private void SetOrtakMekanIdForTeacher(int classLessonId, int teacherId, int roomId)
    {
        try
        {
            var db = DatabaseManager.Shared;
            for (int i = 1; i <= 7; i++)
            {
                db.Execute($"UPDATE dagitim_bloklari SET ortak_mekan_{i}_id = {roomId} WHERE sinif_ders_id = {classLessonId} AND ogretmen_{i}_id = {teacherId}");
            }
        }
        catch { }
    }
    
    private void RemoveTeacher_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int assignmentId)
        {
            _classLessonRepo.RemoveTeacherAssignment(assignmentId);
            LoadClassLessons();
            LoadClassesPanel();
        }
    }
    
    private void DeleteClassLesson_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int classLessonId)
        {
            var result = MessageBox.Show("Bu dersi Sınıf Müfredatından silmek istediğinizden emin misiniz?", 
                "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _classLessonRepo.RemoveLessonFromClass(classLessonId);
                LoadClassLessons();
                LoadClassesPanel();
            }
        }
    }
    
    private void CopyClassLessons_Click(object sender, RoutedEventArgs e)
    {
        if (ClassesList.SelectedItem is ClassViewModel cls)
        {
             var lessons = _classLessonRepo.GetByClassId(cls.Id);
             if (lessons.Count == 0)
             {
                 MessageBox.Show("Bu sınıfta kopyalanacak ders bulunamadı.", "Uyarı");
                 return;
             }
             _copiedClassLessons = lessons.Select(l => (l.LessonId, l.TotalHours)).ToList();
             // MessageBox.Show($"{cls.Name} sınıfına ait {lessons.Count} ders kopyalandı.", "Bilgi");
        }
    }

    private void PasteClassLessons_Click(object sender, RoutedEventArgs e)
    {
        if (_copiedClassLessons == null || _copiedClassLessons.Count == 0)
        {
             MessageBox.Show("Önce bir sınıftan dersleri kopyalayın.", "Bilgi");
             return;
        }

        if (ClassesList.SelectedItem is ClassViewModel cls)
        {
             try
             {
                 foreach(var item in _copiedClassLessons)
                 {
                     _classLessonRepo.AddLessonToClass(cls.Id, item.LessonId, item.Hours);
                 }
                 
                 // Refresh
                 if (_selectedClassId == cls.Id) LoadClassLessons();
                 LoadClassesPanel();
             }
             catch(Exception ex)
             {
                 MessageBox.Show($"Yapıştırma sırasında hata oluştu: {ex.Message}", "Hata");
             }
        }
    }

    private void AddClass_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Yeni Sınıf Ekle",
            Width = 300,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };
        
        var stack = new StackPanel { Margin = new Thickness(15) };
        stack.Children.Add(new TextBlock { Text = "Sınıf Adı (Örn: 9-A):" });
        var textBox = new TextBox { Margin = new Thickness(0, 5, 0, 15) };
        stack.Children.Add(textBox);
        
        var saveBtn = new Button { Content = "Kaydet", Width = 80 };
        saveBtn.Click += (s, args) =>
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                var classRepo = new ClassRepository();
                var allClasses = classRepo.GetAll();
                int nextId = (allClasses.Count > 0 ? allClasses.Max(c => c.Id) : 0) + 1;
                
                classRepo.Save(new SchoolClass { Id = nextId, Name = textBox.Text });
                dialog.Close();
                LoadClassesPanel();
            }
        };
        stack.Children.Add(saveBtn);
        
        dialog.Content = stack;
        dialog.ShowDialog();
    }
    
    private void DeleteClass_Click(object sender, RoutedEventArgs e)
    {
        if (ClassesList.SelectedItem is ClassViewModel cls)
        {
            var result = MessageBox.Show($"'{cls.Name}' sınıfını ve bu sınıfa ait tüm ders atamalarını, dağıtım bloklarını ve kısıtları silmek istediğinizden emin misiniz?", 
                "Sınıfı Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                var classRepo = new ClassRepository();
                classRepo.Delete(cls.Id);
                
                if (_selectedClassId == cls.Id)
                {
                    _selectedClassId = 0;
                }
                
                LoadClassesPanel();
            }
        }
    }
    
    private void ClassLessonSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshLessonPool();
    }

    private void Report_TeacherSchedule_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string docNo = ReportDocumentNo.Text;
            string date = ReportDate.Text;
            if (string.IsNullOrEmpty(date)) date = DateTime.Now.ToString("dd.MM.yyyy");

            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateTeacherSchedule(docNo, date);
            
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Öğretmen El Programı");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Report_TeacherDailySheet_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateTeacherDailySchedule();
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Öğretmen Günlük Ders Programı");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_ClassSchedule_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string docNo = ReportDocumentNo.Text;
            string date = ReportDate.Text;
            if (string.IsNullOrEmpty(date)) date = DateTime.Now.ToString("dd.MM.yyyy");

            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateClassSchedule(docNo, date);
            
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Sınıf Ders Programı");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Report_TeacherDaily_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateTeacherMasterSchedule();
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Öğretmen Master (Çarşaf) Programı");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_ClassDaily_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateClassMasterSchedule();
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Sınıf Master (Çarşaf) Programı");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_DutyList_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string docNo = ReportDocumentNo.Text;
            string date = ReportDate.Text;
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateDutySchedule(docNo, date);
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Nöbet Çizelgesi");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_RoomSchedule_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string docNo = ReportDocumentNo.Text;
            string date = ReportDate.Text;
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateRoomSchedule(docNo, date);
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Ortak Mekan Programı");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Report_Guidance_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateGuidanceReport();
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Rehber Öğretmenler Listesi");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_Clubs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateClubsReport();
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Eğitsel Kulüp Listesi");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_Unassigned_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateUnassignedReport();
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Yerleşmeyen Dersler Listesi");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_Assignments_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateAssignmentsReport();
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Ders Atama Listesi");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_JointTeachers_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var schoolRepo = new Persistence.SchoolRepository();
            var info = schoolRepo.GetSchoolInfo();
            var generator = new Services.ReportGenerator(info);
            string html = generator.GenerateJointTeacherReport();
            var viewer = new Views.ReportViewerWindow(html);
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_ElectiveRatio_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var generator = new DersDagitim.Services.PdfReportGenerator();
            byte[] pdfBytes = generator.GenerateElectiveRatioReport();
            var viewer = new Views.ReportViewerWindow(pdfBytes, "Seçmeli/Zorunlu Ders Oranı");
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void Report_JointSpace_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var schoolRepo = new DersDagitim.Persistence.SchoolRepository();
            var info = schoolRepo.GetSchoolInfo();
            var generator = new DersDagitim.Services.ReportGenerator(info);
            
            string html = generator.GenerateJointSpaceScheduleReport(ReportDate.Text);
            
            var viewer = new Views.ReportViewerWindow(html);
            viewer.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor oluşturulurken hata oluştu: {ex.Message}", "Hata");
        }
    }

    private void ShowReport(string html)
    {
        try
        {
            var viewer = new ReportViewerWindow(html);
            viewer.Owner = this;
            
            // Positioning: Top-aligned with 40px margin, centered horizontally
            viewer.WindowStartupLocation = WindowStartupLocation.Manual;
            viewer.Left = this.Left + (this.ActualWidth - viewer.Width) / 2;
            viewer.Top = this.Top + 40;
            
            viewer.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rapor görüntülenirken hata: {ex.Message}", "Hata");
        }
    }

    private void Report_EOkulExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var teacherRepo = new TeacherRepository();
            var teachers = teacherRepo.GetAll();
            
            var sb = new StringBuilder();
            sb.AppendLine("Sınıf,Ders,Öğretmen,Gün,Saat"); // Common format headers
            
            foreach (var teacher in teachers)
            {
                if (teacher.ScheduleInfo == null) continue;
                foreach (var kvp in teacher.ScheduleInfo)
                {
                    var slot = kvp.Key;
                    var content = kvp.Value;
                    
                    if (string.IsNullOrEmpty(content)) continue;
                    
                    // Split content (ClassName    LessonCode)
                    var parts = content.Split(new[] { "    " }, StringSplitOptions.RemoveEmptyEntries);
                    string className = parts.Length > 0 ? parts[0] : "";
                    string lessonName = parts.Length > 1 ? parts[1] : "";
                    
                    string dayName = slot.Day switch {
                        1 => "Pazartesi", 2 => "Salı", 3 => "Çarşamba", 4 => "Perşembe", 5 => "Cuma", 
                        6 => "Cumartesi", 7 => "Pazar", _ => ""
                    };
                    
                    // Format: 10-A,MAT,Ahmet Yılmaz,Pazartesi,1
                    sb.AppendLine($"{className},{lessonName},{teacher.Name},{dayName},{slot.Hour}");
                }
            }
            
            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "CSV Dosyası (*.csv)|*.csv";
            saveDialog.FileName = $"eOkul_Aktarim_{DateTime.Now:yyyyMMdd}.csv";
            
            if (saveDialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("e-Okul aktarım dosyası başarıyla oluşturuldu!", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Aktarım dosyası oluşturulurken hata:\n{ex.Message}", "Hata");
        }
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // if (_schoolInfo == null) return; // No longer needed for password

            string current = CurrentPwInput.Password;
            string @new = NewPwInput.Password;
            string repeat = RepeatPwInput.Password;
            
            string savedPw = SettingsManager.Get("AppPassword", "12345");

            if (current != savedPw)
            {
                MessageBox.Show("Mevcut şifre hatalı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(@new))
            {
                MessageBox.Show("Yeni şifre boş olamaz!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (@new != repeat)
            {
                MessageBox.Show("Yeni şifreler uyuşmuyor!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // _schoolInfo.EntryPassword = @new; // Deprecated
            SettingsManager.Set("AppPassword", @new);
            SettingsManager.SetBool("AppPasswordActive", CheckEnableLoginPassword.IsChecked == true);
            
            CurrentPwInput.Clear();
            NewPwInput.Clear();
            RepeatPwInput.Clear();
            
            MessageBox.Show("Åifre başarıyla değiştirildi (Sabit Veritabanına Kaydedildi).", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Åifre değiştirme hatası:\n{ex.Message}", "Hata");
        }
    }

    private void CheckEnableLoginPassword_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = CheckEnableLoginPassword.IsChecked == true;
        SettingsManager.SetBool("AppPasswordActive", isActive);
    }

    private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9]+");
        e.Handled = regex.IsMatch(e.Text);
    }

    // ==================== MANUEL DAÄITIM (ELLE PROGRAMLAMA) ====================

    private List<DistributionBlock> _allPlacedBlocks = new(); 
    private Teacher? _selectedManualTeacher;
    private Dictionary<TimeSlot, SlotState>? _currentDragClassConstraints; // Cache for drag operations
    private List<(int LessonId, int Hours)>? _copiedClassLessons; // Clipboard for class lessons

    private void Nav_Manual(object sender, RoutedEventArgs e)
    {
        ShowPanel("ManualDistribution");
        SetActiveNavButton(NavBtnScheduling);
        
        // Cache all blocks for conflict checking
        try 
        {
            var repo = new DistributionRepository();
            _allPlacedBlocks = repo.GetAllBlocks().Where(b => b.Day > 0 && b.Hour > 0).ToList();
        } 
        catch { _allPlacedBlocks = new List<DistributionBlock>(); }

        LoadManualTeachers();
    }

    private void LoadManualTeachers()
    {
        try
        {
            var teachers = new TeacherRepository().GetAll().OrderBy(t => t.Name).ToList();
            var manualTeachers = new List<ManualTeacherItem>();
            
            // Get stats for each teacher to show in dropdown
            var distRepo = new DistributionRepository(); 
            // Note: Efficient way would be one big query, but for <100 teachers this is fine
            
            foreach(var t in teachers)
            {
                var tBlocks = distRepo.GetByTeacher(t.Id);
                int placed = tBlocks.Count(b => b.Day > 0);
                manualTeachers.Add(new ManualTeacherItem 
                { 
                    Id = t.Id, 
                    Name = t.Name, 
                    PlacedCount = placed 
                });
            }
            
            ComboManualTeacher.ItemsSource = manualTeachers;
            
            if (manualTeachers.Count > 0)
                ComboManualTeacher.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Öğretmenler yüklenirken hata: {ex.Message}");
        }
    }

    private void ComboManualTeacher_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboManualTeacher.SelectedItem is ManualTeacherItem item)
        {
            _selectedManualTeacher = new TeacherRepository().GetById(item.Id);
            LoadManualTeacherStats();
            LoadManualBlocks();
            LoadManualSchedule();
        }
    }

    private void LoadManualTeacherStats()
    {
        if (_selectedManualTeacher == null) return;
        
        var blocks = new DistributionRepository().GetByTeacher(_selectedManualTeacher.Id);
        int total = blocks.Sum(b => b.BlockDuration);
        int placed = blocks.Where(b => b.Day > 0).Sum(b => b.BlockDuration);
        int remaining = total - placed;
        
        TxtManualTotalLoad.Text = $"{total} Saat";
        TxtManualPlacedLoad.Text = $"{placed} Saat";
        TxtManualRemainingLoad.Text = $"{remaining} Saat";
    }

    private void LoadManualBlocks()
    {
        if (_selectedManualTeacher == null) return;
        
        var repo = new DistributionRepository();
        var blocks = repo.GetByTeacher(_selectedManualTeacher.Id); // All blocks
        
        // Filter: Only UNPLACED blocks (Day=0 or Hour=0)
        var unplaced = blocks.Where(b => b.Day == 0 || b.Hour == 0).ToList();
        
        var listItems = new List<ManualBlockItem>();
        
        // Cache lessons for matching
        var lessonRepo = new LessonRepository();
        var allLessons = lessonRepo.GetAll();
        
        var classRepo = new ClassRepository();
            
        foreach(var b in unplaced)
        {
            var l = allLessons.FirstOrDefault(x => x.Code == b.LessonCode);
            var c = classRepo.GetById(b.ClassId);
            
            listItems.Add(new ManualBlockItem
            {
                BlockId = b.Id,
                LessonCode = l?.Code ?? b.LessonCode,
                LessonName = l?.Name ?? "??",
                ClassName = c?.Name ?? "??",
                Duration = b.BlockDuration,
                RawBlock = b
            });
        }
        
        ListManualBlocks.ItemsSource = listItems;
    }

    private void LoadManualSchedule()
    {
        ManualScheduleGrid.Children.Clear();
        ManualScheduleGrid.RowDefinitions.Clear();
        ManualScheduleGrid.ColumnDefinitions.Clear();
        
        if (_selectedManualTeacher == null) return;

        // Build Grid (Headers + Slots)
        // Columns: Saat + Days (Pzt..Cum) -> 6 Columns (or more if weekend)
        bool includeWeekend = !SettingsManager.GetBool("DistHideWeekend", false);
        int days = includeWeekend ? 7 : 5;
        
        // Col Defs
        ManualScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // Hour
        for(int i=0; i<days; i++) ManualScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Row Defs (Header + 8..10 hours)
        int hours = 10; // Default max hours, could be dynamic
        ManualScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        for(int i=0; i<hours; i++) ManualScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Headers
        string[] dNames = { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
        for(int i=0; i<days; i++)
        {
             var txt = new TextBlock { Text = dNames[i], FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(5) };
             Grid.SetColumn(txt, i + 1);
             ManualScheduleGrid.Children.Add(txt);
        }

        // Get Placed Blocks for this teacher to render
        var repo = new DistributionRepository();
        var placedBlocks = repo.GetByTeacher(_selectedManualTeacher.Id).Where(b => b.Day > 0 && b.Hour > 0).ToList();
        
        var lessonRepo = new LessonRepository();
        var allLessons = lessonRepo.GetAll(); // Cache lessons
        
        var classRepo = new ClassRepository();

        // Cells
        for(int h=1; h<=hours; h++)
        {
            // Hour Label
            var hTxt = new TextBlock { Text = $"{h}. Ders", VerticalAlignment=VerticalAlignment.Center, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,0,5,0), FontSize=10, Foreground=Brushes.Gray };
            Grid.SetRow(hTxt, h);
            Grid.SetColumn(hTxt, 0);
            ManualScheduleGrid.Children.Add(hTxt);

            for(int d=1; d<=days; d++)
            {
                var border = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0.5),
                    Background = Brushes.White,
                    AllowDrop = true,
                    Tag = new Point(d, h) // Store coordinates
                };
                
                // Events
                border.DragOver += ManualScheduleCell_DragOver;
                border.Drop += ManualScheduleCell_Drop;

                // Content (if block exists)
                var block = placedBlocks.FirstOrDefault(b => b.Day == d && b.Hour == h); // Assuming duration=1 for simplicity in rendering validation, but blocks can be multi-hour
                // Better check: Is this cell inside any block?
                var coveringBlock = placedBlocks.FirstOrDefault(b => b.Day == d && h >= b.Hour && h < b.Hour + b.BlockDuration);

                if (coveringBlock != null)
                {
                    // If it's the start of the block, render info. If middle, render extension.
                    bool isStart = coveringBlock.Hour == h;
                    var l = allLessons.FirstOrDefault(x => x.Code == coveringBlock.LessonCode);
                    var c = classRepo.GetById(coveringBlock.ClassId);

                    border.Background = coveringBlock.IsLocked ? new SolidColorBrush(Color.FromRgb(220, 252, 231)) : ThemeManager.GetBrush("SurfaceAltBrush"); // Green tint if locked
                    
                    if (isStart)
                    {
                        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                        st.Children.Add(new TextBlock { Text = l?.Code ?? coveringBlock.LessonCode, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, FontSize=11, Foreground = Brushes.Black });
                        st.Children.Add(new TextBlock { Text = c?.Name ?? "?", HorizontalAlignment = HorizontalAlignment.Center, FontSize = 10, Foreground = Brushes.DarkSlateGray });
                        if(coveringBlock.IsLocked)
                             st.Children.Add(new TextBlock { Text = "\U0001F512", HorizontalAlignment = HorizontalAlignment.Center, FontSize = 9 });

                        // Drag from Schedule Logic? (Maybe later. User asked to drag FROM list TO schedule)
                        // If we want to allow moving existing blocks, we attach mouse down here. 
                        // For now, let's keep it simple: Drag from list only. 
                        // To remove a block, maybe right click?
                        border.ContextMenu = new ContextMenu();
                        var mi = new MenuItem { Header = "Yerleşimi Kaldır", Tag = coveringBlock };
                        mi.Click += (s, e) => RemoveManualBlock((DistributionBlock)((MenuItem)s).Tag);
                        border.ContextMenu.Items.Add(mi);

                        border.Child = st;
                        
                        // Span rows if needed
                        if (coveringBlock.BlockDuration > 1)
                        {
                            Grid.SetRowSpan(border, coveringBlock.BlockDuration);
                        }
                    }
                    else
                    {
                        // Middle of a block, handled by RowSpan of the start block usually. 
                        // If using Grid, we don't add child to covered cells if RowSpan is used.
                        continue; 
                    }
                }

                Grid.SetRow(border, h);
                Grid.SetColumn(border, d);
                ManualScheduleGrid.Children.Add(border);
            }
        }
    }

    private void RemoveManualBlock(DistributionBlock block)
    {
        var repo = new DistributionRepository();
        repo.ClearBlock(block.Id); // Removes placement
        
        // Refresh
        // Update cached blocks
        _allPlacedBlocks.RemoveAll(b => b.Id == block.Id); 
        
        LoadManualTeacherStats();
        LoadManualBlocks();
        LoadManualSchedule();
    }

    // --- Drag & Drop ---

    private void ManualBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is ManualBlockItem item)
        {
             // Cache Class constraints for performance
             _currentDragClassConstraints = new ClassRepository().GetById(item.RawBlock.ClassId)?.Constraints;
            
             // Colorize Grid for feedback
             foreach(UIElement child in ManualScheduleGrid.Children)
             {
                 if (child is Border cell && cell.Tag is Point pos)
                 {
                     int d = (int)pos.X;
                     int h = (int)pos.Y;
                     bool isValid = CheckPlacementValidity(item.RawBlock, d, h);
                     
                     if (!isValid)
                         cell.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // Red-100 (Invalid)
                     else
                         cell.Background = Brushes.White; // Valid
                 }
             }

             DragDrop.DoDragDrop(border, item, DragDropEffects.Move);
             
             // Clear Cache
             _currentDragClassConstraints = null;
             
             // Reset Grid logic after drag ends (dropped or cancelled)
             LoadManualSchedule();
        }
    }

    private void ManualBlock_MouseMove(object sender, MouseEventArgs e)
    {
        // Handled in PreviewMouseLeftButtonDown usually for Lists
    }

    private void ManualScheduleCell_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ManualBlockItem)) is ManualBlockItem item && sender is Border cell)
        {
            var pos = (Point)cell.Tag;
            int day = (int)pos.X;
            int hour = (int)pos.Y;

            // Check Validity only for Cursor Effect, NO Color Change
            bool isValid = CheckPlacementValidity(item.RawBlock, day, hour);

            if (!isValid)
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Move;
            }
            
            e.Handled = true;
        }
    }

    private void ManualScheduleCell_DragLeave(object sender, DragEventArgs e)
    {
         // No logic needed as we don't change colors during DragOver anymore.
    }

    private void ManualScheduleCell_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ManualBlockItem)) is ManualBlockItem item && sender is Border cell)
        {
            var pos = (Point)cell.Tag;
            int day = (int)pos.X;
            int hour = (int)pos.Y;

            if (CheckPlacementValidity(item.RawBlock, day, hour))
            {
                // PLACE IT
                var repo = new DistributionRepository();
                var block = item.RawBlock;
                block.Day = day;
                block.Hour = hour;
                block.IsLocked = true; // KİLİTLE
                block.IsManual = true; // MANUEL İÅARETLE ('manuel' column in DB?) 
                block.PlacementType = "manuel"; // Type

                // We need to update DB
                repo.PlaceBlock(block, "manuel");
                // Also update the 'kilitli' field directly if PlaceBlock doesn't (PlaceBlock updates 'yerlesim_tipi', but IsLocked needs specific update?)
                // DistributionRepository.PlaceBlock updates everything if block object has it? 
                // Let's check: PlaceBlock query uses block.Day/Hour. Need to ensure Kilitli is updated.
                // Assuming PlaceBlock might NOT update 'IsLocked' or 'kilitli' column explicitly!
                // Let's force update IsLocked manually to be safe.
                string sql = $"UPDATE dagitim_bloklari SET kilitli = 1, manuel = 1 WHERE id = {block.Id}";
                DatabaseManager.Shared.Execute(sql);

                // Update Cache
                _allPlacedBlocks.Add(block);

                // Refresh UI
                LoadManualTeacherStats();
                LoadManualBlocks();
                LoadManualSchedule();
            }
            else
            {
                MessageBox.Show("Bu yerleşim yapılamaz! Sınıf veya öğretmenin çakışması var.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                cell.Background = Brushes.White;
            }
        }
    }


    private bool CheckPlacementValidity(DistributionBlock block, int day, int hour)
    {
        // Check ALL slots required by duration
        for (int i = 0; i < block.BlockDuration; i++)
        {
            int checkHour = hour + i;
            if (checkHour > 12) return false; // Out of bounds for this day

            // 1. Check Teacher Busy
            if (_allPlacedBlocks.Any(b => b.TeacherIds.Contains(_selectedManualTeacher.Id) && b.Day == day && 
                                          b.Hour <= checkHour && (b.Hour + b.BlockDuration) > checkHour))
            {
                return false; // Teacher busy
            }
    
            // 2. Check Class Busy
            if (_allPlacedBlocks.Any(b => b.ClassId == block.ClassId && b.Day == day && 
                                          b.Hour <= checkHour && (b.Hour + b.BlockDuration) > checkHour))
            {
                return false; // Class busy
            }
            
            // 3. Check Teacher Constraints
            if (_selectedManualTeacher != null && _selectedManualTeacher.Constraints != null)
            {
                var slot = new TimeSlot(day, checkHour);
                if (_selectedManualTeacher.Constraints.TryGetValue(slot, out var state) && state == SlotState.Closed)
                {
                    return false; // Teacher closed
                }
            }
            
            // 4. Check Class Constraints
            if (_currentDragClassConstraints != null)
            {
                var slot = new TimeSlot(day, checkHour);
                if (_currentDragClassConstraints.TryGetValue(slot, out var state) && state == SlotState.Closed)
                {
                    return false; // Class closed
                }
            }

            // 5. Check Room (Ortak Mekan) Conflicts
            var blockRooms = block.GetOrtakMekanIds();
            if (blockRooms.Count > 0)
            {
                if (_allPlacedBlocks.Any(b => b.Id != block.Id && b.Day == day &&
                    b.Hour <= checkHour && (b.Hour + b.BlockDuration) > checkHour &&
                    b.GetOrtakMekanIds().Intersect(blockRooms).Any()))
                {
                    return false; // Room conflict
                }
            }

            // 6. Check Kardes (Sibling) Block Conflicts - all teachers of sibling must be free
            if (block.KardesId > 0)
            {
                var kardesBlocks = _allPlacedBlocks.Where(b => b.KardesId == block.KardesId && b.Id != block.Id).ToList();
                foreach (var kb in kardesBlocks)
                {
                    foreach (var tid in kb.TeacherIds)
                    {
                        if (_allPlacedBlocks.Any(b => b.Id != kb.Id && b.TeacherIds.Contains(tid) && b.Day == day &&
                            b.Hour <= checkHour && (b.Hour + b.BlockDuration) > checkHour))
                        {
                            return false; // Kardes teacher busy
                        }
                    }
                }
            }
        }

        return true;
    }
    
    // Save button not strictly needed if we save on Drop, but keeps User happy
    private void BtnSaveManual_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Değişiklikler anlık olarak kaydedilmiştir.", "Bilgi");
    }


    private async void ValidateDatabase_Click(object sender, MouseButtonEventArgs e)
    {
        if (MessageBox.Show("Veritabanındaki blokların bütünlüğü, eksik dersler ve atama senkronizasyonu kontrol edilecek.\n\nDevam etmek istiyor musunuz?", "Veri Kontrolü", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var progressWindow = new Views.DistributionProgressWindow();
        progressWindow.Owner = this;
        progressWindow.Title = "Veri Kontrolü";
        progressWindow.Show();

        progressWindow.UpdateStatus("Veri kontrolü başlatılıyor...");

        List<string> logs = new();

        try
        {
            await Task.Run(() =>
            {
                var repo = new DistributionRepository();
                logs = repo.ValidateAndFixDatabase(msg =>
                {
                    progressWindow.UpdateStatus(msg);
                });
            });

            // Log dosyasına kaydet
            string logPath = System.IO.Path.Combine(ConfigManager.Shared.DataDirectory, "db_repair_log.txt");
            System.IO.File.WriteAllLines(logPath, logs);

            int issueCount = logs.Count(l => l.Contains("ÇAKIŞMA") || l.Contains("HATA") || l.Contains("KRİTİK"));
            int repairCount = logs.Count(l => l.Contains("ONARILDI") || l.Contains("DÜZELTİLDİ"));

            if (issueCount > 0)
                progressWindow.UpdateStatus($"Tamamlandı. {issueCount} sorun bulundu, {repairCount} onarım yapıldı.");
            else
                progressWindow.UpdateStatus("Tamamlandı. Sorun bulunamadı.");
        }
        catch (Exception ex)
        {
            progressWindow.SetError(ex.Message);
        }
    }
}

public class ManualTeacherItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int PlacedCount { get; set; }
    
    public string DisplayName => $"{Name} ({PlacedCount})";
}

public class ManualBlockItem
{
    public int BlockId { get; set; }
    public string LessonCode { get; set; } = "";
    public string LessonName { get; set; } = "";
    public string ClassName { get; set; } = "";
    public int Duration { get; set; }
    public DistributionBlock RawBlock { get; set; }
    
    public string DurationInfo => $"{Duration} Saat";
    public string DisplayCode => $"{LessonName} {LessonCode} {Duration}";
}

    // View models for data binding
public class ClassViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int TotalHours { get; set; }
    public int UnassignedCount { get; set; }
    public bool HasUnassigned => UnassignedCount > 0;
}

public class LessonViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ShortName => Name.Length > 17 ? Name.Substring(0, 17) + "..." : Name;
    public string Code { get; set; } = "";
    public string DefaultBlock { get; set; } = "";
    public string Initial { get; set; } = "";
    public string IconKind { get; set; } = "Class";
    public bool IsAssigned { get; set; }
    public string AssignedTeachers { get; set; } = "";
    public bool HasTeachers => !string.IsNullOrEmpty(AssignedTeachers);
    public bool HasNoTeacher => IsAssigned && !HasTeachers;
}

