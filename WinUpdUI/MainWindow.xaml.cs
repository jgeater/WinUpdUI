using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Security.Principal;
using System.Globalization;

namespace WinUpdUI
{
    public partial class MainWindow : Window
    {
        private void ShowDetailedError(Exception ex, string context)
        {
            string fullError = $"{context}\n\n" +
                              $"Message: {ex.Message}\n\n" +
                              $"Exception Type: {ex.GetType().FullName}\n\n" +
                              $"Stack Trace:\n{ex.StackTrace}";

            if (ex.InnerException != null)
            {
                fullError += $"\n\n=== Inner Exception ===\n" +
                             $"Message: {ex.InnerException.Message}\n" +
                             $"Type: {ex.InnerException.GetType().FullName}\n" +
                             $"Stack: {ex.InnerException.StackTrace}";
            }

            MessageBox.Show(fullError, "Detailed Error Information",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
        }

        private void InitializeApp()
        {
            CheckAdminPrivileges();
        }

        private void CheckAdminPrivileges()
        {
            bool isAdmin = IsAdministrator();
            if (isAdmin)
            {
                AdminStatusText.Text = "✓ Running with Administrator Privileges";
                AdminStatusText.Foreground = new SolidColorBrush(Colors.LightGreen);
            }
            else
            {
                AdminStatusText.Text = "⚠ Not running as Administrator - Some features may be limited";
                AdminStatusText.Foreground = new SolidColorBrush(Colors.Yellow);
            }
        }

        private bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is UIElement element && !e.Handled)
            {
                e.Handled = true;
                var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent
                };
                element.RaiseEvent(args);
            }
        }

        private void NavigationTab_Checked(object sender, RoutedEventArgs e)
        {
            // Guard against event firing during InitializeComponent before panels are created
            if (DiagnosticsPanel == null || UpdatesPanel == null || PendingPanel == null || 
                HistoryPanel == null || ConfigPanel == null || SettingsPanel == null)
                return;

            if (sender is RadioButton radioButton)
            {
                DiagnosticsPanel.Visibility = Visibility.Collapsed;
                UpdatesPanel.Visibility = Visibility.Collapsed;
                PendingPanel.Visibility = Visibility.Collapsed;
                HistoryPanel.Visibility = Visibility.Collapsed;
                ConfigPanel.Visibility = Visibility.Collapsed;
                SettingsPanel.Visibility = Visibility.Collapsed;

                if (radioButton == DiagnosticsTab)
                    DiagnosticsPanel.Visibility = Visibility.Visible;
                else if (radioButton == UpdatesTab)
                    UpdatesPanel.Visibility = Visibility.Visible;
                else if (radioButton == PendingTab)
                    PendingPanel.Visibility = Visibility.Visible;
                else if (radioButton == HistoryTab)
                    HistoryPanel.Visibility = Visibility.Visible;
                else if (radioButton == ConfigTab)
                    ConfigPanel.Visibility = Visibility.Visible;
                else if (radioButton == SettingsTab)
                {
                    SettingsPanel.Visibility = Visibility.Visible;
                    LoadThemePreference();  // Sync UI with current theme setting
                }
            }
        }

        private async void RunDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading("Running diagnostics...");

            try
            {
                var results = await Task.Run(() =>
                {
                    var diagnostics = new WindowsUpdateDiagnostics();
                    diagnostics.RunDiagnostics();
                    return diagnostics.GetResults();
                });

                var displayResults = results.Select(r => new DiagnosticDisplayItem
                {
                    CheckName = r.CheckName,
                    Message = r.Message,
                    Recommendation = string.IsNullOrEmpty(r.Recommendation) ? "" : r.Recommendation,
                    HasRecommendation = !string.IsNullOrEmpty(r.Recommendation),
                    StatusText = r.Status.ToString(),
                    StatusColor = GetStatusColor(r.Status)
                }).ToList();

                DiagnosticsListView.ItemsSource = displayResults;
                DiagnosticsResultsCard.Visibility = Visibility.Visible;

                UpdateStatus($"Diagnostics completed - {results.Count} checks performed");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running diagnostics: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading("Checking for updates...");

            try
            {
                var updates = await Task.Run(() =>
                {
                    var manager = new WindowsUpdateManager();
                    return manager.GetAvailableUpdates(true);
                });

                var displayUpdates = updates.Select(u => new UpdateDisplayItem
                {
                    Title = u.Title,
                    KBText = u.KBArticleIDs?.Any() == true ? $"KB: {string.Join(", ", u.KBArticleIDs)}" : "No KB available",
                    SizeText = $"Size: {FormatBytes((long)u.MaxDownloadSize)}",
                    MainCategory = u.Categories?.FirstOrDefault() ?? "Update",
                    CategoryColor = GetCategoryColor(u.Categories?.FirstOrDefault()),
                    DownloadStatusText = u.IsDownloaded ? "Downloaded" : "Not Downloaded",
                    DownloadStatusColor = u.IsDownloaded ? FindResource("SuccessBrush") as Brush : FindResource("WarningBrush") as Brush,
                    ShowDownloadStatus = true
                }).ToList();

                UpdatesListView.ItemsSource = displayUpdates;
                UpdatesCountText.Text = $"Found {updates.Count} available update(s)";
                UpdatesResultsCard.Visibility = Visibility.Visible;

                UpdateStatus($"Found {updates.Count} available updates");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private async void CheckPending_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading("Checking pending updates...");

            try
            {
                var updates = await Task.Run(() =>
                {
                    var manager = new WindowsUpdateManager();
                    return manager.GetPendingUpdates();
                });

                var displayUpdates = updates.Select(u => new UpdateDisplayItem
                {
                    Title = u.Title,
                    KBText = u.KBArticleIDs?.Any() == true ? $"KB: {string.Join(", ", u.KBArticleIDs)}" : "No KB available"
                }).ToList();

                PendingListView.ItemsSource = displayUpdates;
                PendingCountText.Text = updates.Count == 0 ? "No pending updates" : $"{updates.Count} pending update(s) ready to install";
                PendingResultsCard.Visibility = Visibility.Visible;

                UpdateStatus($"Found {updates.Count} pending updates");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking pending updates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private async void LoadHistory_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading("Loading update history...");

            try
            {
                var history = await Task.Run(() =>
                {
                    var manager = new WindowsUpdateManager();
                    return manager.GetUpdateHistory(40);
                });

                // Deduplicate by KB number (or title+date if no KB)
                var seen = new HashSet<string>();
                var deduplicatedHistory = new List<dynamic>();

                foreach (var h in history)
                {
                    var key = ExtractKBFromTitle(h.Title);
                    if (string.IsNullOrEmpty(key))
                        key = $"{h.Title}_{h.Date:yyyyMMdd}";

                    if (seen.Add(key))
                        deduplicatedHistory.Add(h);
                }

                // Separate into Defender and non-Defender updates
                var defenderUpdates = deduplicatedHistory
                    .Where(h => h.Title.IndexOf("Defender", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(10)
                    .ToList();

                var nonDefenderUpdates = deduplicatedHistory
                    .Where(h => h.Title.IndexOf("Defender", StringComparison.OrdinalIgnoreCase) < 0)
                    .Take(10)
                    .ToList();

                var displayHistory = new List<HistoryDisplayItem>();

                // Add non-Defender updates first
                if (nonDefenderUpdates.Count > 0)
                {
                    displayHistory.Add(new HistoryDisplayItem { IsHeader = true, Title = "Other Updates" });
                    displayHistory.AddRange(nonDefenderUpdates.Select(h => new HistoryDisplayItem
                    {
                        Title = h.Title,
                        DateText = h.Date.ToString("yyyy-MM-dd HH:mm"),
                        KBText = ExtractKBFromTitle(h.Title),
                        ResultText = h.ResultCode,
                        ResultColor = GetResultColor(h.ResultCode)
                    }));
                }

                // Add Defender updates second
                if (defenderUpdates.Count > 0)
                {
                    displayHistory.Add(new HistoryDisplayItem { IsHeader = true, Title = "Windows Defender Updates" });
                    displayHistory.AddRange(defenderUpdates.Select(h => new HistoryDisplayItem
                    {
                        Title = h.Title,
                        DateText = h.Date.ToString("yyyy-MM-dd HH:mm"),
                        KBText = ExtractKBFromTitle(h.Title),
                        ResultText = h.ResultCode,
                        ResultColor = GetResultColor(h.ResultCode)
                    }));
                }

                HistoryListView.ItemsSource = displayHistory;
                HistoryResultsCard.Visibility = Visibility.Visible;

                UpdateStatus($"Loaded {deduplicatedHistory.Count} unique history entries ({nonDefenderUpdates.Count} other, {defenderUpdates.Count} Defender)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading history: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private async void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading("Loading configuration...");

            try
            {
                var config = await Task.Run(() => WindowsUpdateConfiguration.GetConfiguration());

                ConfigContentPanel.Children.Clear();

                AddConfigSection("Update Server Settings", new Dictionary<string, string>
                {
                    { "WU Server", config.WUServer ?? "Not configured" },
                    { "WU Status Server", config.WUStatusServer ?? "Not configured" },
                    { "Use WU Server", config.UseWUServer.ToString() },
                    { "Target Group", config.TargetGroup ?? "Not configured" }
                });

                AddConfigSection("Auto Update Settings", new Dictionary<string, string>
                {
                    { "Auto Update Option", config.AutoUpdateOption ?? "Not configured" },
                    { "No Auto Update", config.NoAutoUpdate.ToString() },
                    { "Scheduled Install Day", GetDayName(config.ScheduledInstallDay) },
                    { "Scheduled Install Time", $"{config.ScheduledInstallTime}:00" }
                });

                AddConfigSection("Last Update Information", new Dictionary<string, string>
                {
                    { "Last Success Time", config.LastSuccessTime ?? "Unknown" },
                    { "Last Search Success Time", config.LastSearchSuccessTime ?? "Unknown" }
                });

                if (config.ServiceStatus?.Any() == true)
                {
                    var serviceDict = new Dictionary<string, string>();
                    foreach (var status in config.ServiceStatus)
                    {
                        serviceDict[status] = "";
                    }
                    AddConfigSection("Service Status", serviceDict);
                }

                if (config.MDMPolicies?.Any() == true)
                {
                    var policyDict = new Dictionary<string, string>();
                    foreach (var policy in config.MDMPolicies)
                    {
                        policyDict[policy] = "";
                    }
                    AddConfigSection("MDM Policies", policyDict);
                }

                UpdateStatus("Configuration loaded");
            }
            catch (Exception ex)
            {
                ShowDetailedError(ex, "Error loading configuration");
            }
            finally
            {
                HideLoading();
            }
        }

        private void AddConfigSection(string title, Dictionary<string, string> items)
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            var header = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = FindResource("TextPrimaryBrush") as Brush
            };
            section.Children.Add(header);

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Value))
                {
                    var singleLine = new TextBlock
                    {
                        Text = $"• {item.Key}",
                        Margin = new Thickness(10, 3, 0, 3),
                        TextWrapping = TextWrapping.Wrap
                    };
                    singleLine.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    section.Children.Add(singleLine);
                }
                else
                {
                    var grid = new Grid { Margin = new Thickness(10, 3, 0, 3) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var keyText = new TextBlock
                    {
                        Text = item.Key + ":",
                        FontWeight = FontWeights.Medium
                    };
                    keyText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    Grid.SetColumn(keyText, 0);
                    grid.Children.Add(keyText);

                    var valueText = new TextBlock
                    {
                        Text = item.Value,
                        TextWrapping = TextWrapping.Wrap,
                    };
                    valueText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                    Grid.SetColumn(valueText, 1);
                    grid.Children.Add(valueText);

                    section.Children.Add(grid);
                }
            }

            // Wrap each section in a card border, same as other panels
            var card = new Border
            {
                Style = FindResource("CardStyle") as Style,
                Margin = new Thickness(0, 0, 0, 15),
                Child = section
            };

            ConfigContentPanel.Children.Add(card);
        }

        private void ShowLoading(string message)
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;

            // Fade in overlay
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            LoadingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Scale up card
            var scaleX = new System.Windows.Media.Animation.DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.3 }
            };
            var scaleY = new System.Windows.Media.Animation.DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.3 }
            };
            LoadingCardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleX);
            LoadingCardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleY);

            // Spin hourglass continuously
            var spin = new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(1200))
            {
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            HourglassRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spin);
        }

        private void HideLoading()
        {
            // Stop spinning
            HourglassRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);

            // Fade out overlay
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) => LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
            LastUpdateText.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
            ShowToast(message);
        }

        private async void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastNotification.Visibility = Visibility.Visible;

            // Fade in
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
            ToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Slide up from bottom
            var slideIn = new System.Windows.Media.Animation.DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(250));
            slideIn.EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            var translateTransform = new System.Windows.Media.TranslateTransform();
            ToastNotification.RenderTransform = translateTransform;
            translateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideIn);

            // Hold
            await Task.Delay(2500);

            // Fade out
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            fadeOut.Completed += (s, e) => ToastNotification.Visibility = Visibility.Collapsed;
            ToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private Brush GetStatusColor(DiagnosticStatus status)
        {
            switch (status)
            {
                case DiagnosticStatus.Pass:
                    return FindResource("SuccessBrush") as Brush;
                case DiagnosticStatus.Warning:
                    return FindResource("WarningBrush") as Brush;
                case DiagnosticStatus.Error:
                    return FindResource("ErrorBrush") as Brush;
                default:
                    return FindResource("TextSecondaryBrush") as Brush;
            }
        }

        private Brush GetCategoryColor(string category)
        {
            if (string.IsNullOrEmpty(category))
                return FindResource("PrimaryBrush") as Brush;

            if (category.ToLower().Contains("security"))
                return FindResource("ErrorBrush") as Brush;
            else if (category.ToLower().Contains("driver"))
                return FindResource("AccentBrush") as Brush;
            else if (category.ToLower().Contains("definition"))
                return FindResource("WarningBrush") as Brush;
            else
                return FindResource("PrimaryBrush") as Brush;
        }

        private Brush GetResultColor(string resultCode)
        {
            if (string.IsNullOrEmpty(resultCode))
                return FindResource("TextSecondaryBrush") as Brush;

            resultCode = resultCode.ToLower();
            if (resultCode.Contains("succeed") || resultCode.Contains("success"))
                return FindResource("SuccessBrush") as Brush;
            else if (resultCode.Contains("fail"))
                return FindResource("ErrorBrush") as Brush;
            else if (resultCode.Contains("abort"))
                return FindResource("WarningBrush") as Brush;
            else
                return FindResource("TextSecondaryBrush") as Brush;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string GetDayName(int day)
        {
            if (day == 0) return "Every day";
            if (day >= 1 && day <= 7)
                return new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }[day];
            return "Not configured";
        }

        private string ExtractKBFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "";

            var match = System.Text.RegularExpressions.Regex.Match(title, @"KB\d+");
            return match.Success ? match.Value : "";
        }

        private bool _isLoadingThemePreference = false;

        private void ThemeRadio_Changed(object sender, RoutedEventArgs e)
        {
            // Don't save when we're just loading the preference
            if (_isLoadingThemePreference)
                return;

            if (ThemeSystemRadio.IsChecked == true)
            {
                App.SaveThemePreference("System");
                var app = Application.Current as App;
                app?.LoadSystemTheme();
            }
            else if (ThemeLightRadio.IsChecked == true)
            {
                App.SaveThemePreference("Light");
                var app = Application.Current as App;
                app?.ApplyTheme(SystemTheme.Light);
            }
            else if (ThemeDarkRadio.IsChecked == true)
            {
                App.SaveThemePreference("Dark");
                var app = Application.Current as App;
                app?.ApplyTheme(SystemTheme.Dark);
            }
        }

        private void LoadThemePreference()
        {
            try
            {
                _isLoadingThemePreference = true;

                var themePref = App.LoadThemePreference() ?? "System";

                ThemeSystemRadio.IsChecked = (themePref == "System");
                ThemeLightRadio.IsChecked = (themePref == "Light");
                ThemeDarkRadio.IsChecked = (themePref == "Dark");
            }
            finally
            {
                _isLoadingThemePreference = false;
            }
        }

        private void SaveThemePreference(string theme)
        {
            App.SaveThemePreference(theme);
        }
    }

    public class DiagnosticDisplayItem
    {
        public string CheckName { get; set; }
        public string Message { get; set; }
        public string Recommendation { get; set; }
        public bool HasRecommendation { get; set; }
        public string StatusText { get; set; }
        public Brush StatusColor { get; set; }
    }

    public class UpdateDisplayItem
    {
        public string Title { get; set; }
        public string KBText { get; set; }
        public string SizeText { get; set; }
        public string MainCategory { get; set; }
        public Brush CategoryColor { get; set; }
        public string DownloadStatusText { get; set; }
        public Brush DownloadStatusColor { get; set; }
        public bool ShowDownloadStatus { get; set; }
    }

    public class HistoryDisplayItem
    {
        public bool IsHeader { get; set; }
        public string Title { get; set; }
        public string DateText { get; set; }
        public string KBText { get; set; }
        public string ResultText { get; set; }
        public Brush ResultColor { get; set; }
    }
}
