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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting WUA settings: {ex.Message}");
            }
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

                                        // Only consider it a real enrollment if we have provider info
                                        bool isRealEnrollment = providerName != null || 
                                                               providerID != null || 
                                                               discoveryServiceFullUrl != null;

                                        if (isRealEnrollment)
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

                // Check Windows Update for Business settings
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings"))
                {
                    if (key != null)
                    {
                        var deferFeature = key.GetValue("DeferFeatureUpdatesPeriodInDays");
                        var deferQuality = key.GetValue("DeferQualityUpdatesPeriodInDays");
                        var pauseStart = key.GetValue("PauseFeatureUpdatesStartTime");

                        if (deferFeature != null || deferQuality != null || pauseStart != null)
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
                        }
                    }
                }

                // Check Intune Management Extension presence
                var intuneKeys = new[]
                {
                    @"SOFTWARE\Microsoft\IntuneManagementExtension\Win32Apps",
                    @"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts"
                };

                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    foreach (var keyPath in intuneKeys)
                    {
                        using (var key = baseKey.OpenSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                if (!MDMPolicies.Any(p => p.Contains("Intune Management")))
                                {
                                    MDMPolicies.Add("\n=== Intune Management ===");
                                    MDMPolicies.Add($"Intune Management Extension detected");
                                    HasMDMPolicies = true;
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MDMPolicies.Add($"Error checking MDM policies: {ex.Message}");
            }
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
