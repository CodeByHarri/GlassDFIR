using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GlassDFIR.Models;

namespace GlassDFIR.Services
{
    public class EvidenceCollectionService : IEvidenceCollectionService
    {
        private readonly IVssService _vssService;

        public EvidenceCollectionService(IVssService vssService)
        {
            _vssService = vssService;
        }

        public IEnumerable<UserProfile> GetAvailableProfiles()
        {
            var profiles = new List<UserProfile>();
            try
            {
                string usersPath = @"C:\Users";
                if (Directory.Exists(usersPath))
                {
                    foreach (var userDir in Directory.GetDirectories(usersPath))
                    {
                        string userName = Path.GetFileName(userDir);
                        if (userName.Equals("Public", StringComparison.OrdinalIgnoreCase)) continue;
                        if (userName.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue;
                        if (userName.Equals("Default User", StringComparison.OrdinalIgnoreCase)) continue;
                        if (userName.Equals("All Users", StringComparison.OrdinalIgnoreCase)) continue;

                        profiles.Add(new UserProfile { Name = userName, Path = userDir, IsSelected = true });
                    }
                }
            }
            catch { }
            return profiles;
        }

        public IEnumerable<CollectionArtifact> GetAvailableArtifacts(IEnumerable<UserProfile>? selectedProfiles = null)
        {
            var artifacts = new List<CollectionArtifact>();
            var profiles = selectedProfiles ?? GetAvailableProfiles();

            // 1. NTFS Metadata (Root)
            string[] ntfsFiles = { "$MFT", "$LogFile", "$Boot", "$SDS", "$Extend\\$UsnJrnl:$J" };
            foreach (var f in ntfsFiles)
            {
                artifacts.Add(new CollectionArtifact
                {
                    Name = f.Replace("$", ""),
                    Description = $"NTFS Metadata: {f}",
                    SourcePath = $"C:\\{f}",
                    RequiresVss = true,
                    ToolRecommendation = "MFTECmd",
                    Category = "NTFS Metadata",
                    Status = ArtifactStatus.Loading
                });
            }

            // 2. Registry (System)
            artifacts.Add(new CollectionArtifact 
            { 
                Name = "Registry (System)", 
                Description = "SYSTEM, SOFTWARE, SAM, SECURITY hives", 
                SourcePath = @"C:\Windows\System32\config", 
                IsDirectory = true,
                RequiresVss = true, 
                ToolRecommendation = "RECmd",
                Category = "Registry",
                Status = ArtifactStatus.Loading
            });

            // 3. Registry (Users)
            foreach (var profile in profiles.Where(p => p.IsSelected))
            {
                // NTUSER.DAT
                string ntuser = Path.Combine(profile.Path, "NTUSER.DAT");
                artifacts.Add(new CollectionArtifact
                {
                    Name = $"Registry ({profile.Name} - NTUSER)",
                    Description = $"User hive for {profile.Name}",
                    SourcePath = ntuser,
                    RequiresVss = true,
                    ToolRecommendation = "RECmd / SBECmd",
                    Category = "Registry (Users)",
                    Status = ArtifactStatus.Loading
                });

                // UsrClass.dat
                string usrclass = Path.Combine(profile.Path, @"AppData\Local\Microsoft\Windows\UsrClass.dat");
                artifacts.Add(new CollectionArtifact
                {
                    Name = $"Registry ({profile.Name} - UsrClass)",
                    Description = $"User class hive for {profile.Name}",
                    SourcePath = usrclass,
                    RequiresVss = true,
                    ToolRecommendation = "RECmd / SBECmd",
                    Category = "Registry (Users)",
                    Status = ArtifactStatus.Loading
                });

                // Browser Data (Chrome)
                string chrome = Path.Combine(profile.Path, @"AppData\Local\Google\Chrome\User Data\Default\History");
                artifacts.Add(new CollectionArtifact
                {
                    Name = $"Chrome History ({profile.Name})",
                    Description = $"Web history for {profile.Name}",
                    SourcePath = chrome,
                    RequiresVss = true,
                    ToolRecommendation = "BrowsingHistoryView",
                    Category = "Web Activity",
                    Status = ArtifactStatus.Loading
                });

                // Browser Data (Edge)
                string edge = Path.Combine(profile.Path, @"AppData\Local\Microsoft\Edge\User Data\Default\History");
                artifacts.Add(new CollectionArtifact
                {
                    Name = $"Edge History ({profile.Name})",
                    Description = $"Web history for {profile.Name}",
                    SourcePath = edge,
                    RequiresVss = true,
                    ToolRecommendation = "BrowsingHistoryView",
                    Category = "Web Activity",
                    Status = ArtifactStatus.Loading
                });

                // Browser Data (Firefox)
                // Firefox uses random profiles, so we need to find the profile folder
                string firefoxBase = Path.Combine(profile.Path, @"AppData\Roaming\Mozilla\Firefox\Profiles");
                if (Directory.Exists(firefoxBase))
                {
                    try
                    {
                        var profilesDirs = Directory.GetDirectories(firefoxBase);
                        foreach (var pDir in profilesDirs)
                        {
                            string places = Path.Combine(pDir, "places.sqlite");
                            if (File.Exists(places))
                            {
                                artifacts.Add(new CollectionArtifact
                                {
                                    Name = $"Firefox History ({profile.Name} - {Path.GetFileName(pDir)})",
                                    Description = $"Web history for {profile.Name}",
                                    SourcePath = places,
                                    RequiresVss = true,
                                    ToolRecommendation = "BrowsingHistoryView",
                                    Category = "Web Activity",
                                    Status = ArtifactStatus.Loading
                                });
                            }
                        }
                    }
                    catch { }
                }


            }

            // 4. Logs
            artifacts.Add(new CollectionArtifact 
            { 
                Name = "Event Logs", 
                Description = "Full Windows Event Log directory", 
                SourcePath = @"C:\Windows\System32\winevt\Logs", 
                IsDirectory = true,
                RequiresVss = true, 
                ToolRecommendation = "EvtxECmd",
                Category = "Logs",
                Status = ArtifactStatus.Loading
            });

            // 5. Execution & Activity
            artifacts.Add(new CollectionArtifact { Name = "Prefetch", Description = "Prefetch files", SourcePath = @"C:\Windows\Prefetch", IsDirectory = true, RequiresVss = true, ToolRecommendation = "PECmd", Category = "Execution", Status = ArtifactStatus.Loading });
            artifacts.Add(new CollectionArtifact { Name = "Amcache", Description = "Amcache.hve", SourcePath = @"C:\Windows\AppCompat\Programs\Amcache.hve", RequiresVss = true, ToolRecommendation = "AmcacheParser", Category = "Execution", Status = ArtifactStatus.Loading });
            artifacts.Add(new CollectionArtifact { Name = "SRUM", Description = "SRUDB.dat", SourcePath = @"C:\Windows\System32\sru\SRUDB.dat", RequiresVss = true, ToolRecommendation = "SrumECmd", Category = "System Usage", Status = ArtifactStatus.Loading });

            // 6. User Activity (Specific folders for each user)
            foreach (var profile in profiles.Where(p => p.IsSelected))
            {
                // LNK Files
                artifacts.Add(new CollectionArtifact
                {
                    Name = $"LNK Files ({profile.Name})",
                    SourcePath = Path.Combine(profile.Path, @"AppData\Roaming\Microsoft\Windows\Recent"),
                    IsDirectory = true,
                    RequiresVss = true,
                    ToolRecommendation = "LECmd",
                    Category = "Activity",
                    Status = ArtifactStatus.Loading
                });

                // JumpLists
                artifacts.Add(new CollectionArtifact
                {
                    Name = $"JumpLists ({profile.Name})",
                    SourcePath = Path.Combine(profile.Path, @"AppData\Roaming\Microsoft\Windows\Recent\AutomaticDestinations"),
                    IsDirectory = true,
                    RequiresVss = true,
                    ToolRecommendation = "JLECmd",
                    Category = "Activity",
                    Status = ArtifactStatus.Loading
                });

                // Timeline
                artifacts.Add(new CollectionArtifact
                {
                    Name = $"Timeline ({profile.Name})",
                    SourcePath = Path.Combine(profile.Path, @"AppData\Local\ConnectedDevicesPlatform"),
                    IsDirectory = true,
                    RequiresVss = true,
                    ToolRecommendation = "WxTCmd",
                    Category = "Activity",
                    Status = ArtifactStatus.Loading
                });
            }


            return artifacts;
        }

        public async Task CollectArtifactsAsync(IEnumerable<CollectionArtifact> artifacts, string destinationPath, Action<string> logCallback, Action<double> progressCallback)
        {
            var selected = artifacts.Where(a => a.IsSelected).ToList();
            if (!selected.Any()) return;

            // Check for Admin privileges
            bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

            string? shadowId = null;
            string? shadowVolume = null;
            string? symlinkPath = null;
            int current = 0;
            string logFile = Path.Combine(destinationPath, "collection_log.txt");

            using (var sw = new StreamWriter(logFile, true))
            {
                sw.WriteLine($"--- Collection Started: {DateTime.Now} ---");
                sw.WriteLine($"[{DateTime.Now}] Elevated (Admin): {isAdmin}");
                if (!isAdmin)
                {
                    logCallback("⚠️ Not running as Administrator. Some locked files (NTFS metadata, registry hives) may not be collected. Run as Admin for best results.");
                    sw.WriteLine($"[{DateTime.Now}] WARNING: Not elevated. /B mode disabled. Locked files may fail.");
                }

                if (isAdmin && selected.Any(a => a.RequiresVss))
                {
                    sw.WriteLine($"[{DateTime.Now}] Creating Volume Shadow Copy for C:...");
                    shadowId = _vssService.CreateSnapshot("C:");
                    if (shadowId != null)
                    {
                        string? devicePath = _vssService.GetSnapshotVolume(shadowId);
                        if (devicePath != null)
                        {
                            sw.WriteLine($"[{DateTime.Now}] Shadow Copy device path: {devicePath}");

                            // Robocopy cannot access \\?\GLOBALROOT\Device\... paths directly.
                            // Create a temporary symlink so robocopy can use a normal path.
                            symlinkPath = _vssService.CreateSymlinkToVolume(devicePath);
                            if (symlinkPath != null)
                            {
                                shadowVolume = symlinkPath;
                                sw.WriteLine($"[{DateTime.Now}] Shadow Copy symlink: {symlinkPath}");
                                logCallback($"Shadow Copy ready: {symlinkPath}");
                            }
                            else
                            {
                                sw.WriteLine($"[{DateTime.Now}] WARNING: Failed to create symlink to shadow volume.");
                                logCallback("[WARNING] Failed to create symlink to shadow volume. Attempting direct copy.");
                            }
                        }
                        else
                        {
                            sw.WriteLine($"[{DateTime.Now}] WARNING: Failed to resolve shadow volume path.");
                            logCallback("[WARNING] Failed to resolve shadow volume path. Attempting direct copy.");
                        }
                    }
                    else
                    {
                        sw.WriteLine($"[{DateTime.Now}] WARNING: Shadow Copy creation failed.");
                        logCallback("[WARNING] Shadow Copy failed. Locked files may skip.");
                    }
                }
                
                foreach (var art in selected)
                {
                    current++;
                    progressCallback((double)current / selected.Count * 100);
                    logCallback($"Collecting {art.Name}...");
                    sw.WriteLine($"[{DateTime.Now}] Starting: {art.Name} ({art.SourcePath})");

                    string source = art.SourcePath;
                    bool usingVss = art.RequiresVss && shadowVolume != null;
                    if (usingVss)
                    {
                        // Replace the drive letter with the symlink path
                        // e.g. C:\Users\harri\NTUSER.DAT -> C:\Temp\GlassDFIR_VSS_xxx\Users\harri\NTUSER.DAT
                        source = Regex.Replace(source, @"^[A-Za-z]:\\", shadowVolume!.TrimEnd('\\') + "\\");
                    }

                    // Only use /B (backup mode) for direct copies without VSS.
                    // VSS shadow copies already provide unlocked access via the symlink.
                    bool useBackup = isAdmin && !usingVss;

                    try
                    {
                        string subDest;
                        int exitCode;

                        if (art.IsDirectory)
                        {
                            subDest = Path.Combine(destinationPath, art.Category.Replace(" ", "_"), art.Name.Replace(" ", "_"));
                            Directory.CreateDirectory(subDest);
                            exitCode = await RunRobocopyAsync(source, subDest, logCallback, sw, useBackup);
                        }
                        else
                        {
                            // Create a subfolder for the artifact to prevent filename collisions (e.g. Chrome History vs Edge History)
                            string artifactDir = Path.Combine(destinationPath, art.Category.Replace(" ", "_"), art.Name.Replace(" ", "_"));
                            Directory.CreateDirectory(artifactDir);
                            exitCode = await RunRobocopyFileAsync(source, artifactDir, Path.GetFileName(art.SourcePath), logCallback, sw, useBackup);
                        }

                        if (exitCode >= 8)
                        {
                            logCallback($"[WARNING] {art.Name} may have failed (Exit: {exitCode})");
                            sw.WriteLine($"[{DateTime.Now}] WARNING: High exit code {exitCode}");
                        }
                        else
                        {
                            sw.WriteLine($"[{DateTime.Now}] Completed (Exit: {exitCode})");
                        }
                    }
                    catch (Exception ex)
                    {
                        logCallback($"[ERROR] Failed to collect {art.Name}: {ex.Message}");
                        sw.WriteLine($"[{DateTime.Now}] ERROR: {art.Name} - {ex.Message}");
                    }
                }
                
                sw.WriteLine($"--- Collection Finished: {DateTime.Now} ---");
            }

            // Clean up: remove the symlink first, then the snapshot
            if (symlinkPath != null)
            {
                _vssService.CleanupSymlink(symlinkPath);
            }
            if (shadowId != null)
            {
                logCallback("Cleaning up Shadow Copy...");
                _vssService.DeleteSnapshot(shadowId);
            }

            logCallback("Collection Complete.");
        }

        private async Task<int> RunRobocopyAsync(string source, string dest, Action<string> logCallback, StreamWriter? logFile = null, bool useBackupMode = false)
        {
            var si = new ProcessStartInfo
            {
                FileName = "robocopy",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            si.ArgumentList.Add(source);
            si.ArgumentList.Add(dest);
            si.ArgumentList.Add("/E");
            si.ArgumentList.Add("/R:1");
            si.ArgumentList.Add("/W:1");
            si.ArgumentList.Add("/NP");
            si.ArgumentList.Add("/NFL");
            si.ArgumentList.Add("/NDL");
            if (useBackupMode) si.ArgumentList.Add("/B");

            string cmd = $"robocopy {string.Join(" ", si.ArgumentList.Select(a => $"\"{a}\""))}";
            logCallback($"[DEBUG] Running: {cmd}");
            logFile?.WriteLine($"[{DateTime.Now}] DEBUG: Executing: {cmd}");

            var proc = new Process { StartInfo = si };
            proc.Start();
            await proc.WaitForExitAsync();
            return proc.ExitCode;
        }

        private async Task<int> RunRobocopyFileAsync(string sourceFile, string destDir, string fileName, Action<string> logCallback, StreamWriter? logFile = null, bool useBackupMode = false)
        {
            string? sDir = Path.GetDirectoryName(sourceFile);
            if (string.IsNullOrEmpty(sDir)) return -1;

            // VSS Root Fix: Ensure trailing slash for volume root
            if (sDir.StartsWith(@"\\?\GLOBALROOT") && !sDir.EndsWith("\\"))
            {
                int shadowIdx = sDir.IndexOf("HarddiskVolumeShadowCopy", StringComparison.OrdinalIgnoreCase);
                if (shadowIdx != -1)
                {
                    string afterShadow = sDir.Substring(shadowIdx);
                    if (!afterShadow.Contains("\\"))
                    {
                        sDir += "\\";
                    }
                }
            }

            var si = new ProcessStartInfo
            {
                FileName = "robocopy",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            si.ArgumentList.Add(sDir);
            si.ArgumentList.Add(destDir);
            si.ArgumentList.Add(fileName);
            si.ArgumentList.Add("/R:1");
            si.ArgumentList.Add("/W:1");
            si.ArgumentList.Add("/NP");
            if (useBackupMode) si.ArgumentList.Add("/B");

            string cmd = $"robocopy {string.Join(" ", si.ArgumentList.Select(a => $"\"{a}\""))}";
            logCallback($"[DEBUG] Running: {cmd}");
            logFile?.WriteLine($"[{DateTime.Now}] DEBUG: Executing: {cmd}");

            var proc = new Process { StartInfo = si };
            proc.Start();
            await proc.WaitForExitAsync();
            return proc.ExitCode;
        }
    }
}
