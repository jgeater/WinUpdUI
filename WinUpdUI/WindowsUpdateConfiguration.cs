using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace WinUpdUI
{
    /// <summary>
    /// Retrieves Windows Update configuration settings
    /// </summary>
    public class WindowsUpdateConfiguration
    {
        public string AutoUpdateOption { get; set; }
        public bool AutoUpdateEnabled { get; set; }
        public int ScheduledInstallDay { get; set; }
        public int ScheduledInstallTime { get; set; }
        public bool UseWUServer { get; set; }
        public string WUServer { get; set; }
        public string WUStatusServer { get; set; }
        public string TargetGroup { get; set; }
        public bool NoAutoUpdate { get; set; }
        public bool ElevateNonAdmins { get; set; }
        public string LastSuccessTime { get; set; }
        public string LastSearchSuccessTime { get; set; }
        public List<string> ServiceStatus { get; set; }
        public List<RegistryKeyInfo> CheckedRegistryKeys { get; set; }
        public List<string> MDMPolicies { get; set; }
        public bool HasMDMPolicies { get; set; }

        public static WindowsUpdateConfiguration GetConfiguration()
        {
            var config = new WindowsUpdateConfiguration
            {
                ServiceStatus = new List<string>(),
                CheckedRegistryKeys = new List<RegistryKeyInfo>(),
                MDMPolicies = new List<string>()
            };

            try
            {
                config.GetRegistrySettings();
                config.GetServiceStatus();
                config.GetWUASettings();
                config.GetMDMPolicies();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting configuration: {ex.Message}");
            }

            return config;
        }

        private void GetRegistrySettings()
        {
            try
            {
                // Check Windows Update Policy settings
                string keyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(keyPath))
                {
                    var keyInfo = new RegistryKeyInfo
                    {
                        Path = $@"HKLM\{keyPath}",
                        Exists = key != null
                    };

                    if (key != null)
                    {
                        WUServer = key.GetValue("WUServer") as string;
                        WUStatusServer = key.GetValue("WUStatusServer") as string;
                        TargetGroup = key.GetValue("TargetGroup") as string;
                        ElevateNonAdmins = Convert.ToBoolean(key.GetValue("ElevateNonAdmins", 0));

                        keyInfo.Values.Add($"WUServer = {WUServer ?? "(not set)"}");
                        keyInfo.Values.Add($"WUStatusServer = {WUStatusServer ?? "(not set)"}");
                        keyInfo.Values.Add($"TargetGroup = {TargetGroup ?? "(not set)"}");
                        keyInfo.Values.Add($"ElevateNonAdmins = {ElevateNonAdmins}");
                    }

                    CheckedRegistryKeys.Add(keyInfo);
                }

                // Check Windows Update Auto Update Policy settings
                keyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(keyPath))
                {
                    var keyInfo = new RegistryKeyInfo
                    {
                        Path = $@"HKLM\{keyPath}",
                        Exists = key != null
                    };

                    if (key != null)
                    {
                        var auOption = key.GetValue("AUOptions");
                        if (auOption != null)
                        {
                            AutoUpdateOption = GetAutoUpdateOptionText((int)auOption);
                            keyInfo.Values.Add($"AUOptions = {auOption} ({AutoUpdateOption})");
                        }

                        NoAutoUpdate = Convert.ToBoolean(key.GetValue("NoAutoUpdate", 0));
                        ScheduledInstallDay = Convert.ToInt32(key.GetValue("ScheduledInstallDay", 0));
                        ScheduledInstallTime = Convert.ToInt32(key.GetValue("ScheduledInstallTime", 0));
                        UseWUServer = Convert.ToBoolean(key.GetValue("UseWUServer", 0));

                        keyInfo.Values.Add($"NoAutoUpdate = {NoAutoUpdate}");
                        keyInfo.Values.Add($"ScheduledInstallDay = {ScheduledInstallDay}");
                        keyInfo.Values.Add($"ScheduledInstallTime = {ScheduledInstallTime}");
                        keyInfo.Values.Add($"UseWUServer = {UseWUServer}");
                    }

                    CheckedRegistryKeys.Add(keyInfo);
                }

                // Check Windows Update Auto Update settings
                keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update";
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(keyPath))
                {
                    var keyInfo = new RegistryKeyInfo
                    {
                        Path = $@"HKLM\{keyPath}",
                        Exists = key != null
                    };

                    if (key != null)
                    {
                        var enabled = key.GetValue("EnableFeaturedSoftware");
                        AutoUpdateEnabled = enabled != null && Convert.ToBoolean(enabled);
                        keyInfo.Values.Add($"EnableFeaturedSoftware = {enabled ?? "(not set)"}");
                    }

                    CheckedRegistryKeys.Add(keyInfo);
                }

                // Check last download success
                keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Download";
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(keyPath))
                {
                    var keyInfo = new RegistryKeyInfo
                    {
                        Path = $@"HKLM\{keyPath}",
                        Exists = key != null
                    };

                    if (key != null)
                    {
                        LastSuccessTime = key.GetValue("LastSuccessTime") as string;
                        keyInfo.Values.Add($"LastSuccessTime = {LastSuccessTime ?? "(not set)"}");
                    }

                    CheckedRegistryKeys.Add(keyInfo);
                }

                // Check last search success
                keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Search";
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(keyPath))
                {
                    var keyInfo = new RegistryKeyInfo
                    {
                        Path = $@"HKLM\{keyPath}",
                        Exists = key != null
                    };

                    if (key != null)
                    {
                        LastSearchSuccessTime = key.GetValue("LastSuccessTime") as string;
                        keyInfo.Values.Add($"LastSuccessTime = {LastSearchSuccessTime ?? "(not set)"}");
                    }

                    CheckedRegistryKeys.Add(keyInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading registry: {ex.Message}");
            }
        }

        private void GetServiceStatus()
        {
            try
            {
                string[] services = { "wuauserv", "BITS", "cryptsvc", "msiserver" };
                
                foreach (string serviceName in services)
                {
                    using (ManagementObject service = new ManagementObject($"Win32_Service.Name='{serviceName}'"))
                    {
                        service.Get();
                        string state = service["State"]?.ToString() ?? "Unknown";
                        string startMode = service["StartMode"]?.ToString() ?? "Unknown";
                        ServiceStatus.Add($"{serviceName}: {state} (StartMode: {startMode})");
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceStatus.Add($"Error getting service status: {ex.Message}");
            }
        }

        private void GetWUASettings()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_Service WHERE Name = 'wuauserv'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string state = obj["State"]?.ToString();
                        AutoUpdateEnabled = state == "Running";
                    }
                }

                var automaticUpdates = new WUApiLib.AutomaticUpdates();
                var results = automaticUpdates.Results;

                // Newer Windows versions no longer populate the legacy result registry keys.
                string installationTime = FormatWuaTime(results.LastInstallationSuccessDate);
                string searchTime = FormatWuaTime(results.LastSearchSuccessDate);

                if (!string.IsNullOrEmpty(installationTime))
                    LastSuccessTime = installationTime;

                if (!string.IsNullOrEmpty(searchTime))
                    LastSearchSuccessTime = searchTime;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting WUA settings: {ex.Message}");
            }
        }

        private static string FormatWuaTime(DateTime value)
        {
            // COM uses 1601-01-01 when the result has never been recorded.
            if (value.Year <= 1601)
                return null;

            // The WUA COM API returns UTC as an Unspecified DateTime.
            DateTime utcTime = value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

            return utcTime.ToLocalTime().ToString("g");
        }

        private string GetAutoUpdateOptionText(int option)
        {
            switch (option)
            {
                case 1: return "Disabled";
                case 2: return "Notify before download";
                case 3: return "Download but notify before install";
                case 4: return "Automatic download and install";
                case 5: return "Allow local admin to choose setting";
                default: return $"Unknown ({option})";
            }
        }

        public void Display()
        {
            Console.WriteLine("\n=== Windows Update Configuration ===");
            Console.WriteLine($"Auto Update Enabled: {AutoUpdateEnabled}");
            Console.WriteLine($"Auto Update Option: {AutoUpdateOption ?? "Not configured"}");
            Console.WriteLine($"No Auto Update: {NoAutoUpdate}");
            Console.WriteLine($"Use WSUS Server: {UseWUServer}");

            if (!string.IsNullOrEmpty(WUServer))
                Console.WriteLine($"WSUS Server: {WUServer}");

            if (!string.IsNullOrEmpty(WUStatusServer))
                Console.WriteLine($"WSUS Status Server: {WUStatusServer}");

            if (!string.IsNullOrEmpty(TargetGroup))
                Console.WriteLine($"Target Group: {TargetGroup}");

            if (ScheduledInstallDay > 0)
                Console.WriteLine($"Scheduled Install Day: {GetDayOfWeek(ScheduledInstallDay)}");

            if (ScheduledInstallTime > 0)
                Console.WriteLine($"Scheduled Install Time: {ScheduledInstallTime:D2}:00");

            Console.WriteLine($"Elevate Non-Admins: {ElevateNonAdmins}");

            if (!string.IsNullOrEmpty(LastSuccessTime))
                Console.WriteLine($"Last Download Success: {LastSuccessTime}");

            if (!string.IsNullOrEmpty(LastSearchSuccessTime))
                Console.WriteLine($"Last Search Success: {LastSearchSuccessTime}");

            Console.WriteLine("\n=== Service Status ===");
            foreach (var status in ServiceStatus)
            {
                Console.WriteLine(status);
            }

            Console.WriteLine("\n=== Registry Keys Checked ===");
            foreach (var regKey in CheckedRegistryKeys)
            {
                if (regKey.Exists)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[✓] {regKey.Path}");
                    Console.ResetColor();

                    if (regKey.Values.Count > 0)
                    {
                        foreach (var value in regKey.Values)
                        {
                            Console.WriteLine($"    {value}");
                        }
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[!] {regKey.Path} (not found)");
                    Console.ResetColor();
                }
            }

            // Display MDM Policies
            if (HasMDMPolicies && MDMPolicies.Count > 0)
            {
                Console.WriteLine("\n=== MDM/Intune Policies ===");
                foreach (var policy in MDMPolicies)
                {
                    Console.WriteLine(policy);
                }
            }
            else
            {
                Console.WriteLine("\n=== MDM/Intune Policies ===");
                Console.WriteLine("No MDM/Intune policies detected.");
                Console.WriteLine("Device appears to be unmanaged or using local Group Policy only.");
            }
        }

        private string GetDayOfWeek(int day)
        {
            switch (day)
            {
                case 0: return "Every day";
                case 1: return "Sunday";
                case 2: return "Monday";
                case 3: return "Tuesday";
                case 4: return "Wednesday";
                case 5: return "Thursday";
                case 6: return "Friday";
                case 7: return "Saturday";
                default: return $"Unknown ({day})";
            }
        }

        private void GetMDMPolicies()
        {
            try
            {
                // Check MDM provider information - validate it's a real active enrollment
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Enrollments"))
                {
                    if (key != null)
                    {
                        foreach (string subKeyName in key.GetSubKeyNames())
                        {
                            using (var enrollmentKey = key.OpenSubKey(subKeyName))
                            {
                                if (enrollmentKey != null)
                                {
                                    var enrollmentState = enrollmentKey.GetValue("EnrollmentState");

                                    // Check for active enrollment (state = 1) AND validate it's real
                                    if (enrollmentState != null && Convert.ToInt32(enrollmentState) == 1)
                                    {
                                        var providerName = enrollmentKey.GetValue("ProviderName");
                                        var providerID = enrollmentKey.GetValue("ProviderID");
                                        var discoveryServiceFullUrl = enrollmentKey.GetValue("DiscoveryServiceFullURL");
                                        var upn = enrollmentKey.GetValue("UPN");

                                        // Windows also creates enrollment records for local and platform CSP authorities.
                                        // Require Intune-specific provider or discovery information before reporting MDM.
                                        bool isIntuneEnrollment = IsIntuneEnrollmentProvider(providerName, providerID, discoveryServiceFullUrl);

                                        if (isIntuneEnrollment)
                                        {
                                            if (!MDMPolicies.Any(p => p == "=== MDM Enrollment ==="))
                                            {
                                                MDMPolicies.Add("=== MDM Enrollment ===");
                                            }

                                            if (providerName != null)
                                            {
                                                MDMPolicies.Add($"Provider: {providerName}");
                                            }
                                            else if (discoveryServiceFullUrl != null)
                                            {
                                                MDMPolicies.Add($"Discovery URL: {discoveryServiceFullUrl}");
                                            }
                                            else if (providerID != null)
                                            {
                                                MDMPolicies.Add($"Provider ID: {providerID}");
                                            }

                                            MDMPolicies.Add($"Enrollment State: Active");

                                            if (upn != null)
                                            {
                                                MDMPolicies.Add($"User: {upn}");
                                            }

                                            HasMDMPolicies = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Check MDM Update policies using 64-bit registry view
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    try
                    {
                        using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\PolicyManager\current\device\Update", writable: false))
                        {
                            if (key != null)
                            {
                                string[] valueNames = key.GetValueNames();

                                // Filter to get only actual policy settings (not metadata)
                                var policyValues = valueNames.Where(v => 
                                    !string.IsNullOrWhiteSpace(v) && 
                                    !v.EndsWith("_ProviderSet", StringComparison.OrdinalIgnoreCase) &&
                                    !v.EndsWith("_WinningProvider", StringComparison.OrdinalIgnoreCase) &&
                                    !v.EndsWith("_LastWrite", StringComparison.OrdinalIgnoreCase) &&
                                    !v.Equals("knobs_Dirty", StringComparison.OrdinalIgnoreCase)).ToArray();

                                if (policyValues.Length > 0)
                                {
                                    MDMPolicies.Add("\n=== MDM Update Policies ===");
                                    MDMPolicies.Add($"Found {policyValues.Length} active policy setting(s) from PolicyManager");

                                    // Group policies by category for better readability
                                    var deferrals = new List<string>();
                                    var deadlines = new List<string>();
                                    var enrollment = new List<string>();
                                    var other = new List<string>();

                                    foreach (string valueName in policyValues)
                                    {
                                        var value = key.GetValue(valueName);
                                        if (value != null)
                                        {
                                            string formattedValue = value.ToString();
                                            string policyLine = $"{valueName} = {formattedValue}";

                                            // Categorize for better organization
                                            if (valueName.Contains("Defer") || valueName.Contains("Pause"))
                                            {
                                                deferrals.Add(policyLine);
                                            }
                                            else if (valueName.Contains("Deadline") || valueName.Contains("Grace"))
                                            {
                                                deadlines.Add(policyLine);
                                            }
                                            else if (valueName.Contains("Enrolled"))
                                            {
                                                enrollment.Add(policyLine);
                                            }
                                            else
                                            {
                                                other.Add(policyLine);
                                            }
                                        }
                                    }

                                    // Display in organized sections
                                    if (deferrals.Count > 0)
                                    {
                                        MDMPolicies.Add("\nDeferral & Pause Settings:");
                                        MDMPolicies.AddRange(deferrals.Select(p => $"  {p}"));
                                    }
                                    if (deadlines.Count > 0)
                                    {
                                        MDMPolicies.Add("\nDeadline Settings:");
                                        MDMPolicies.AddRange(deadlines.Select(p => $"  {p}"));
                                    }
                                    if (enrollment.Count > 0)
                                    {
                                        MDMPolicies.Add("\nEnrollment Status:");
                                        MDMPolicies.AddRange(enrollment.Select(p => $"  {p}"));
                                    }
                                    if (other.Count > 0)
                                    {
                                        MDMPolicies.Add("\nOther Settings:");
                                        MDMPolicies.AddRange(other.Select(p => $"  {p}"));
                                    }

                                    HasMDMPolicies = true;
                                }
                            }
                        }
                    }
                    catch (System.Security.SecurityException)
                    {
                        MDMPolicies.Add("\n! Access denied to PolicyManager registry key");
                        MDMPolicies.Add("  Run with administrator privileges to view MDM policies");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        MDMPolicies.Add("\n! Unauthorized access to PolicyManager registry key");
                        MDMPolicies.Add("  Run with administrator privileges to view MDM policies");
                    }
                }

                // Check Windows Update for Business settings and paused updates.
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings"))
                {
                    if (key != null)
                    {
                        var deferFeature = key.GetValue("DeferFeatureUpdatesPeriodInDays");
                        var deferQuality = key.GetValue("DeferQualityUpdatesPeriodInDays");
                        var pauseAllStart = key.GetValue("PauseUpdatesStartTime");
                        var pauseAllEnd = key.GetValue("PauseUpdatesExpiryTime");
                        var pauseFeatureStart = key.GetValue("PauseFeatureUpdatesStartTime");
                        var pauseFeatureEnd = key.GetValue("PauseFeatureUpdatesEndTime");
                        var pauseQualityStart = key.GetValue("PauseQualityUpdatesStartTime");
                        var pauseQualityEnd = key.GetValue("PauseQualityUpdatesEndTime");

                        bool hasPause = pauseAllStart != null || pauseAllEnd != null ||
                                        pauseFeatureStart != null || pauseFeatureEnd != null ||
                                        pauseQualityStart != null || pauseQualityEnd != null;

                        if (deferFeature != null || deferQuality != null || hasPause)
                        {
                            if (!MDMPolicies.Any(p => p.Contains("Windows Update for Business")))
                            {
                                MDMPolicies.Add("\n=== Windows Update for Business ===");
                            }
                            if (deferFeature != null)
                            {
                                MDMPolicies.Add($"Defer Feature Updates: {deferFeature} days");
                                HasMDMPolicies = true;
                            }
                            if (deferQuality != null)
                            {
                                MDMPolicies.Add($"Defer Quality Updates: {deferQuality} days");
                                HasMDMPolicies = true;
                            }
                            if (pauseAllStart != null && pauseAllEnd != null)
                            {
                                MDMPolicies.Add($"Pause source: Local Settings UI (all updates paused until {pauseAllEnd})");
                                HasMDMPolicies = true;
                            }
                            else
                            {
                                if (pauseFeatureStart != null && pauseFeatureEnd != null)
                                {
                                    MDMPolicies.Add($"Pause source: Local Settings UI (feature updates paused until {pauseFeatureEnd})");
                                    HasMDMPolicies = true;
                                }
                                if (pauseQualityStart != null && pauseQualityEnd != null)
                                {
                                    MDMPolicies.Add($"Pause source: Local Settings UI (quality updates paused until {pauseQualityEnd})");
                                    HasMDMPolicies = true;
                                }
                            }
                        }
                    }
                }

                // Update-ring policies also expose pause state through MDM policy result classes.
                // If an update ring has paused feature or quality updates, report that source instead
                // of labeling the pause as a local Settings UI action.
                try
                {
                    using (var searcher = new ManagementObjectSearcher(
                        @"root\cimv2\mdm\dmmap",
                        "SELECT * FROM MDM_Policy_Result01_Update02"))
                    {
                        bool addedUpdateRingHeader = false;
                        foreach (ManagementObject policy in searcher.Get())
                        {
                            object pauseFeature = policy["PauseFeatureUpdates"];
                            object pauseQuality = policy["PauseQualityUpdates"];

                            if (pauseFeature != null && Convert.ToInt32(pauseFeature) == 1)
                            {
                                if (!addedUpdateRingHeader)
                                {
                                    MDMPolicies.Add("\n=== Update Ring Policy ===");
                                    addedUpdateRingHeader = true;
                                }
                                MDMPolicies.Add("Feature updates are paused by an update ring policy");
                                HasMDMPolicies = true;
                            }

                            if (pauseQuality != null && Convert.ToInt32(pauseQuality) == 1)
                            {
                                if (!addedUpdateRingHeader)
                                {
                                    MDMPolicies.Add("\n=== Update Ring Policy ===");
                                    addedUpdateRingHeader = true;
                                }
                                MDMPolicies.Add("Quality updates are paused by an update ring policy");
                                HasMDMPolicies = true;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Update-ring policy results are unavailable on unmanaged or older systems.
                }

                // The Intune Management Extension registry key and OMADM enrollment records can exist
                // without an active Intune enrollment. Never treat their mere presence as proof of MDM.
            }
            catch (Exception ex)
            {
                MDMPolicies.Add($"Error checking MDM policies: {ex.Message}");
            }
        }

        private static bool IsIntuneEnrollmentProvider(object providerName, object providerId, object discoveryUrl)
        {
            string[] intuneMarkers =
            {
                "intune",
                "microsoft device management",
                "microsoftintune",
                "manage.microsoft.com",
                "dm.microsoft.com"
            };

            object[] values = { providerName, providerId, discoveryUrl };

            foreach (object value in values)
            {
                if (value == null)
                    continue;

                string text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text) &&
                    intuneMarkers.Any(marker => text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Information about a checked registry key
    /// </summary>
    public class RegistryKeyInfo
    {
        public string Path { get; set; }
        public bool Exists { get; set; }
        public List<string> Values { get; set; }

        public RegistryKeyInfo()
        {
            Values = new List<string>();
        }
    }
}
