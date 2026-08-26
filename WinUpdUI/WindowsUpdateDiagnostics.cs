using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using Microsoft.Win32;

namespace WinUpdUI
{
    /// <summary>
    /// Diagnoses common Windows Update issues
    /// </summary>
    public class WindowsUpdateDiagnostics
    {
        private List<DiagnosticResult> _results;

        public WindowsUpdateDiagnostics()
        {
            _results = new List<DiagnosticResult>();
        }

        public void RunDiagnostics()
        {
            Console.WriteLine("\n=== Running Windows Update Diagnostics ===\n");

            CheckWindowsUpdateService();
            CheckBITSService();
            CheckCryptographicService();
            CheckInstallerService();
            CheckDiskSpace();
            CheckPendingReboot();
            CheckUpdateDatabase();
            CheckNetworkConnectivity();
            CheckSystemIntegrity();
            CheckFeatureUpdateBlocks();

            DisplayResults();
        }

        private void CheckWindowsUpdateService()
        {
            CheckService("wuauserv", "Windows Update Service");
        }

        private void CheckBITSService()
        {
            CheckService("BITS", "Background Intelligent Transfer Service");
        }

        private void CheckCryptographicService()
        {
            CheckService("cryptsvc", "Cryptographic Services");
        }

        private void CheckInstallerService()
        {
            CheckService("msiserver", "Windows Installer Service");
        }

        private void CheckService(string serviceName, string displayName)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    sc.Refresh();
                    
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        _results.Add(new DiagnosticResult
                        {
                            CheckName = displayName,
                            Status = DiagnosticStatus.Pass,
                            Message = $"{displayName} is running"
                        });
                    }
                    else
                    {
                        _results.Add(new DiagnosticResult
                        {
                            CheckName = displayName,
                            Status = DiagnosticStatus.Warning,
                            Message = $"{displayName} is not running (Status: {sc.Status})",
                            Recommendation = $"Start the service using: net start {serviceName}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _results.Add(new DiagnosticResult
                {
                    CheckName = displayName,
                    Status = DiagnosticStatus.Error,
                    Message = $"Error checking {displayName}: {ex.Message}"
                });
            }
        }

        private void CheckDiskSpace()
        {
            try
            {
                DriveInfo systemDrive = new DriveInfo(Environment.GetEnvironmentVariable("SystemDrive") ?? "C:");
                long freeSpaceGB = systemDrive.AvailableFreeSpace / (1024 * 1024 * 1024);
                long totalSpaceGB = systemDrive.TotalSize / (1024 * 1024 * 1024);
                double freePercent = (double)systemDrive.AvailableFreeSpace / systemDrive.TotalSize * 100;

                if (freeSpaceGB < 10)
                {
                    _results.Add(new DiagnosticResult
                    {
                        CheckName = "Disk Space",
                        Status = DiagnosticStatus.Error,
                        Message = $"Low disk space: {freeSpaceGB} GB free ({freePercent:F1}%) out of {totalSpaceGB} GB",
                        Recommendation = "Free up at least 10 GB of disk space before installing updates"
                    });
                }
                else if (freeSpaceGB < 20)
                {
                    _results.Add(new DiagnosticResult
                    {
                        CheckName = "Disk Space",
                        Status = DiagnosticStatus.Warning,
                        Message = $"Disk space is getting low: {freeSpaceGB} GB free ({freePercent:F1}%) out of {totalSpaceGB} GB",
                        Recommendation = "Consider freeing up more disk space"
                    });
                }
                else
                {
                    _results.Add(new DiagnosticResult
                    {
                        CheckName = "Disk Space",
                        Status = DiagnosticStatus.Pass,
                        Message = $"Sufficient disk space: {freeSpaceGB} GB free ({freePercent:F1}%) out of {totalSpaceGB} GB"
                    });
                }
            }
            catch (Exception ex)
            {
                _results.Add(new DiagnosticResult
                {
                    CheckName = "Disk Space",
                    Status = DiagnosticStatus.Error,
                    Message = $"Error checking disk space: {ex.Message}"
                });
            }
        }

        private void CheckPendingReboot()
        {
            bool rebootPending = false;
            string reason = "";
            List<string> detectedKeys = new List<string>();

            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                {
                    if (key != null)
                    {
                        rebootPending = true;
                        reason = "Component Based Servicing";
                        detectedKeys.Add(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
                    }
                }

                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                {
                    if (key != null)
                    {
                        rebootPending = true;
                        reason = string.IsNullOrEmpty(reason) ? "Windows Update" : reason + ", Windows Update";
                        detectedKeys.Add(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
                    }
                }

                // Check for pending file rename operations (common indicator of pending reboot)
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
                {
                    if (key != null)
                    {
                        var pendingFileRenameOperations = key.GetValue("PendingFileRenameOperations");
                        if (pendingFileRenameOperations != null)
                        {
                            rebootPending = true;
                            reason = string.IsNullOrEmpty(reason) ? "Pending file operations" : reason + ", Pending file operations";
                            detectedKeys.Add(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager [PendingFileRenameOperations]");
                        }
                    }
                }

                // Check for pending computer rename
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName"))
                {
                    if (key != null)
                    {
                        var computerName = key.GetValue("ComputerName") as string;

                        using (var pendingKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName"))
                        {
                            if (pendingKey != null)
                            {
                                var pendingComputerName = pendingKey.GetValue("ComputerName") as string;

                                if (!string.IsNullOrEmpty(computerName) && 
                                    !string.IsNullOrEmpty(pendingComputerName) && 
                                    !computerName.Equals(pendingComputerName, StringComparison.OrdinalIgnoreCase))
                                {
                                    rebootPending = true;
                                    reason = string.IsNullOrEmpty(reason) ? "Computer rename pending" : reason + ", Computer rename pending";
                                    detectedKeys.Add(@"HKLM\SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName");
                                }
                            }
                        }
                    }
                }

                if (rebootPending)
                {
                    string keyList = "\n  Registry keys detected:\n  - " + string.Join("\n  - ", detectedKeys);
                    _results.Add(new DiagnosticResult
                    {
                        CheckName = "Pending Reboot",
                        Status = DiagnosticStatus.Warning,
                        Message = $"System reboot is pending due to: {reason}{keyList}",
                        Recommendation = "Restart the computer to complete previous updates"
                    });
                }
                else
                {
                    _results.Add(new DiagnosticResult
                    {
                        CheckName = "Pending Reboot",
                        Status = DiagnosticStatus.Pass,
                        Message = "No pending reboot required"
                    });
                }
            }
            catch (Exception ex)
            {
                _results.Add(new DiagnosticResult
                {
                    CheckName = "Pending Reboot",
                    Status = DiagnosticStatus.Error,
                    Message = $"Error checking pending reboot: {ex.Message}"
                });
            }
        }

        private void CheckUpdateDatabase()
        {
            try
            {
                string softwareDistribution = Path.Combine(Environment.GetEnvironmentVariable("SystemRoot"), "SoftwareDistribution");
                string dataStore = Path.Combine(softwareDistribution, "DataStore");

                if (Directory.Exists(dataStore))
                {
                    DirectoryInfo di = new DirectoryInfo(dataStore);
                    long sizeBytes = di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
                    long sizeMB = sizeBytes / (1024 * 1024);

                    if (sizeMB > 1024)
                    {
                        _results.Add(new DiagnosticResult
                        {
                            CheckName = "Update Database",
                            Status = DiagnosticStatus.Warning,
                            Message = $"Update database is large: {sizeMB} MB",
                            Recommendation = "Consider running Windows Update troubleshooter or resetting update components"
                        });
                    }
                    else
                    {
                        _results.Add(new DiagnosticResult
                        {
                            CheckName = "Update Database",
                            Status = DiagnosticStatus.Pass,
                            Message = $"Update database size: {sizeMB} MB"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _results.Add(new DiagnosticResult
                {
                    CheckName = "Update Database",
                    Status = DiagnosticStatus.Warning,
                    Message = $"Could not check update database: {ex.Message}"
                });
            }
        }

        private void CheckNetworkConnectivity()
        {
            try
            {
                // Try HTTP request first (more reliable than ping for Windows Update servers)
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                using (var client = new System.Net.WebClient())
                {
                    client.Headers.Add("User-Agent", "WinUpdateDiag");
                    client.Proxy = System.Net.WebRequest.DefaultWebProxy;
                    client.Proxy.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

                    try
                    {
                        // Try to download a small file from Microsoft's update servers
                        client.DownloadData("http://www.msftconnecttest.com/connecttest.txt");
                        stopwatch.Stop();

                        _results.Add(new DiagnosticResult
                        {
                            CheckName = "Network Connectivity",
                            Status = DiagnosticStatus.Pass,
                            Message = $"Can reach Microsoft servers ({stopwatch.ElapsedMilliseconds}ms)"
                        });
                        return;
                    }
                    catch
                    {
                        // If HTTP fails, try ping to a reliable Microsoft server
                        stopwatch.Restart();
                        using (var ping = new System.Net.NetworkInformation.Ping())
                        {
                            var result = ping.Send("8.8.8.8", 5000); // Google DNS as fallback
                            stopwatch.Stop();

                            if (result.Status == System.Net.NetworkInformation.IPStatus.Success)
                            {
                                _results.Add(new DiagnosticResult
                                {
                                    CheckName = "Network Connectivity",
                                    Status = DiagnosticStatus.Warning,
                                    Message = $"Internet is reachable but cannot connect to Microsoft servers",
                                    Recommendation = "Check firewall settings and proxy configuration for Windows Update"
                                });
                            }
                            else
                            {
                                _results.Add(new DiagnosticResult
                                {
                                    CheckName = "Network Connectivity",
                                    Status = DiagnosticStatus.Error,
                                    Message = $"No network connectivity detected",
                                    Recommendation = "Check network connection and verify internet access"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _results.Add(new DiagnosticResult
                {
                    CheckName = "Network Connectivity",
                    Status = DiagnosticStatus.Warning,
                    Message = $"Network connectivity check failed: {ex.Message}",
                    Recommendation = "Verify internet connection and DNS settings"
                });
            }
        }

        private void CheckSystemIntegrity()
        {
            try
            {
                string systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
                string[] criticalPaths = {
                    Path.Combine(systemRoot, "System32"),
                    Path.Combine(systemRoot, "SoftwareDistribution"),
                    Path.Combine(systemRoot, "System32", "catroot2")
                };

                bool allPathsExist = criticalPaths.All(Directory.Exists);

                if (allPathsExist)
                {
                    _results.Add(new DiagnosticResult
                    {
                        CheckName = "System Integrity",
                        Status = DiagnosticStatus.Pass,
                        Message = "Critical Windows Update paths exist"
                    });
                }
                else
                {
                    _results.Add(new DiagnosticResult
                    {
                        CheckName = "System Integrity",
                        Status = DiagnosticStatus.Error,
                        Message = "One or more critical Windows Update paths are missing",
                        Recommendation = "Run System File Checker (sfc /scannow)"
                    });
                }
            }
            catch (Exception ex)
            {
                _results.Add(new DiagnosticResult
                {
                    CheckName = "System Integrity",
                    Status = DiagnosticStatus.Warning,
                    Message = $"System integrity check failed: {ex.Message}"
                });
            }
        }

        private void CheckFeatureUpdateBlocks()
        {
            try
            {
                var blockingSettings = new List<string>();

                // Check for feature update deferrals
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"))
                {
                    if (key != null)
                    {
                        var deferFeatureUpdates = key.GetValue("DeferFeatureUpdates");
                        if (deferFeatureUpdates != null && Convert.ToInt32(deferFeatureUpdates) == 1)
                        {
                            var deferDays = key.GetValue("DeferFeatureUpdatesPeriodInDays");
                            blockingSettings.Add($"Feature updates deferred by {deferDays ?? "unknown"} days");
                        }

                        var pauseFeatureUpdates = key.GetValue("PauseFeatureUpdates");
                        if (pauseFeatureUpdates != null && Convert.ToInt32(pauseFeatureUpdates) == 1)
                        {
                            blockingSettings.Add("Feature updates are paused");
                        }

                        var targetReleaseVersion = key.GetValue("TargetReleaseVersion");
                        if (targetReleaseVersion != null && Convert.ToInt32(targetReleaseVersion) == 1)
                        {
                            var targetVersion = key.GetValue("TargetReleaseVersionInfo");
                            blockingSettings.Add($"Feature updates limited to version: {targetVersion}");
                        }

                        var productVersion = key.GetValue("ProductVersion");
                        if (productVersion != null)
                        {
                            blockingSettings.Add($"Product version restricted to: {productVersion}");
                        }
                    }
                }

                // Check Windows Update for Business settings
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings"))
                {
                    if (key != null)
                    {
                        var pauseFeatureUpdatesStartTime = key.GetValue("PauseFeatureUpdatesStartTime");
                        var pauseFeatureUpdatesEndTime = key.GetValue("PauseFeatureUpdatesEndTime");

                        if (pauseFeatureUpdatesStartTime != null && pauseFeatureUpdatesEndTime != null)
                        {
                            blockingSettings.Add($"Feature updates paused until: {pauseFeatureUpdatesEndTime}");
                        }

                        var deferFeatureUpdatesPeriodInDays = key.GetValue("DeferFeatureUpdatesPeriodInDays");
                        if (deferFeatureUpdatesPeriodInDays != null)
                        {
                            blockingSettings.Add($"Feature updates deferred: {deferFeatureUpdatesPeriodInDays} days");
                        }
                    }
                }

                // Check for Windows 10/11 upgrade blocks
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"))
                {
                    if (key != null)
                    {
                        var disableOSUpgrade = key.GetValue("DisableOSUpgrade");
                        if (disableOSUpgrade != null && Convert.ToInt32(disableOSUpgrade) == 1)
                        {
                            blockingSettings.Add("OS upgrades are disabled");
                        }
                    }
                }

                if (blockingSettings.Count > 0)
                {
                    _results.Add(new DiagnosticResult
                    {
                        CheckName = "Feature Update Restrictions",
                        Status = DiagnosticStatus.Warning,
                        Message = $"Found {blockingSettings.Count} setting(s) that may block or limit feature updates:\n      " + string.Join("\n      ", blockingSettings),
                        Recommendation = "Review these settings if feature updates are needed"
                    });
                }
                else
                {
                    _results.Add(new DiagnosticResult
                    {
                        CheckName = "Feature Update Restrictions",
                        Status = DiagnosticStatus.Pass,
                        Message = "No feature update restrictions found"
                    });
                }
            }
            catch (Exception ex)
            {
                _results.Add(new DiagnosticResult
                {
                    CheckName = "Feature Update Restrictions",
                    Status = DiagnosticStatus.Warning,
                    Message = $"Could not check feature update restrictions: {ex.Message}"
                });
            }
        }

        public List<DiagnosticResult> GetResults()
        {
            return _results;
        }

        private void DisplayResults()
        {
            Console.WriteLine("\n=== Diagnostic Results ===\n");

            int pass = _results.Count(r => r.Status == DiagnosticStatus.Pass);
            int warning = _results.Count(r => r.Status == DiagnosticStatus.Warning);
            int error = _results.Count(r => r.Status == DiagnosticStatus.Error);

            foreach (var result in _results)
            {
                string statusSymbol = result.Status == DiagnosticStatus.Pass ? "[✓]" :
                                     result.Status == DiagnosticStatus.Warning ? "[!]" : "[✗]";
                
                ConsoleColor color = result.Status == DiagnosticStatus.Pass ? ConsoleColor.Green :
                                   result.Status == DiagnosticStatus.Warning ? ConsoleColor.Yellow : ConsoleColor.Red;

                Console.ForegroundColor = color;
                Console.Write(statusSymbol);
                Console.ResetColor();
                Console.WriteLine($" {result.CheckName}: {result.Message}");

                if (!string.IsNullOrEmpty(result.Recommendation))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"    → {result.Recommendation}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine($"\nSummary: {pass} passed, {warning} warnings, {error} errors");
        }
    }

    public enum DiagnosticStatus
    {
        Pass,
        Warning,
        Error
    }

    public class DiagnosticResult
    {
        public string CheckName { get; set; }
        public DiagnosticStatus Status { get; set; }
        public string Message { get; set; }
        public string Recommendation { get; set; }
    }
}
