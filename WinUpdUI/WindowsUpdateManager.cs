using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WUApiLib;

namespace WinUpdUI
{
    /// <summary>
    /// Manages Windows Update operations including searching, configuration, and diagnostics
    /// </summary>
    public class WindowsUpdateManager
    {
        private readonly UpdateSession _updateSession;
        private readonly IUpdateSearcher _updateSearcher;

        public WindowsUpdateManager()
        {
            _updateSession = new UpdateSession();
            _updateSearcher = _updateSession.CreateUpdateSearcher();
        }

        /// <summary>
        /// Gets available updates from Windows Update
        /// </summary>
        public List<UpdateInfo> GetAvailableUpdates(bool includeOptional = false)
        {
            var updates = new List<UpdateInfo>();
            try
            {
                Console.WriteLine("Searching for updates...");

                // Check if drivers are excluded by MDM policy
                bool driversExcluded = CheckIfDriversExcludedByMDM();
                if (driversExcluded)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠ Note: Driver updates are excluded by MDM policy (ExcludeWUDriversInQualityUpdate)");
                    Console.WriteLine("  Drivers will not appear in search results. Use --drivers to see blocked drivers.");
                    Console.ResetColor();
                }

                // Simplified search criteria - more compatible across systems
                string searchCriteria = includeOptional 
                    ? "IsInstalled=0" 
                    : "IsInstalled=0 and Type='Software'";

                ISearchResult searchResult = _updateSearcher.Search(searchCriteria);

                Console.WriteLine($"Found {searchResult.Updates.Count} update(s)");

                foreach (IUpdate update in searchResult.Updates)
                {
                    updates.Add(new UpdateInfo
                    {
                        Title = update.Title,
                        Description = update.Description,
                        IsDownloaded = update.IsDownloaded,
                        IsMandatory = update.IsMandatory,
                        KBArticleIDs = GetKBArticles(update.KBArticleIDs),
                        MaxDownloadSize = update.MaxDownloadSize,
                        MinDownloadSize = update.MinDownloadSize,
                        RebootRequired = update.InstallationBehavior?.RebootBehavior != WUApiLib.InstallationRebootBehavior.irbNeverReboots,
                        SeverityLevel = update.MsrcSeverity,
                        UpdateID = update.Identity.UpdateID,
                        SupportUrl = update.SupportUrl,
                        Categories = GetCategories(update.Categories)
                    });
                }
            }
            catch (COMException ex)
            {
                HandleSearchError(ex, "available updates");
            }

            return updates;
        }

        /// <summary>
        /// Gets pending updates that are downloaded but not installed
        /// </summary>
        public List<UpdateInfo> GetPendingUpdates()
        {
            var updates = new List<UpdateInfo>();
            try
            {
                Console.WriteLine("Searching for pending updates...");

                // Try simple search first
                ISearchResult searchResult;
                try
                {
                    searchResult = _updateSearcher.Search("IsInstalled=0 and IsPresent=1");
                }
                catch (COMException)
                {
                    // Fallback to simpler criteria if IsPresent causes issues
                    searchResult = _updateSearcher.Search("IsInstalled=0");
                }

                int pendingCount = 0;
                foreach (IUpdate update in searchResult.Updates)
                {
                    // Filter for downloaded updates
                    if (update.IsDownloaded)
                    {
                        pendingCount++;
                        updates.Add(new UpdateInfo
                        {
                            Title = update.Title,
                            Description = update.Description,
                            IsDownloaded = update.IsDownloaded,
                            IsMandatory = update.IsMandatory,
                            KBArticleIDs = GetKBArticles(update.KBArticleIDs),
                            MaxDownloadSize = update.MaxDownloadSize,
                            RebootRequired = update.InstallationBehavior?.RebootBehavior != WUApiLib.InstallationRebootBehavior.irbNeverReboots,
                            UpdateID = update.Identity.UpdateID
                        });
                    }
                }

                Console.WriteLine($"Found {pendingCount} pending update(s)");
            }
            catch (COMException ex)
            {
                HandleSearchError(ex, "pending updates");
            }

            return updates;
        }

        /// <summary>
        /// Gets applicable updates that are not yet installed (includes both downloaded and not downloaded)
        /// </summary>
        public List<UpdateInfo> GetApplicableUpdates(bool includeOptional = false)
        {
            var updates = new List<UpdateInfo>();
            try
            {
                Console.WriteLine("Searching for applicable updates...");

                // Search for all non-installed updates
                string searchCriteria = includeOptional 
                    ? "IsInstalled=0" 
                    : "IsInstalled=0 and Type='Software'";

                ISearchResult searchResult = _updateSearcher.Search(searchCriteria);

                Console.WriteLine($"Found {searchResult.Updates.Count} applicable update(s)");

                foreach (IUpdate update in searchResult.Updates)
                {
                    updates.Add(new UpdateInfo
                    {
                        Title = update.Title,
                        Description = update.Description,
                        IsDownloaded = update.IsDownloaded,
                        IsMandatory = update.IsMandatory,
                        KBArticleIDs = GetKBArticles(update.KBArticleIDs),
                        MaxDownloadSize = update.MaxDownloadSize,
                        MinDownloadSize = update.MinDownloadSize,
                        RebootRequired = update.InstallationBehavior?.RebootBehavior != WUApiLib.InstallationRebootBehavior.irbNeverReboots,
                        SeverityLevel = update.MsrcSeverity,
                        UpdateID = update.Identity.UpdateID,
                        SupportUrl = update.SupportUrl,
                        Categories = GetCategories(update.Categories)
                    });
                }
            }
            catch (COMException ex)
            {
                HandleSearchError(ex, "applicable updates");
            }

            return updates;
        }

        /// <summary>
        /// Gets installed updates history
        /// </summary>
        public List<UpdateHistoryInfo> GetUpdateHistory(int count = 20)
        {
            var history = new List<UpdateHistoryInfo>();
            try
            {
                Console.WriteLine($"Retrieving update history (last {count} entries)...");
                
                IUpdateSearcher searcher = _updateSession.CreateUpdateSearcher();
                int totalHistory = searcher.GetTotalHistoryCount();
                int actualCount = Math.Min(count, totalHistory);

                IUpdateHistoryEntryCollection historyCollection = searcher.QueryHistory(0, actualCount);

                foreach (IUpdateHistoryEntry entry in historyCollection)
                {
                    history.Add(new UpdateHistoryInfo
                    {
                        Title = entry.Title,
                        Date = entry.Date,
                        Operation = GetOperationText(entry.Operation),
                        ResultCode = GetResultText(entry.ResultCode),
                        Description = entry.Description,
                        UpdateID = entry.UpdateIdentity.UpdateID
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving update history: {ex.Message}");
            }

            return history;
        }

        /// <summary>
        /// Gets installed Defender/antivirus definition updates history
        /// </summary>
        public List<UpdateHistoryInfo> GetDefenderUpdateHistory(int count = 20)
        {
            var history = new List<UpdateHistoryInfo>();
            try
            {
                Console.WriteLine($"Retrieving Defender update history (last {count} entries)...");

                IUpdateSearcher searcher = _updateSession.CreateUpdateSearcher();
                int totalHistory = searcher.GetTotalHistoryCount();

                // We may need to look through more entries to find enough Defender updates
                int actualCount = Math.Min(count * 10, totalHistory); // Search through more entries
                IUpdateHistoryEntryCollection historyCollection = searcher.QueryHistory(0, actualCount);

                foreach (IUpdateHistoryEntry entry in historyCollection)
                {
                    // Check if this is a Defender/Definition update
                    string title = entry.Title ?? "";
                    if (IsDefenderUpdate(title))
                    {
                        history.Add(new UpdateHistoryInfo
                        {
                            Title = entry.Title,
                            Date = entry.Date,
                            Operation = GetOperationText(entry.Operation),
                            ResultCode = GetResultText(entry.ResultCode),
                            Description = entry.Description,
                            UpdateID = entry.UpdateIdentity.UpdateID,
                            IsDefenderUpdate = true
                        });

                        // Stop when we have enough Defender updates
                        if (history.Count >= count)
                            break;
                    }
                }

                Console.WriteLine($"Found {history.Count} Defender update entries");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving Defender update history: {ex.Message}");
            }

            return history;
        }

        /// <summary>
        /// Gets non-Defender update history (excludes definition updates)
        /// </summary>
        public List<UpdateHistoryInfo> GetNonDefenderUpdateHistory(int count = 20)
        {
            var history = new List<UpdateHistoryInfo>();
            try
            {
                Console.WriteLine($"Retrieving update history (excluding Defender, last {count} entries)...");

                IUpdateSearcher searcher = _updateSession.CreateUpdateSearcher();
                int totalHistory = searcher.GetTotalHistoryCount();

                // Search through more entries to find enough non-Defender updates
                // Use a larger multiplier since Defender updates are frequent
                int actualCount = Math.Min(count * 20, totalHistory);
                IUpdateHistoryEntryCollection historyCollection = searcher.QueryHistory(0, actualCount);

                foreach (IUpdateHistoryEntry entry in historyCollection)
                {
                    string title = entry.Title ?? "";

                    // Only include non-Defender updates
                    if (!IsDefenderUpdate(title))
                    {
                        history.Add(new UpdateHistoryInfo
                        {
                            Title = entry.Title,
                            Date = entry.Date,
                            Operation = GetOperationText(entry.Operation),
                            ResultCode = GetResultText(entry.ResultCode),
                            Description = entry.Description,
                            UpdateID = entry.UpdateIdentity.UpdateID,
                            IsDefenderUpdate = false
                        });

                        if (history.Count >= count)
                            break;
                    }
                }

                Console.WriteLine($"Found {history.Count} non-Defender update entries");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving update history: {ex.Message}");
            }

            return history;
        }

        /// <summary>
        /// Determines if an update is a Defender/antivirus definition update
        /// </summary>
        private bool IsDefenderUpdate(string title)
        {
            if (string.IsNullOrEmpty(title))
                return false;

            string lowerTitle = title.ToLower();

            // Check for common Defender/definition update patterns
            return lowerTitle.Contains("definition update") ||
                   lowerTitle.Contains("windows defender") ||
                   lowerTitle.Contains("microsoft defender") ||
                   lowerTitle.Contains("antivirus") ||
                   lowerTitle.Contains("anti-virus") ||
                   lowerTitle.Contains("security intelligence") ||
                   lowerTitle.Contains("virus and spyware definitions") ||
                   lowerTitle.Contains("windows malicious software removal");
        }

        private string GetOperationText(WUApiLib.tagUpdateOperation operation)
        {
            switch (operation)
            {
                case WUApiLib.tagUpdateOperation.uoInstallation: return "Installation";
                case WUApiLib.tagUpdateOperation.uoUninstallation: return "Uninstallation";
                default: return operation.ToString();
            }
        }

        private string GetResultText(WUApiLib.OperationResultCode resultCode)
        {
            switch (resultCode)
            {
                case WUApiLib.OperationResultCode.orcNotStarted: return "Not Started";
                case WUApiLib.OperationResultCode.orcInProgress: return "In Progress";
                case WUApiLib.OperationResultCode.orcSucceeded: return "Succeeded";
                case WUApiLib.OperationResultCode.orcSucceededWithErrors: return "Succeeded with Errors";
                case WUApiLib.OperationResultCode.orcFailed: return "Failed";
                case WUApiLib.OperationResultCode.orcAborted: return "Aborted";
                default: return resultCode.ToString();
            }
        }

        private List<string> GetKBArticles(IStringCollection kbCollection)
        {
            var articles = new List<string>();
            if (kbCollection != null)
            {
                foreach (string kb in kbCollection)
                {
                    articles.Add(kb);
                }
            }
            return articles;
        }

        private List<string> GetCategories(ICategoryCollection categories)
        {
            var categoryList = new List<string>();
            if (categories != null)
            {
                foreach (ICategory category in categories)
                {
                    categoryList.Add(category.Name);
                }
            }
            return categoryList;
        }

        private void HandleSearchError(COMException ex, string operationType)
        {
            const int WU_E_INVALID_CRITERIA = unchecked((int)0x80240032);
            const int WU_E_PT_INVALID_URL = unchecked((int)0x80240002);
            const int WU_E_NO_SERVICE = unchecked((int)0x80240437);

            Console.ForegroundColor = ConsoleColor.Red;

            if (ex.ErrorCode == WU_E_INVALID_CRITERIA)
            {
                Console.WriteLine($"Error searching for {operationType}: Invalid search criteria (0x80240032)");
                Console.WriteLine("This can occur when:");
                Console.WriteLine("  - Windows Update service is not properly initialized");
                Console.WriteLine("  - Windows Update database is corrupted");
                Console.WriteLine("  - System requires a restart");
                Console.WriteLine("\nTry running: WinUdateDiag --diagnose");
            }
            else if (ex.ErrorCode == WU_E_PT_INVALID_URL)
            {
                Console.WriteLine($"Error searching for {operationType}: Invalid update server URL (0x80240002)");
                Console.WriteLine("Check your WSUS/Windows Update configuration.");
            }
            else if (ex.ErrorCode == WU_E_NO_SERVICE)
            {
                Console.WriteLine($"Error searching for {operationType}: Windows Update service is not available (0x80240437)");
                Console.WriteLine("Ensure the Windows Update service is running.");
            }
            else
            {
                Console.WriteLine($"Error searching for {operationType}: {ex.Message} (0x{ex.ErrorCode:X})");
            }

            Console.ResetColor();
        }

        /// <summary>
        /// Checks if drivers are excluded by MDM policy
        /// </summary>
        private bool CheckIfDriversExcludedByMDM()
        {
            try
            {
                // Check PolicyManager for ExcludeWUDriversInQualityUpdate
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\PolicyManager\current\device\Update"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("ExcludeWUDriversInQualityUpdate");
                        if (value != null && value.ToString() == "1")
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // If we can't read the registry, assume drivers aren't excluded
            }

            return false;
        }

        /// <summary>
        /// Gets driver updates that are blocked by MDM policy
        /// This attempts to search for drivers even when ExcludeWUDriversInQualityUpdate is enabled
        /// </summary>
        public List<UpdateInfo> GetBlockedDrivers()
        {
            var updates = new List<UpdateInfo>();

            // First check if drivers are actually excluded
            if (!CheckIfDriversExcludedByMDM())
            {
                Console.WriteLine("Driver updates are not excluded by MDM policy.");
                return updates;
            }

            try
            {
                Console.WriteLine("Searching for driver updates that are blocked by MDM policy...");
                Console.WriteLine("(Attempting to enumerate drivers despite ExcludeWUDriversInQualityUpdate policy)");

                // Search specifically for driver updates
                // Type='Driver' will attempt to find driver updates
                string searchCriteria = "IsInstalled=0 and Type='Driver'";

                ISearchResult searchResult = _updateSearcher.Search(searchCriteria);

                Console.WriteLine($"Found {searchResult.Updates.Count} driver update(s) blocked by policy");

                foreach (IUpdate update in searchResult.Updates)
                {
                    updates.Add(new UpdateInfo
                    {
                        Title = update.Title,
                        Description = update.Description,
                        IsDownloaded = update.IsDownloaded,
                        IsMandatory = update.IsMandatory,
                        KBArticleIDs = GetKBArticles(update.KBArticleIDs),
                        MaxDownloadSize = update.MaxDownloadSize,
                        MinDownloadSize = update.MinDownloadSize,
                        RebootRequired = update.InstallationBehavior?.RebootBehavior != WUApiLib.InstallationRebootBehavior.irbNeverReboots,
                        SeverityLevel = update.MsrcSeverity,
                        UpdateID = update.Identity.UpdateID,
                        SupportUrl = update.SupportUrl,
                        Categories = GetCategories(update.Categories)
                    });
                }
            }
            catch (COMException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ Error searching for driver updates:");
                Console.WriteLine($"  Error Code: 0x{ex.ErrorCode:X8}");

                // Note: Type='Driver' search may not work if the policy is enforced at API level
                if ((uint)ex.ErrorCode == 0x80240032 || (uint)ex.ErrorCode == 0x80240002)
                {
                    Console.WriteLine("\n  The MDM policy appears to be enforced at the Windows Update API level,");
                    Console.WriteLine("  preventing driver enumeration even with direct Type='Driver' queries.");
                    Console.WriteLine("  This is expected behavior when ExcludeWUDriversInQualityUpdate is enabled.");
                    Console.WriteLine("\n  To see blocked drivers, you would need to:");
                    Console.WriteLine("  1. Temporarily disable the MDM policy");
                    Console.WriteLine("  2. Contact your IT administrator");
                    Console.WriteLine("  3. Check Device Manager for devices with available driver updates");
                }
                else
                {
                    Console.WriteLine($"  {ex.Message}");
                }
                Console.ResetColor();
            }

            return updates;
        }
    }

    public class UpdateInfo
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsDownloaded { get; set; }
        public bool IsMandatory { get; set; }
        public List<string> KBArticleIDs { get; set; }
        public decimal MaxDownloadSize { get; set; }
        public decimal MinDownloadSize { get; set; }
        public bool RebootRequired { get; set; }
        public string SeverityLevel { get; set; }
        public string UpdateID { get; set; }
        public string SupportUrl { get; set; }
        public List<string> Categories { get; set; }

        public override string ToString()
        {
            string kb = KBArticleIDs != null && KBArticleIDs.Count > 0 
                ? $"KB{string.Join(", KB", KBArticleIDs)}" 
                : "N/A";
            
            double sizeMB = (double)MaxDownloadSize / (1024 * 1024);
            string size = sizeMB > 0 ? $"{sizeMB:F2} MB" : "N/A";
            
            return $"  Title: {Title}\n" +
                   $"  KB: {kb}\n" +
                   $"  Downloaded: {IsDownloaded}\n" +
                   $"  Mandatory: {IsMandatory}\n" +
                   $"  Size: {size}\n" +
                   $"  Reboot Required: {RebootRequired}\n" +
                   $"  Severity: {SeverityLevel ?? "N/A"}\n" +
                   $"  Categories: {(Categories != null && Categories.Count > 0 ? string.Join(", ", Categories) : "N/A")}";
        }
    }

    public class UpdateHistoryInfo
    {
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Operation { get; set; }
        public string ResultCode { get; set; }
        public string Description { get; set; }
        public string UpdateID { get; set; }
        public bool IsDefenderUpdate { get; set; }

        public override string ToString()
        {
            return $"  [{Date:yyyy-MM-dd HH:mm:ss}] {Operation} - {ResultCode}\n" +
                   $"  {Title}";
        }
    }
}
