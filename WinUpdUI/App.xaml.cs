using Microsoft.Win32;
using System;
using System.Windows;

namespace WinUpdUI
{
    public partial class App : Application
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string RegistryValueName = "AppsUseLightTheme";
        private const string WinUpdUIRegKeyPath = @"Software\WinUpdUI";
        private const string ThemePreferenceValueName = "ThemePreference";

        private SystemTheme _currentTheme = SystemTheme.Light;
        private string _userThemePreference = null; // null = use system, "Light" or "Dark" = override

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load user preference (if any)
            _userThemePreference = LoadThemePreference();

            // Apply the appropriate theme
            if (_userThemePreference == "Light")
                ApplyTheme(SystemTheme.Light);
            else if (_userThemePreference == "Dark")
                ApplyTheme(SystemTheme.Dark);
            else
            {
                // Use system theme
                _currentTheme = GetWindowsTheme();
                ApplyTheme(_currentTheme);
            }

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            base.OnExit(e);
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // Only react to OS theme changes if user hasn't overridden with a preference
            if (_userThemePreference == null && e.Category == UserPreferenceCategory.General)
            {
                var newTheme = GetWindowsTheme();
                if (newTheme != _currentTheme)
                {
                    _currentTheme = newTheme;
                    Dispatcher.Invoke(() => ApplyTheme(_currentTheme));
                }
            }
        }

        public void ApplyTheme(SystemTheme theme)
        {
            var uri = theme == SystemTheme.Dark
                ? new Uri("pack://application:,,,/Themes/Dark.xaml", UriKind.Absolute)
                : new Uri("pack://application:,,,/Themes/Light.xaml", UriKind.Absolute);

            // Always replace index 0 (the theme slot) rather than comparing URIs
            Resources.MergedDictionaries[0] = new ResourceDictionary { Source = uri };
        }

        public static SystemTheme GetWindowsTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(RegistryValueName);
                        // 0 = dark mode, 1 = light mode
                        if (value is int intVal && intVal == 0)
                            return SystemTheme.Dark;
                    }
                }
            }
            catch { }

            return SystemTheme.Light;
        }

        public static void SaveThemePreference(string preference)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(WinUpdUIRegKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue(ThemePreferenceValueName, preference, Microsoft.Win32.RegistryValueKind.String);
                    }
                }
            }
            catch { }
        }

        public static string LoadThemePreference()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(WinUpdUIRegKeyPath))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(ThemePreferenceValueName);
                        if (value is string strVal && (strVal == "Light" || strVal == "Dark" || strVal == "System"))
                            return strVal == "System" ? null : strVal;
                    }
                }
            }
            catch { }

            return null; // default: use system theme
        }

        public void LoadSystemTheme()
        {
            _userThemePreference = null;
            _currentTheme = GetWindowsTheme();
            ApplyTheme(_currentTheme);
        }
    }

    public enum SystemTheme { Light, Dark }
}
