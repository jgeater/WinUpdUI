using Microsoft.Win32;
using System;
using System.Windows;

namespace WinUpdUI
{
    public partial class App : Application
    {
        private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string RegistryValueName = "AppsUseLightTheme";

        private SystemTheme _currentTheme = SystemTheme.Light;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _currentTheme = GetWindowsTheme();
            ApplyTheme(_currentTheme);

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            base.OnExit(e);
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
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
    }

    public enum SystemTheme { Light, Dark }
}
