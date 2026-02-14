using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassDFIR.Models;
using GlassDFIR.Services;
using System.ComponentModel;
using System.Data;
using System.Windows.Data;
using System.Collections.Concurrent;
using System.Text;
using System.Windows.Threading;
using System.Linq;

namespace GlassDFIR.ViewModels
{
    public partial class ToolRunnerViewModel : BladeViewModel
    {
        private readonly ToolModule _tool;
        private readonly ICsvParsingService _csvParsingService;
        private readonly IToolManager _toolManager;
        private readonly MainViewModel _mainViewModel;
        private readonly ICaseManager _caseManager;
        private readonly IVssService _vssService;
        private readonly IBrowserHistoryService _browserHistoryService;
        
        [ObservableProperty]
        private string _outputLog = string.Empty;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private string _inputPath = string.Empty;

        [ObservableProperty]
        private string _outputPath = string.Empty;

        [ObservableProperty]
        private string _elapsedTime = "00:00:00";

        [ObservableProperty]
        private System.Data.DataTable? _results; // Made nullable

        [ObservableProperty]
        private System.ComponentModel.ICollectionView? _resultsView; // Made nullable

        [ObservableProperty]
        private string _filterText = string.Empty;

        // Throttling fields
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly DispatcherTimer _logUpdateTimer;
        private readonly DispatcherTimer _executionTimer;
        private readonly Stopwatch _stopwatch = new();

        partial void OnFilterTextChanged(string value)
        {
            if (ResultsView != null)
                ResultsView.Refresh();
        }



        [ObservableProperty]
        private bool _isHayabusa;

        [ObservableProperty]
        private bool _isMFTECmd;

        [ObservableProperty]
        private bool _isLECmd;

        [ObservableProperty]
        private bool _isLECmdManualMode;

        [ObservableProperty]
        private bool _isJLECmd;

        [ObservableProperty]
        private bool _isJLECmdManualMode;

        [ObservableProperty]
        private bool _isAmcacheParser;

        [ObservableProperty]
        private bool _isSrumECmd;

        [ObservableProperty]
        private bool _isWxTCmd;

        [ObservableProperty]
        private bool _isWxTCmdManualMode;

        [ObservableProperty]
        private bool _isRECmd;

        [ObservableProperty]
        private bool _isRECmdManualMode;

        [ObservableProperty]
        private bool _isSBECmd;

        [ObservableProperty]
        private bool _isSBECmdManualMode;

        [ObservableProperty]
        private bool _isSbeCmdNtUser = true; // Default to Process NTUSER.DAT

        [ObservableProperty]
        private bool _isSbeCmdUsrClass = true; // Default to Process UsrClass.dat

        [ObservableProperty]
        private bool _isSBECmdLiveAnalysis;

        [ObservableProperty]
        private bool _isVolatility;

        [ObservableProperty]
        private bool _isVolatilityLiveMode;

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<PluginOption> _volatilityPlugins = new();

        [ObservableProperty]
        private bool _isYaraScan;

        [ObservableProperty]
        private string _yaraRulesPath = string.Empty;

        [ObservableProperty]
        private string _reCmdBatchPath = string.Empty;

        [ObservableProperty]
        private bool _isReCmdAutoBatch = true; // Default to Auto

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<UserProfile> _userProfiles = new();

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<RegistryHiveOption> _systemHives = new();

        // MFTEcmd Targets
        [ObservableProperty] private string _mftPath = @"C:\$MFT";
        [ObservableProperty] private string _jPath = @"C:\$Extend\$UsnJrnl:$J";
        [ObservableProperty] private string _logFilePath = @"C:\$LogFile";
        [ObservableProperty] private string _bootPath = @"C:\$Boot";
        [ObservableProperty] private string _sdsPath = @"C:\$Secure:$SDS";

        [ObservableProperty]
        private bool _hayabusaVerbose = true; // Default as requested

        [ObservableProperty]
        private bool _hayabusaAllRules = true; // Default as requested

        [ObservableProperty]
        private bool _hayabusaLiveAnalysis;

        [ObservableProperty]
        private bool _isBrowserHistory;

        [ObservableProperty]
        private bool _isChromeSelected = true;

        [ObservableProperty]
        private bool _isEdgeSelected = true;

        [ObservableProperty]
        private bool _isFirefoxSelected = true;

        public ToolRunnerViewModel(ToolModule tool, ICsvParsingService csvParsingService, MainViewModel mainViewModel, IToolManager toolManager, ICaseManager caseManager, IVssService vssService, IBrowserHistoryService browserHistoryService)
        {
            _tool = tool;
            _csvParsingService = csvParsingService;
            _mainViewModel = mainViewModel;
            _toolManager = toolManager;
            _caseManager = caseManager;
            _vssService = vssService;
            _browserHistoryService = browserHistoryService;

            Title = tool.Name;
            Icon = "⚙️";
            InputPath = tool.DefaultInputPath;

            // Isolation: If a case is active, direct output to the case folder
            if (_caseManager.CurrentCase != null)
            {
                OutputPath = System.IO.Path.Combine(_caseManager.CurrentCase.OutputPath, System.IO.Path.GetFileName(tool.DefaultOutputPath));
            }
            else
            {
                OutputPath = tool.DefaultOutputPath;
            }
            IsHayabusa = tool.Name.Equals("Hayabusa", System.StringComparison.OrdinalIgnoreCase);
            IsMFTECmd = tool.Name.Equals("MFTECmd", System.StringComparison.OrdinalIgnoreCase);
            IsLECmd = tool.Name.Equals("LECmd", System.StringComparison.OrdinalIgnoreCase);
            IsJLECmd = _tool.Name.Equals("JLECmd", System.StringComparison.OrdinalIgnoreCase);
            IsAmcacheParser = _tool.Name.Equals("AmcacheParser", System.StringComparison.OrdinalIgnoreCase);
            IsSrumECmd = _tool.Name.Equals("SrumECmd", System.StringComparison.OrdinalIgnoreCase);
            IsWxTCmd = _tool.Name.Equals("WxTCmd", System.StringComparison.OrdinalIgnoreCase);
            IsRECmd = _tool.Name.Equals("RECmd", System.StringComparison.OrdinalIgnoreCase);
            IsSBECmd = _tool.Name.Equals("SBECmd", System.StringComparison.OrdinalIgnoreCase);
            IsBrowserHistory = _tool.Name.Equals("BrowserHistory", System.StringComparison.OrdinalIgnoreCase);
            IsVolatility = _tool.Name.Equals("Volatility", System.StringComparison.OrdinalIgnoreCase);

            if (IsVolatility)
            {
                InitializeVolatilityPlugins();
                // Pre-populate YARA rules path
                var yaraRoot = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Tools", "Yara");
                if (System.IO.Directory.Exists(yaraRoot))
                {
                    var rulesMaster = System.IO.Path.Combine(yaraRoot, "rules-master");
                    if (System.IO.Directory.Exists(rulesMaster))
                    {
                        YaraRulesPath = rulesMaster;
                    }
                    else
                    {
                        YaraRulesPath = yaraRoot;
                    }
                }
            }

            if (IsLECmd || IsJLECmd || IsWxTCmd || IsSBECmd || IsBrowserHistory)
            {
                LoadUserProfiles();
            }

            if (IsRECmd)
            {
                LoadSystemHives();
            }

            if (IsLECmd)
            {
                // Pre-populate manual path with current user's Recent folder
                InputPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Recent");
            }
            if (IsJLECmd)
            {
                 // Pre-populate manual user Recent folder (JLECmd typically targets AutomaticDestinations/CustomDestinations inside Recent)
                 InputPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Recent");
            }
            if (IsWxTCmd)
            {
                // Pre-populate manual path with %LOCALAPPDATA%\ConnectedDevicesPlatform
                InputPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), @"ConnectedDevicesPlatform");
            }

            // Initialize Log Throttling Timer (runs every 250ms)
            _logUpdateTimer = new DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(250)
            };
            _logUpdateTimer.Tick += ProcessLogQueue;

            // Initialize Execution Timer (runs every 1s)
            _executionTimer = new DispatcherTimer
            {
                Interval = System.TimeSpan.FromSeconds(1)
            };
            _executionTimer.Tick += UpdateExecutionTime;
        }

        private void InitializeVolatilityPlugins()
        {
            VolatilityPlugins.Add(new PluginOption("windows.pslist", "Process List (EPROCESS)", true));
            VolatilityPlugins.Add(new PluginOption("windows.pstree", "Process Tree (Hierarchy)", true));
            VolatilityPlugins.Add(new PluginOption("windows.psscan", "Process Scan (Pool)", true));
            VolatilityPlugins.Add(new PluginOption("windows.cmdline", "Command Line Arguments", true));
            VolatilityPlugins.Add(new PluginOption("windows.dlllist", "Loaded DLLs", true));
            VolatilityPlugins.Add(new PluginOption("windows.handles", "Open Handles", true));
            VolatilityPlugins.Add(new PluginOption("windows.netscan", "Network Connections", true));
            VolatilityPlugins.Add(new PluginOption("windows.malfind", "Malware Selection", true));
            VolatilityPlugins.Add(new PluginOption("windows.svcscan", "Service Scan", true));
            VolatilityPlugins.Add(new PluginOption("windows.registry.hivelist", "Registry Hivelist", true));
            VolatilityPlugins.Add(new PluginOption("windows.registry.userassist", "UserAssist Entries", true));
            VolatilityPlugins.Add(new PluginOption("windows.registry.amcache", "Amcache Analysis", true));
        }

        [RelayCommand]
        private void Close()
        {
            _mainViewModel.CloseBlade(this);
        }

        private bool FilterData(object obj)
        {
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            if (obj is System.Data.DataRowView row)
            {
                foreach (var item in row.Row.ItemArray)
                {
                    if (item != null && item.ToString()?.Contains(FilterText, System.StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
            }
            return false;
        }

        [RelayCommand]
        private void BrowseInput()
        {
            // If Hayabusa or MFTEcmd or LECmd or JLECmd or directory-based tool, try folder picker first
            if (IsHayabusa || IsMFTECmd || IsLECmd || IsJLECmd || _tool.DefaultInputPath.Contains("Logs") || _tool.DefaultInputPath.Contains("Prefetch"))
            {
                // .NET 9 / Modern Windows Folder Picker
                var dialog = new Microsoft.Win32.OpenFolderDialog();
                if (dialog.ShowDialog() == true)
                {
                    InputPath = dialog.FolderName;
                }
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                if (dialog.ShowDialog() == true)
                {
                    InputPath = dialog.FileName;
                }
            }
        }

        [RelayCommand]
        private void BrowseOutput()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = System.IO.Path.GetFileName(OutputPath),
                InitialDirectory = System.IO.Path.GetDirectoryName(OutputPath),
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };
            
            if (dialog.ShowDialog() == true)
            {
                OutputPath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void BrowseMFT() => BrowseFileOrFolder(path => MftPath = path);

        [RelayCommand]
        private void BrowseJ() => BrowseFileOrFolder(path => JPath = path);

        [RelayCommand]
        private void BrowseLogFile() => BrowseFileOrFolder(path => LogFilePath = path);

        [RelayCommand]
        private void BrowseBoot() => BrowseFileOrFolder(path => BootPath = path);

        [RelayCommand]
        private void BrowseSDS() => BrowseFileOrFolder(path => SdsPath = path);

        [RelayCommand]
        private void BrowseYaraRules() => BrowseFileOrFolder(path => YaraRulesPath = path);

        private void BrowseFileOrFolder(System.Action<string> updateAction)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*",
                CheckFileExists = false // Allow paths like \$MFT which might not be directly selectable in standard dialog
            };
            if (dialog.ShowDialog() == true)
            {
                updateAction(dialog.FileName);
            }
        }

        [RelayCommand]
        private async Task Execute()
        {
            if (IsRunning) return;
            IsRunning = true;
            StatusMessage = "Initializing...";
            OutputLog = string.Empty;
            ElapsedTime = "00:00:00";
            
            // Clear queue and start timers
            while (_logQueue.TryDequeue(out _)) { }
            _stopwatch.Restart();
            _logUpdateTimer.Start();
            _executionTimer.Start();

            AppendLog($"Starting {_tool.Name} (v1.1-elevation-fix)...\n");

            // Check for Admin privileges if targeting system files/logs
            bool requiresAdmin = IsMFTECmd || 
                               (IsHayabusa && HayabusaLiveAnalysis) || 
                               (InputPath != null && InputPath.Contains("System32", System.StringComparison.OrdinalIgnoreCase)) ||
                               _tool.Name.Equals("AmcacheParser", System.StringComparison.OrdinalIgnoreCase) ||
                               _tool.Name.Equals("AppCompatCacheParser", System.StringComparison.OrdinalIgnoreCase) ||
                               _tool.Name.Equals("PECmd", System.StringComparison.OrdinalIgnoreCase) ||
                               IsWxTCmd ||
                               IsRECmd ||
                               IsLECmd ||
                               IsJLECmd ||
                               IsSBECmd ||
                               IsVolatility ||
                               IsYaraScan;

            AppendLog($"[DEBUG] IsVolatility: {IsVolatility}, LiveMode: {IsVolatilityLiveMode}, AdminRequired: {requiresAdmin}\n");

            if (!IsUserAnAdmin() && requiresAdmin)
            {
                AppendLog("WARNING: You are NOT running as Administrator. Elevated privileges are required for system-level forensics.\n");

                var result = System.Windows.MessageBox.Show(
                    "Administrative privileges are required for this tool to function correctly (accessing NTFS metadata, system logs, etc.).\n\nWould you like to restart GlassDFIR as Administrator?",
                    "Elevation Required",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    RestartAsAdmin();
                }
                else
                {
                    AppendLog("Execution CANCELLED: Elevation is required to access locked forensic artifacts.\n");
                }
                return;
            }

            AppendLog($"Current Elevation (Admin): {IsUserAnAdmin()}\n");

            try
            {
                if (IsVolatility && !IsVolatilityLiveMode && string.IsNullOrWhiteSpace(InputPath))
                {
                    AppendLog("[ERROR] No memory file selected. Please browse for an image or enable 'Live Memory Capture'.\n");
                    IsRunning = false;
                    return;
                }

                if (IsYaraScan && !IsVolatilityLiveMode && string.IsNullOrWhiteSpace(InputPath))
                {
                    AppendLog("[ERROR] No input file selected for YARA scan.\n");
                    IsRunning = false;
                    return;
                }

                string args = _tool.ArgumentsTemplate;
                
                // Specific Logic for Hayabusa to use check boxes
                if (IsHayabusa)
                {
                    // Core command
                    args = "csv-timeline";
                    
                    // Input logic
                    if (HayabusaLiveAnalysis)
                    {
                        args += " --live-analysis";
                    }
                    else if (!string.IsNullOrWhiteSpace(InputPath))
                    {
                        if (System.IO.Directory.Exists(InputPath))
                        {
                            args += $" -d \"{InputPath}\"";
                        }
                        else
                        {
                            args += $" -f \"{InputPath}\"";
                        }
                    }

                    // Output
                    args += $" -o \"{OutputPath}\"";

                    // Flags
                    if (HayabusaVerbose) args += " -v";
                    if (HayabusaAllRules) args += " --enable-all-rules"; // -A
                    
                    // Defaults for non-interactive
                    args += " --no-wizard --clobber";
                }
                else if (IsMFTECmd)
                {
                    // MFTEcmd multi-target execution is handled inside the Task.Run below
                    // We just need a placeholder here if needed, but we'll use a loop.
                }
                else
                {
                    // Standard replacement
                    args = args
                        .Replace("{Input}", InputPath)
                        .Replace("{Output}", OutputPath) // For Tools that take full path
                        .Replace("{OutputFolder}", System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".")
                        .Replace("{OutputFile}", System.IO.Path.GetFileName(OutputPath));

                    if (IsRECmd && !string.IsNullOrWhiteSpace(ReCmdBatchPath) && !SystemHives.Any(h => h.IsSelected))
                    {
                        // Fallback for when no specific hives are selected (use global InputPath)
                        if (System.IO.File.Exists(ReCmdBatchPath))
                            args += $" --bn \"{ReCmdBatchPath}\"";
                        else
                            args += $" --bn \"{ReCmdBatchPath}\"";
                    }
                }

                // Suppress generic log for tools that handle their own commands in loops
                bool isMultiTarget = (IsLECmd && !IsLECmdManualMode) || 
                                     (IsJLECmd && !IsJLECmdManualMode) || 
                                     (IsWxTCmd && !IsWxTCmdManualMode) || 
                                     (IsSBECmd && !IsSBECmdManualMode) || 
                                     (IsRECmd && SystemHives.Any(h => h.IsSelected)) ||
                                     IsMFTECmd ||
                                     IsVolatility ||
                                     IsBrowserHistory ||
                                     IsYaraScan;

                if (!isMultiTarget) AppendLog($"Command: {_tool.ExePath} {args}\n\n");

                await Task.Run(async () =>
                {
                    if (IsLECmd && !IsLECmdManualMode)
                    {
                        var selectedProfiles = UserProfiles.Where(u => u.IsSelected).ToList();
                        if (selectedProfiles.Count == 0)
                        {
                            AppendLog("No user profiles selected.\n");
                            return;
                        }

                        string outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".";
                        string baseFileName = System.IO.Path.GetFileNameWithoutExtension(OutputPath);
                        var tempCsvFiles = new System.Collections.Generic.List<string>();

                        bool anySuccess = false;

                        foreach (var profile in selectedProfiles)
                        {
                            StatusMessage = $"Processing User: {profile.Name}...";
                            string userRecentPath = System.IO.Path.Combine(profile.Path, @"AppData\Roaming\Microsoft\Windows\Recent");

                            if (!System.IO.Directory.Exists(userRecentPath))
                            {
                                AppendLog($"[SKIP] Recent folder not found for user {profile.Name} at: {userRecentPath}\n");
                                continue;
                            }

                            string tempOutputFile = $"{baseFileName}_{profile.Name}.csv";
                            string fullTempOutputPath = System.IO.Path.Combine(outputFolder, tempOutputFile);
                            
                            // LECmd arguments for this specific user
                            // Note: cmd /c wrapper logic is still needed
                            string cmdArgs = $"-d \"{userRecentPath}\" --csv \"{outputFolder}\" --csvf \"{tempOutputFile}\"";
                            string finalArgs = $"/c \"\"LECmd.exe\" {cmdArgs}\""; 

                            AppendLog($"\n--- Processing User: {profile.Name} ---\n");
                            AppendLog($"Command: cmd.exe {finalArgs}\n");

                            using var process = new Process();
                            process.StartInfo.FileName = "cmd.exe";
                            process.StartInfo.Arguments = finalArgs;
                            process.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(_tool.ExePath);
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.CreateNoWindow = true;

                            process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data); };
                            process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            await process.WaitForExitAsync();

                            if (System.IO.File.Exists(fullTempOutputPath))
                            {
                                AppendLog($"[SUCCESS] Generated CSV for {profile.Name}.\n");
                                tempCsvFiles.Add(fullTempOutputPath);
                                anySuccess = true;
                            }
                            else
                            {
                                AppendLog($"[WARNING] Expected CSV not found for {profile.Name}.\n");
                            }
                        }

                        if (anySuccess)
                        {
                            MergeCsvFiles(tempCsvFiles, OutputPath, baseFileName);
                        }

                        StatusMessage = "Completed";
                    }
                    else if (IsJLECmd && !IsJLECmdManualMode)
                    {
                         var selectedProfiles = UserProfiles.Where(u => u.IsSelected).ToList();
                        if (selectedProfiles.Count == 0)
                        {
                            AppendLog("No user profiles selected.\n");
                            return;
                        }

                        string outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".";
                        string baseFileName = System.IO.Path.GetFileNameWithoutExtension(OutputPath);
                        var tempCsvFiles = new System.Collections.Generic.List<string>();

                        bool anySuccess = false;

                        foreach (var profile in selectedProfiles)
                        {
                            StatusMessage = $"Processing User: {profile.Name}...";
                            string userRecentPath = System.IO.Path.Combine(profile.Path, @"AppData\Roaming\Microsoft\Windows\Recent");

                            if (!System.IO.Directory.Exists(userRecentPath))
                            {
                                AppendLog($"[SKIP] Recent folder not found for user {profile.Name} at: {userRecentPath}\n");
                                continue;
                            }

                            string tempOutputFile = $"{baseFileName}_{profile.Name}.csv";
                            string fullTempOutputPath = System.IO.Path.Combine(outputFolder, tempOutputFile);
                            
                            // JLECmd arguments: -d "Directory" --csv ...
                            // We use -q for quiet if supported, JLECmd supports it.
                            string cmdArgs = $"-d \"{userRecentPath}\" --csv \"{outputFolder}\" --csvf \"{tempOutputFile}\"";
                            
                            // JLECmd is also a Zimmerman tool, so it likely needs the same cmd /c wrapper fix for windowless execution
                            string finalArgs = $"/c \"\"JLECmd.exe\" {cmdArgs}\""; 

                            AppendLog($"\n--- Processing User: {profile.Name} ---\n");
                            AppendLog($"Command: cmd.exe {finalArgs}\n");

                            using var process = new Process();
                            process.StartInfo.FileName = "cmd.exe";
                            process.StartInfo.Arguments = finalArgs;
                            process.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(_tool.ExePath);
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.CreateNoWindow = true;

                            process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data); };
                            process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            await process.WaitForExitAsync();

                            if (System.IO.File.Exists(fullTempOutputPath))
                            {
                                AppendLog($"[SUCCESS] Generated CSV for {profile.Name}.\n");
                                tempCsvFiles.Add(fullTempOutputPath);
                                anySuccess = true;
                            }
                            else
                            {
                                AppendLog($"[WARNING] Expected CSV not found for {profile.Name}.\n");
                            }
                        }

                        if (anySuccess)
                        {
                            MergeCsvFiles(tempCsvFiles, OutputPath, baseFileName);
                        }

                        StatusMessage = "Completed";
                    }
                    else if (IsWxTCmd && !IsWxTCmdManualMode)
                    {
                        var selectedProfiles = UserProfiles.Where(u => u.IsSelected).ToList();
                        if (selectedProfiles.Count == 0)
                        {
                            AppendLog("No user profiles selected.\n");
                            return;
                        }

                        string outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".";
                        string baseFileName = System.IO.Path.GetFileNameWithoutExtension(OutputPath);
                        var tempCsvFiles = new System.Collections.Generic.List<string>();

                        bool anySuccess = false;

                        foreach (var profile in selectedProfiles)
                        {
                            StatusMessage = $"Processing User: {profile.Name}...";
                            string userBaseTimelinePath = System.IO.Path.Combine(profile.Path, @"AppData\Local\ConnectedDevicesPlatform");

                            if (!System.IO.Directory.Exists(userBaseTimelinePath))
                            {
                                AppendLog($"[SKIP] Timeline folder not found for user {profile.Name} at: {userBaseTimelinePath}\n");
                                continue;
                            }

                            // WxTCmd requires a specific file (-f). We need to find ActivitiesCache.db
                            // It's usually in a subfolder like L.username or L.default
                            var dbFiles = System.IO.Directory.GetFiles(userBaseTimelinePath, "ActivitiesCache.db", System.IO.SearchOption.AllDirectories);
                            if (dbFiles.Length == 0)
                            {
                                AppendLog($"[SKIP] ActivitiesCache.db not found for user {profile.Name} in {userBaseTimelinePath}\n");
                                continue;
                            }
                            string dbPath = dbFiles[0];

                            // Track existing CSVs to find the new one generated by WxTCmd (since it doesn't support --csvf)
                            // We look for any CSV in the output folder to see what just appeared
                            var existingCsvs = System.IO.Directory.GetFiles(outputFolder, "*.csv").ToHashSet();

                            // WxTCmd arguments: -f "File" --csv "Directory"
                            string cmdArgs = $"-f \"{dbPath}\" --csv \"{outputFolder}\"";
                            string finalArgs = $"/c \"\"WxTCmd.exe\" {cmdArgs}\""; 

                            AppendLog($"\n--- Processing User: {profile.Name} ---\n");
                            AppendLog($"Command: cmd.exe {finalArgs}\n");

                            using var process = new Process();
                            process.StartInfo.FileName = "cmd.exe";
                            process.StartInfo.Arguments = finalArgs;
                            process.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(_tool.ExePath);
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.CreateNoWindow = true;

                            process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data); };
                            process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            await process.WaitForExitAsync();

                            // Find the newly created CSVs. WxTCmd can create multiple. 
                            // We want the primary one: _Activity.csv
                            var currentCsvs = System.IO.Directory.GetFiles(outputFolder, "*.csv");
                            var newCsvs = currentCsvs.Where(f => !existingCsvs.Contains(f)).ToList();
                            
                            // Prefer the one ending with _Activity.csv
                            var mainCsv = newCsvs.FirstOrDefault(f => f.EndsWith("_Activity.csv", System.StringComparison.OrdinalIgnoreCase)) ?? newCsvs.FirstOrDefault();

                            if (mainCsv != null && System.IO.File.Exists(mainCsv))
                            {
                                // Rename to a consistent temp name for merging
                                string tempOutputFile = System.IO.Path.Combine(outputFolder, $"{baseFileName}_{profile.Name}.csv");
                                try 
                                { 
                                    if (System.IO.File.Exists(tempOutputFile)) System.IO.File.Delete(tempOutputFile);
                                    System.IO.File.Move(mainCsv, tempOutputFile);
                                    
                                    // Clean up other WxTCmd outputs for this run to avoid clutter
                                    foreach (var extraCsv in newCsvs.Where(f => f != mainCsv))
                                    {
                                        try { System.IO.File.Delete(extraCsv); } catch { }
                                    }

                                    AppendLog($"[SUCCESS] Generated CSV for {profile.Name}.\n");
                                    tempCsvFiles.Add(tempOutputFile);
                                    anySuccess = true;
                                } 
                                catch (System.Exception ex)
                                {
                                    AppendLog($"[ERROR] Failed to stage WxTCmd output: {ex.Message}\n");
                                }
                            }
                            else
                            {
                                AppendLog($"[WARNING] Expected WxTCmd CSV not found for {profile.Name}.\n");
                            }
                        }

                        if (anySuccess)
                        {
                            MergeCsvFiles(tempCsvFiles, OutputPath, baseFileName);
                        }

                        StatusMessage = "Completed";
                    }
                    else if (IsSBECmd && !IsSBECmdManualMode)
                    {
                        string outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".";
                        string baseFileName = System.IO.Path.GetFileNameWithoutExtension(OutputPath);

                        if (IsSBECmdLiveAnalysis)
                        {
                            StatusMessage = "Processing Live Registry...";
                            string tempOutputFile = System.IO.Path.Combine(outputFolder, $"{baseFileName}_Live.csv");

                            // SBECmd arguments: -l --csv ...
                            string cmdArgs = $"-l --csv \"{outputFolder}\" --csvf \"{System.IO.Path.GetFileName(tempOutputFile)}\" --nl";

                            AppendLog($"\n--- Processing Live Registry (All Loaded Hives) ---\n");
                            AppendLog($"Command: {_tool.ExePath} {cmdArgs}\n");
                            
                            using var process = new Process();
                            process.StartInfo.FileName = _tool.ExePath;
                            process.StartInfo.Arguments = cmdArgs;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.CreateNoWindow = true;

                            process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data); };
                            process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            await process.WaitForExitAsync();

                            if (System.IO.File.Exists(tempOutputFile))
                            {
                                try
                                {
                                    if (System.IO.File.Exists(OutputPath)) System.IO.File.Delete(OutputPath);
                                    System.IO.File.Move(tempOutputFile, OutputPath);
                                    AppendLog($"\n[SUCCESS] Live ShellBags saved to: {OutputPath}\n");
                                }
                                catch (System.Exception ex)
                                {
                                    AppendLog($"\n[WARNING] Could not rename live output to final path: {ex.Message}\n");
                                    AppendLog($"           Result remains at: {tempOutputFile}\n");
                                }
                            }

                            StatusMessage = "Completed";
                            return; // Live analysis is a single-shot command
                        }

                        var selectedProfiles = UserProfiles.Where(u => u.IsSelected).ToList();
                        if (selectedProfiles.Count == 0)
                        {
                            AppendLog("No user profiles selected.\n");
                            return;
                        }

                        var tempCsvFiles = new System.Collections.Generic.List<string>();

                        bool anySuccess = false;

                        foreach (var profile in selectedProfiles)
                        {
                            StatusMessage = $"Processing User: {profile.Name}...";
                            string ntuserPath = System.IO.Path.Combine(profile.Path, "NTUSER.DAT");
                            string usrClassPath = System.IO.Path.Combine(profile.Path, @"AppData\Local\Microsoft\Windows\UsrClass.dat");

                            var hivesToProcess = new System.Collections.Generic.List<(string Name, string Path)>();
                            if (IsSbeCmdNtUser && System.IO.File.Exists(ntuserPath)) hivesToProcess.Add(("NTUSER", ntuserPath));
                            if (IsSbeCmdUsrClass && System.IO.File.Exists(usrClassPath)) hivesToProcess.Add(("UsrClass", usrClassPath));

                            if (hivesToProcess.Count == 0)
                            {
                                AppendLog($"[SKIP] No selected ShellBag hives found for user {profile.Name}.\n");
                                continue;
                            }

                            foreach (var hive in hivesToProcess)
                            {
                                string userTempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"GlassDFIR_SBECmd_{profile.Name}_{hive.Name}_{System.Guid.NewGuid()}");
                                System.IO.Directory.CreateDirectory(userTempDir);

                                try
                                {
                                    // Use 'robocopy /B' to handle locked hives (NTUSER.DAT/UsrClass.dat)
                                    // robocopy [Source] [Dest] [File] /B
                                    string sourceDir = System.IO.Path.GetDirectoryName(hive.Path) ?? "";
                                    string fileName = System.IO.Path.GetFileName(hive.Path);
                                    
                                    var roboStartInfo = new ProcessStartInfo
                                    {
                                        FileName = "robocopy.exe",
                                        Arguments = $"\"{sourceDir}\" \"{userTempDir}\" \"{fileName}\" /B /R:0 /W:0",
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true
                                    };

                                    AppendLog($"[STAGING] Attempting to capture locked hive {hive.Name} via robocopy /B...\n");

                                    using (var roboProcess = Process.Start(roboStartInfo))
                                    {
                                        if (roboProcess != null)
                                        {
                                            string roboOutput = await roboProcess.StandardOutput.ReadToEndAsync();
                                            await roboProcess.WaitForExitAsync();

                                            // Robocopy success codes are 0-7. 8+ indicates failures/errors.
                                            if (roboProcess.ExitCode >= 8)
                                            {
                                                AppendLog($"[DEBUG] Robocopy Exit Code: {roboProcess.ExitCode}\n");
                                                AppendLog($"[DEBUG] Robocopy Output: {roboOutput}\n");
                                            }
                                        }
                                    }

                                    string destinationFile = System.IO.Path.Combine(userTempDir, fileName);
                                    if (!System.IO.File.Exists(destinationFile))
                                    {
                                        AppendLog($"[WARNING] Robocopy failed to capture {hive.Name}. Attempting secondary stream-based copy...\n");

                                        // Fallback to stream copy if robocopy fails (e.g. not running as Admin)
                                        try
                                        {
                                            using (var sourceStream = new System.IO.FileStream(hive.Path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                                            using (var destinationStream = new System.IO.FileStream(destinationFile, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
                                            {
                                                await sourceStream.CopyToAsync(destinationStream);
                                            }
                                        }
                                        catch (System.Exception ex)
                                        {
                                            AppendLog($"[ERROR] Failed to stage hive {hive.Name} for user {profile.Name}. Both robocopy and stream copy failed.\n");
                                            AppendLog($"        Error Detail: {ex.Message}\n");
                                            continue;
                                        }
                                    }

                                    string tempOutputFile = System.IO.Path.Combine(outputFolder, $"{baseFileName}_{profile.Name}_{hive.Name}.csv");
                                    string cmdArgs = $"-d \"{userTempDir}\" --csv \"{outputFolder}\" --csvf \"{System.IO.Path.GetFileName(tempOutputFile)}\" --nl";

                                    AppendLog($"\n--- Processing User: {profile.Name} ({hive.Name}) ---\n");
                                    
                                    using var process = new Process();
                                    process.StartInfo.FileName = _tool.ExePath;
                                    process.StartInfo.Arguments = cmdArgs;
                                    process.StartInfo.UseShellExecute = false;
                                    process.StartInfo.RedirectStandardOutput = true;
                                    process.StartInfo.RedirectStandardError = true;
                                    process.StartInfo.CreateNoWindow = true;

                                    process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data); };
                                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };

                                    process.Start();
                                    process.BeginOutputReadLine();
                                    process.BeginErrorReadLine();

                                    await process.WaitForExitAsync();

                                    if (System.IO.File.Exists(tempOutputFile))
                                    {
                                        AppendLog($"[SUCCESS] Generated CSV for {profile.Name} ({hive.Name}).\n");
                                        tempCsvFiles.Add(tempOutputFile);
                                        anySuccess = true;
                                    }
                                }
                                finally
                                {
                                    try { System.IO.Directory.Delete(userTempDir, true); } catch { }
                                }
                            }
                        }

                        if (anySuccess)
                        {
                            MergeCsvFiles(tempCsvFiles, OutputPath, baseFileName, "User_Hive");
                        }

                        StatusMessage = "Completed";
                    }
                    else if (IsRECmd && SystemHives.Any(h => h.IsSelected))
                    {
                        if (!IsReCmdAutoBatch && string.IsNullOrWhiteSpace(ReCmdBatchPath))
                        {
                            AppendLog("\n[ERROR] RECmd requires a Batch File (.reb) or Batch Name (e.g., 'RegistryHives') to process registry hives.\n");
                            AppendLog("Please select or enter a batch configuration in the tool options before running.\n");
                            StatusMessage = "Error: Batch Required";
                            return;
                        }

                        var selectedHives = SystemHives.Where(h => h.IsSelected).ToList();
                        string outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".";
                        string baseFileName = System.IO.Path.GetFileNameWithoutExtension(OutputPath);
                        var tempCsvFiles = new System.Collections.Generic.List<string>();

                        bool anySuccess = false;

                        foreach (var hive in selectedHives)
                        {
                            if (!System.IO.File.Exists(hive.Path))
                            {
                                AppendLog($"[SKIP] Hive not found: {hive.Path}\n");
                                continue;
                            }

                            StatusMessage = $"Processing Hive: {hive.Name}...";
                            string tempOutputFile = $"{baseFileName}_{hive.Name}.csv";
                            string fullTempOutputPath = System.IO.Path.Combine(outputFolder, tempOutputFile);

                            // RECmd arguments for this specific hive
                            string cmdArgs = $"-f \"{hive.Path}\" --csv \"{outputFolder}\" --csvf \"{tempOutputFile}\" --nl";

                            // Batch Selection Logic
                            if (IsReCmdAutoBatch)
                            {
                                string batchName = "RegistryASEPs.reb"; // Default fallback
                                if (hive.Name.Equals("SYSTEM", System.StringComparison.OrdinalIgnoreCase)) batchName = "SystemASEPs.reb";
                                else if (hive.Name.Equals("SOFTWARE", System.StringComparison.OrdinalIgnoreCase)) batchName = "SoftwareASEPs.reb";
                                
                                string batchPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_tool.ExePath) ?? "", "BatchExamples", batchName);
                                if (System.IO.File.Exists(batchPath))
                                    cmdArgs += $" --bn \"{batchPath}\"";
                                else
                                    cmdArgs += $" --bn \"{batchName}\""; // Attempt to use name if file not found at expected path
                            }
                            else if (!string.IsNullOrWhiteSpace(ReCmdBatchPath))
                            {
                                if (System.IO.File.Exists(ReCmdBatchPath))
                                    cmdArgs += $" --bn \"{ReCmdBatchPath}\"";
                                else
                                    cmdArgs += $" --bn \"{ReCmdBatchPath}\"";
                            }

                            AppendLog($"\n--- Processing Hive: {hive.Name} ---\n");
                            AppendLog($"Command: {_tool.ExePath} {cmdArgs}\n");

                            using var process = new Process();
                            process.StartInfo.FileName = _tool.ExePath;
                            process.StartInfo.Arguments = cmdArgs;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.CreateNoWindow = true;

                            process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data); };
                            process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            await process.WaitForExitAsync();

                            if (System.IO.File.Exists(fullTempOutputPath))
                            {
                                AppendLog($"[SUCCESS] Generated RECmd CSV for {hive.Name}.\n");
                                tempCsvFiles.Add(fullTempOutputPath);
                                anySuccess = true;
                            }
                        }

                        if (anySuccess)
                        {
                            MergeCsvFiles(tempCsvFiles, OutputPath, baseFileName, "Hive");
                        }

                        StatusMessage = "Completed";
                    }
                    else if (IsMFTECmd)
                    {
                        var targets = new (string Path, string Label)[]
                        {
                            (MftPath, "MFT"),
                            (JPath, "J"),
                            (LogFilePath, "LogFile"),
                            (BootPath, "Boot"),
                            (SdsPath, "SDS")
                        };

                        string outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".";
                        string baseFileName = System.IO.Path.GetFileNameWithoutExtension(OutputPath);

                        foreach (var target in targets)
                        {
                            if (string.IsNullOrWhiteSpace(target.Path)) continue;

                            // Log availability check
                            if (!System.IO.File.Exists(target.Path) && !System.IO.Directory.Exists(target.Path))
                            {
                                // Special check for streams like :$J
                                bool streamExists = target.Path.Contains(":") && (target.Path.Contains("$J") || target.Path.Contains("$SDS"));
                                
                                if (!streamExists)
                                {
                                    AppendLog($"[SKIP] Target ${target.Label} not found at: {target.Path}\n");
                                    continue;
                                }
                            }

                            StatusMessage = $"Processing ${target.Label}...";
                            string targetOutputFile = $"{baseFileName}_{target.Label}.csv";
                            string targetArgs = $"-f \"{target.Path}\" --csv \"{outputFolder}\" --csvf \"{targetOutputFile}\"";

                            AppendLog($"\n--- Processing ${target.Label} ---\n");
                            AppendLog($"Command: {_tool.ExePath} {targetArgs}\n");

                            using var process = new Process();
                            process.StartInfo.FileName = _tool.ExePath;
                            process.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(_tool.ExePath);
                            process.StartInfo.Arguments = targetArgs;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.CreateNoWindow = true;

                            process.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data); };
                            process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            await process.WaitForExitAsync();

                            // Verify output
                            string fullTargetOutputPath = System.IO.Path.Combine(outputFolder, targetOutputFile);
                            if (System.IO.File.Exists(fullTargetOutputPath))
                            {
                                AppendLog($"[SUCCESS] Generated ${target.Label} CSV.\n");
                            }
                            else
                            {
                                AppendLog($"[WARNING] Expected CSV not found for ${target.Label}. MFTEcmd may have failed to parse the source or access it.\n");
                            }
                        }
                        StatusMessage = "Completed";
                    }
                    else if (IsVolatility)
                    {
                        string outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".";
                        if (!System.IO.Directory.Exists(outputFolder)) System.IO.Directory.CreateDirectory(outputFolder);

                        string? memoryFile = InputPath;

                        if (IsVolatilityLiveMode)
                        {
                            StatusMessage = "Capturing Live Memory...";
                            string winpmemExe = _tool.ExePath.Replace("vol.exe", "winpmem.exe"); 
                            if (!System.IO.File.Exists(winpmemExe))
                            {
                                var allTools = _toolManager.GetAvailableTools();
                                winpmemExe = allTools.FirstOrDefault(t => t.Name == "WinPmem")?.ExePath ?? string.Empty;
                            }

                            if (string.IsNullOrEmpty(winpmemExe) || !System.IO.File.Exists(winpmemExe))
                            {
                                AppendLog("[ERROR] WinPmem executable not found. Live capture aborted.\n");
                                return;
                            }

                             memoryFile = System.IO.Path.Combine(outputFolder, $"LiveMemory_{System.DateTime.Now:yyyyMMdd_HHmmss}.raw");
                             AppendLog($"[LIVE CAPTURE] Running WinPmem to {memoryFile}...\n");
                             
                             // Ensure output directory exists!
                             if (!System.IO.Directory.Exists(outputFolder)) 
                             {
                                 System.IO.Directory.CreateDirectory(outputFolder);
                             }

                             using var winProcess = new Process();
                             winProcess.StartInfo.FileName = winpmemExe;
                             winProcess.StartInfo.Arguments = $"\"{memoryFile}\"";
                             winProcess.StartInfo.UseShellExecute = false;
                             winProcess.StartInfo.RedirectStandardOutput = true;
                             winProcess.StartInfo.RedirectStandardError = true;
                             winProcess.StartInfo.CreateNoWindow = true;

                             winProcess.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog($"[WinPmem] {e.Data}\n"); };
                             winProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"[WinPmem ERROR] {e.Data}\n"); };

                             winProcess.Start();
                             winProcess.BeginOutputReadLine();
                             winProcess.BeginErrorReadLine();
                             
                             await winProcess.WaitForExitAsync();
                             AppendLog($"[DEBUG] WinPmem Exit Code: {winProcess.ExitCode}\n");
                             
                             if (!System.IO.File.Exists(memoryFile))
                             {
                                 AppendLog("[ERROR] Memory capture failed (File not found). Volatility execution aborted.\n");
                                 return;
                             }
                             AppendLog("[SUCCESS] Memory captured successfully.\n");
                        }

                        var selectedPlugins = VolatilityPlugins.Where(p => p.IsSelected).ToList();
                        if (selectedPlugins.Count == 0)
                        {
                            AppendLog("No Volatility plugins selected.\n");
                            return;
                        }

                        foreach (var plugin in selectedPlugins)
                        {
                            StatusMessage = $"Running Volatility: {plugin.Name}...";
                            string pluginSimpleName = plugin.Name.Split('.').Last();
                            string pluginOutputFile = $"Volatility_{pluginSimpleName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
                            string fullPluginOutputPath = System.IO.Path.Combine(outputFolder, pluginOutputFile);

                            string? symbolDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_tool.ExePath) ?? "", "symbols");
                            string symbolArg = System.IO.Directory.Exists(symbolDir) ? $"-s \"{symbolDir}\"" : "";
                            string volArgs = $"-f \"{memoryFile}\" {symbolArg} -r csv windows.{pluginSimpleName}";
                            
                            AppendLog($"\n--- Running Plugin: {plugin.Name} ---\n");
                            AppendLog($"Command: {_tool.ExePath} {volArgs}\n");

                            using var process = new Process();
                            process.StartInfo.FileName = _tool.ExePath;
                            process.StartInfo.Arguments = volArgs;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.CreateNoWindow = true;

                            StringBuilder csvOutput = new StringBuilder();
                            process.OutputDataReceived += (s, e) => 
                            { 
                                if (e.Data != null) 
                                {
                                    AppendLog(e.Data); 
                                    csvOutput.AppendLine(e.Data);
                                }
                            };
                            process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };

                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            await process.WaitForExitAsync();

                            if (csvOutput.Length > 0)
                            {
                                System.IO.File.WriteAllText(fullPluginOutputPath, csvOutput.ToString());
                                AppendLog($"[SUCCESS] Saved CSV to {pluginOutputFile}\n");
                            }
                        }

                        if (IsYaraScan)
                        {
                            if (!string.IsNullOrEmpty(memoryFile))
                        {
                            await RunYaraScanInternal(memoryFile, outputFolder);
                        }
                        }

                        StatusMessage = "Completed";
                    }
                    else if (IsYaraScan)
                    {
                        string outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".";
                        if (!string.IsNullOrEmpty(InputPath))
                        {
                            await RunYaraScanInternal(InputPath, outputFolder);
                        }
                    }
                    else if (IsBrowserHistory)
                    {
                        var selectedProfiles = UserProfiles.Where(u => u.IsSelected).ToList();
                        if (selectedProfiles.Count == 0)
                        {
                            AppendLog("No user profiles selected.\n");
                            return;
                        }

                        StatusMessage = "Extracting Browser History...";
                        var allHistory = new System.Collections.Generic.List<BrowserHistoryItem>();

                        foreach (var profile in selectedProfiles)
                        {
                            AppendLog($"Processing User: {profile.Name}...\n");

                            // Chrome
                            if (IsChromeSelected)
                            {
                                string chromePath = System.IO.Path.Combine(profile.Path, @"AppData\Local\Google\Chrome\User Data\Default\History");
                                if (System.IO.File.Exists(chromePath))
                                {
                                    AppendLog($"  - Extracting Chrome History...\n");
                                    var items = _browserHistoryService.GetHistory(chromePath, BrowserType.Chrome, profile.Name);
                                    allHistory.AddRange(items);
                                    AppendLog($"    Found {items.Count} items.\n");
                                }
                            }

                            // Edge
                            if (IsEdgeSelected)
                            {
                                string edgePath = System.IO.Path.Combine(profile.Path, @"AppData\Local\Microsoft\Edge\User Data\Default\History");
                                if (System.IO.File.Exists(edgePath))
                                {
                                    AppendLog($"  - Extracting Edge History...\n");
                                    var items = _browserHistoryService.GetHistory(edgePath, BrowserType.Edge, profile.Name);
                                    allHistory.AddRange(items);
                                    AppendLog($"    Found {items.Count} items.\n");
                                }
                            }

                            // Firefox
                            if (IsFirefoxSelected)
                            {
                                string firefoxPath = System.IO.Path.Combine(profile.Path, @"AppData\Roaming\Mozilla\Firefox\Profiles");
                                if (System.IO.Directory.Exists(firefoxPath))
                                {
                                    try
                                    {
                                        foreach (var pDir in System.IO.Directory.GetDirectories(firefoxPath))
                                        {
                                            string places = System.IO.Path.Combine(pDir, "places.sqlite");
                                            if (System.IO.File.Exists(places))
                                            {
                                                AppendLog($"  - Extracting Firefox History ({System.IO.Path.GetFileName(pDir)})...\n");
                                                var items = _browserHistoryService.GetHistory(places, BrowserType.Firefox, profile.Name);
                                                allHistory.AddRange(items);
                                                AppendLog($"    Found {items.Count} items.\n");
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (allHistory.Any())
                        {
                            // Sort descending
                            allHistory.Sort((a, b) => b.VisitTime.CompareTo(a.VisitTime));
                            
                            _browserHistoryService.ExportToCsv(allHistory, OutputPath);
                            AppendLog($"\nExported {allHistory.Count} items to {OutputPath}\n");
                        }
                        else
                        {
                            AppendLog("No history found.\n");
                        }
                        StatusMessage = "Completed";
                    }
                    else
                    {
                        StatusMessage = "Running...";
                        using var process = new Process();
                        
                        // Fix for LECmd/JLECmd crashing in windowless environments: wrap in cmd /c
                        if (_tool.Name.Equals("LECmd", System.StringComparison.OrdinalIgnoreCase) || 
                            _tool.Name.Equals("JLECmd", System.StringComparison.OrdinalIgnoreCase))
                        {
                            process.StartInfo.FileName = "cmd.exe";
                            process.StartInfo.Arguments = $"/c \"\"{_tool.ExePath}\" {args}\"";
                        }
                        else
                        {
                            process.StartInfo.FileName = _tool.ExePath;
                            process.StartInfo.Arguments = args;
                        }

                        // Special handling for SrumECmd and SumECmd (Locked Files)
                        string? vssShadowId = null;
                        string? tempStagingDir = null;
                        
                        bool isSrum = _tool.Name.Equals("SrumECmd", System.StringComparison.OrdinalIgnoreCase);
                        bool isSum = _tool.Name.Equals("SumECmd", System.StringComparison.OrdinalIgnoreCase);
                        
                        AppendLog($"[DEBUG] Tool: {_tool.Name}, isSrum: {isSrum}, isSum: {isSum}\n");

                        if (isSrum || isSum)
                        {
                            StatusMessage = "Creating Shadow Copy...";
                            AppendLog("Attempting to create Volume Shadow Copy to access locked files...\n");
                            
                            try
                            {
                                vssShadowId = _vssService.CreateSnapshot("C:\\");
                                if (!string.IsNullOrEmpty(vssShadowId))
                                {
                                    AppendLog($"Shadow Copy created successfully. ID: {vssShadowId}\n");
                                    
                                    string? volumeName = _vssService.GetSnapshotVolume(vssShadowId);
                                    if (string.IsNullOrEmpty(volumeName))
                                    {
                                         AppendLog("[ERROR] Failed to resolve Shadow Copy volume path.\n");
                                    }
                                    else
                                    {
                                        AppendLog($"Shadow Volume: {volumeName}\n");
                                        tempStagingDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GlassDFIR_Staging_" + System.Guid.NewGuid().ToString().Substring(0, 8));
                                        System.IO.Directory.CreateDirectory(tempStagingDir);

                                        if (isSrum)
                                        {
                                            StatusMessage = "Copying SRUDB...";
                                            string sruSource = $"{volumeName}\\Windows\\System32\\sru\\SRUDB.dat";
                                            string softSource = $"{volumeName}\\Windows\\System32\\config\\SOFTWARE";
                                            string sruDest = System.IO.Path.Combine(tempStagingDir, "SRUDB.dat");
                                            string softDest = System.IO.Path.Combine(tempStagingDir, "SOFTWARE");
                                            
                                            CopyFileFromShadow(sruSource, sruDest);
                                            CopyFileFromShadow(softSource, softDest);
                                            
                                             if (System.IO.File.Exists(sruDest) && !string.IsNullOrEmpty(InputPath))
                                             {
                                                  args = args.Replace(InputPath, sruDest);
                                                 if (System.IO.File.Exists(softDest))
                                                 {
                                                     args += $" -r \"{softDest}\"";
                                                 }
                                                 AppendLog($"Redirected SrumECmd to staged files in {tempStagingDir}\n");
                                            }
                                            else
                                            {
                                                AppendLog($"[WARNING] SRUDB.dat not found in shadow copy at {sruSource}\n");
                                            }
                                        }
                                        else if (isSum)
                                        {
                                            StatusMessage = "Copying Sum Logs...";
                                            string sumSource = $"{volumeName}\\Windows\\System32\\LogFiles\\Sum";
                                            string sumDest = System.IO.Path.Combine(tempStagingDir, "Sum");
                                            
                                            CopyDirectoryFromShadow(sumSource, sumDest);
                                            
                                            if (System.IO.Directory.Exists(sumDest))
                                            {
                                                 args = args.Replace(InputPath ?? string.Empty, sumDest);
                                                 AppendLog($"Redirected SumECmd to staged files in {tempStagingDir}\n");
                                            }
                                            else
                                            {
                                                AppendLog($"[WARNING] LogFiles\\Sum directory not found in shadow copy at {sumSource}\n");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    AppendLog("[WARNING] VSS Snapshot could not be created. Attempting direct access.\n");
                                }

                                // Update args in StartInfo
                                if (process.StartInfo.FileName == "cmd.exe")
                                    process.StartInfo.Arguments = $"/c \"\"{_tool.ExePath}\" {args}\"";
                                else
                                    process.StartInfo.Arguments = args;
                            }
                            catch (System.Exception ex)
                            {
                                AppendLog($"[ERROR] VSS operations failed: {ex.Message}. Attempting direct access...\n");
                            }
                        }

                        process.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(_tool.ExePath) ?? ".";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.CreateNoWindow = true;

                        if (Process.Start(process.StartInfo) is Process p)
                        {
                            p.OutputDataReceived += (s, e) => { if (e.Data != null) AppendLog(e.Data); };
                            p.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };
                            p.BeginOutputReadLine();
                            p.BeginErrorReadLine();
                            await p.WaitForExitAsync();
                        }
                        
                        // Cleanup
                        if (!string.IsNullOrEmpty(vssShadowId))
                        {
                            _vssService.DeleteSnapshot(vssShadowId);
                            AppendLog($"[INFO] Shadow Copy {vssShadowId} deleted successfully.\n");
                        }
                        if (!string.IsNullOrEmpty(tempStagingDir))
                        {
                            try 
                            { 
                                System.IO.Directory.Delete(tempStagingDir, true); 
                                AppendLog($"[INFO] Staging directory {tempStagingDir} deleted.\n");
                            } 
                            catch { }
                        }
                        
                        StatusMessage = "Completed";
                    }
                });

                if (IsAmcacheParser)
                {
                    StatusMessage = "Merging Amcache results...";
                    await MergeAmcacheResults();
                }
                else if (IsSrumECmd)
                {
                    StatusMessage = "Merging SrumECmd results...";
                    await MergeSrumResults();
                }

                AppendLog("\nExecution Completed.\n");
            }
            catch (System.Exception ex)
            {
                AppendLog($"\nExecution Failed: {ex.Message}\n");
            }
            finally
            {
                _stopwatch.Stop();
                _executionTimer.Stop();
                _logUpdateTimer.Stop();
                ProcessLogQueue(null, null);

                if (!string.IsNullOrEmpty(OutputPath))
                {
                    string folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath)) ?? ".";
                    string pattern = IsMFTECmd ? $"{System.IO.Path.GetFileNameWithoutExtension(OutputPath)}*.csv" : System.IO.Path.GetFileName(OutputPath);
                    _tool.IsCsvAvailable = System.IO.Directory.GetFiles(folder, pattern).Any();
                }

                IsRunning = false;
            }
        }


        private void RestartAsAdmin()
        {
            var path = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(path)) return;

            var startInfo = new ProcessStartInfo(path)
            {
                Verb = "runas",
                UseShellExecute = true
            };

            try
            {
                Process.Start(startInfo);
                System.Windows.Application.Current.Shutdown();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled UAC prompt
                AppendLog("Elevation cancelled by user.\n");
            }
        }

        private bool IsUserAnAdmin()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void AppendLog(string text)
        {
            // Enqueue log message instead of direct UI update
            _logQueue.Enqueue(text);
        }

        private void ProcessLogQueue(object? sender, System.EventArgs? e)
        {
            if (_logQueue.IsEmpty) return;

            var sb = new StringBuilder();
            while (_logQueue.TryDequeue(out var line))
            {
                sb.AppendLine(line);
            }

            // Update UI in a single batch
            OutputLog += sb.ToString();
        }

        private void UpdateExecutionTime(object? sender, System.EventArgs? e)
        {
            ElapsedTime = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
        }
        private void LoadUserProfiles()
        {
            UserProfiles.Clear();
            try
            {
                var usersDir = @"C:\Users";
                if (System.IO.Directory.Exists(usersDir))
                {
                    var directories = System.IO.Directory.GetDirectories(usersDir);
                    foreach (var dir in directories)
                    {
                        var dirName = System.IO.Path.GetFileName(dir);
                        // Filter out common non-user folders
                        if (dirName.Equals("All Users", System.StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("Default", System.StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("Default User", System.StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("Public", System.StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        UserProfiles.Add(new UserProfile { Name = dirName, Path = dir, IsSelected = true });
                    }
                }
            }
            catch (System.Exception ex)
            {
                AppendLog($"Error loading user profiles: {ex.Message}\n");
            }
        }

        private void LoadSystemHives()
        {
            SystemHives.Clear();
            string configDir = @"C:\Windows\System32\config";

            var hives = new[]
            {
                ("SYSTEM", System.IO.Path.Combine(configDir, "SYSTEM")),
                ("SOFTWARE", System.IO.Path.Combine(configDir, "SOFTWARE")),
                ("SAM", System.IO.Path.Combine(configDir, "SAM")),
                ("SECURITY", System.IO.Path.Combine(configDir, "SECURITY")),
                ("AMCACHE", @"C:\Windows\AppCompat\Programs\Amcache.hve")
            };

            foreach (var hive in hives)
            {
                SystemHives.Add(new RegistryHiveOption { Name = hive.Item1, Path = hive.Item2, IsSelected = true });
            }
        }


        private void CopyFileFromShadow(string source, string dest)
        {
            string sourceDir = System.IO.Path.GetDirectoryName(source) ?? "";
            string fileName = System.IO.Path.GetFileName(source) ?? "";
            string destDir = System.IO.Path.GetDirectoryName(dest) ?? "";
            
            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(destDir)) return;

             var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "robocopy",
                    Arguments = $"\"{sourceDir}\" \"{destDir}\" \"{fileName}\" /R:1 /W:1",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            proc.WaitForExit();
        }

         private void CopyDirectoryFromShadow(string source, string dest)
        {
             var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "robocopy",
                    Arguments = $"\"{source}\" \"{dest}\" /E /R:1 /W:1",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            proc.WaitForExit();
        }

        private async Task MergeAmcacheResults()
        {
            try
            {
                if (string.IsNullOrEmpty(OutputPath)) return;

                string? outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath));
                if (outputFolder == null) return;
                
                // AmcacheParser outputs files like: AmcacheParser_20230501_120000_CategoryName.csv
                // We need to find all CSVs that match this pattern in the output folder.
                var files = System.IO.Directory.GetFiles(outputFolder, "AmcacheParser_*.csv");
                
                if (files.Length == 0)
                {
                    AppendLog("[WARNING] No AmcacheParser CSVs found to merge.\n");
                    return;
                }

                AppendLog($"\n--- Merging {files.Length} Amcache CSVs ---\n");

                var allHeaders = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                var validFiles = new System.Collections.Generic.List<string>();

                // 1. First Pass: Collect all unique headers from all files
                foreach (var file in files)
                {
                    // Skip if file is empty
                    var info = new System.IO.FileInfo(file);
                    if (info.Length == 0) continue;

                    validFiles.Add(file);
                    
                    // Read first line for header
                    using var reader = new System.IO.StreamReader(file);
                    string? headerLine = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(headerLine))
                    {
                        var headers = headerLine.Split(',');
                        foreach (var h in headers) allHeaders.Add(h.Trim());
                    }
                }

                if (validFiles.Count == 0) return;

                // 2. Prepare Master Header with "SourceCategory" as first column
                var masterHeaderList = new System.Collections.Generic.List<string> { "SourceCategory" };
                masterHeaderList.AddRange(allHeaders);
                
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",", masterHeaderList));

                // 3. Second Pass: Read data, map to master headers, and write
                foreach (var file in validFiles)
                {
                    // Extract Category from filename
                    // Example: AmcacheParser_20230501_120000_DeviceContainers.csv
                    // We want "DeviceContainers"
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    string category = "Unknown";
                    
                    // Standard restartable regex to grab the suffix after the timestamp
                    // Pattern: AmcacheParser_YYYYMMDD_HHMMSS_{Category}
                    var parts = fileName.Split('_');
                    if (parts.Length >= 4)
                    {
                        category = string.Join("_", parts.Skip(3));
                    }
                    else if (parts.Length > 1) 
                    {
                        // Fallback: just use last part
                        category = parts.Last();
                    }

                    var lines = await System.IO.File.ReadAllLinesAsync(file);
                    if (lines.Length < 2) continue; // Header only or empty

                    var fileHeaders = lines[0].Split(',').Select(h => h.Trim()).ToList();
                    
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Regex to split by comma but ignore commas inside quotes
                        // This handles paths/data containing commas
                        var values = System.Text.RegularExpressions.Regex.Split(line, ",(?=(?:[^\"\"]*\"\"[^\"\"]*\"\")*[^\"\"]*$)");
                        
                        // Map values to file headers
                        var rowData = new System.Collections.Generic.Dictionary<string, string>();
                        for (int j = 0; j < Math.Min(values.Length, fileHeaders.Count); j++)
                        {
                            rowData[fileHeaders[j]] = values[j];
                        }

                        // construct new row based on master header
                        var newRow = new System.Collections.Generic.List<string>();
                        newRow.Add(category); // First column

                        foreach (var targetHeader in masterHeaderList.Skip(1))
                        {
                            if (rowData.TryGetValue(targetHeader, out var val))
                            {
                                newRow.Add(val);
                            }
                            else
                            {
                                newRow.Add(""); // Empty if column doesn't exist in this source
                            }
                        }
                        
                        sb.AppendLine(string.Join(",", newRow));
                    }
                    
                    // Delete the original partial file
                    try 
                    { 
                        System.IO.File.Delete(file); 
                    } 
                    catch { /* Best effort delete */ }
                }

                await System.IO.File.WriteAllTextAsync(OutputPath, sb.ToString());
                AppendLog($"[SUCCESS] Merged Amcache output saved to {OutputPath}\n");
            }
            catch (System.Exception ex)
            {
                AppendLog($"[ERROR] Failed to merge Amcache CSVs: {ex.Message}\n");
            }
        }

        private async Task MergeSrumResults()
        {
            try
            {
                if (string.IsNullOrEmpty(OutputPath)) return;

                string? outputFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(OutputPath));
                if (outputFolder == null) return;
                
                // Pattern: *_SrumECmd_*_Output.csv
                var files = System.IO.Directory.GetFiles(outputFolder, "*_SrumECmd_*_Output.csv");
                
                if (files.Length == 0)
                {
                    AppendLog("[WARNING] No SrumECmd CSVs found to merge.\n");
                    return;
                }

                AppendLog($"\n--- Merging {files.Length} SrumECmd CSVs ---\n");

                var allHeaders = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                var validFiles = new System.Collections.Generic.List<string>();

                foreach (var file in files)
                {
                    var info = new System.IO.FileInfo(file);
                    if (info.Length == 0) continue;
                    validFiles.Add(file);
                    
                    using var reader = new System.IO.StreamReader(file);
                    string? headerLine = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(headerLine))
                    {
                        var headers = headerLine.Split(',');
                        foreach (var h in headers) allHeaders.Add(h.Trim());
                    }
                }

                if (validFiles.Count == 0) return;

                var masterHeaderList = new System.Collections.Generic.List<string> { "SourceTable" };
                masterHeaderList.AddRange(allHeaders.OrderBy(h => h));
                
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",", masterHeaderList));

                foreach (var file in validFiles)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    string tableName = "Unknown";
                    
                    var parts = fileName.Split('_');
                    if (parts.Length >= 4)
                    {
                        tableName = string.Join("_", parts.Skip(2).Take(parts.Length - 3)); 
                    }
                    else
                    {
                        tableName = fileName;
                    }

                    var lines = await System.IO.File.ReadAllLinesAsync(file);
                    if (lines.Length < 2) continue;

                    var fileHeaders = lines[0].Split(',').Select(h => h.Trim()).ToList();
                    
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = System.Text.RegularExpressions.Regex.Split(line, ",(?=(?:[^\"\"]*\"\"[^\"\"]*\"\")*[^\"\"]*$)");
                        
                        var rowData = new System.Collections.Generic.Dictionary<string, string>();
                        for (int j = 0; j < Math.Min(values.Length, fileHeaders.Count); j++)
                        {
                            rowData[fileHeaders[j]] = values[j];
                        }

                        var newRow = new System.Collections.Generic.List<string>();
                        newRow.Add(tableName); 

                        foreach (var targetHeader in masterHeaderList.Skip(1))
                        {
                            if (rowData.TryGetValue(targetHeader, out var val))
                                newRow.Add(val);
                            else
                                newRow.Add(""); 
                        }
                        
                        sb.AppendLine(string.Join(",", newRow));
                    }
                    
                    try { System.IO.File.Delete(file); } catch { }
                }

                string savePath = OutputPath;
                if (!savePath.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
                {
                     if (System.IO.Directory.Exists(savePath))
                         savePath = System.IO.Path.Combine(savePath, "SrumECmd_Merged.csv");
                     else
                         savePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(savePath) ?? ".", "SrumECmd_Merged.csv");
                }

                await System.IO.File.WriteAllTextAsync(savePath, sb.ToString());
                AppendLog($"[SUCCESS] Merged SrumECmd output saved to {savePath}\n");
            }
            catch (System.Exception ex)
            {
                AppendLog($"[ERROR] Failed to merge SrumECmd CSVs: {ex.Message}\n");
            }
        }



        private void MergeCsvFiles(System.Collections.Generic.List<string> tempCsvFiles, string finalPath, string baseFileName, string sourceColumnName = "User")
        {
            StatusMessage = "Merging results...";
            AppendLog("\n--- Merging CSVs ---\n");

            try
            {
                using var writer = new System.IO.StreamWriter(finalPath);
                bool headerWritten = false;

                foreach (var path in tempCsvFiles)
                {
                    if (!System.IO.File.Exists(path)) continue;

                    string sourceLabel = System.IO.Path.GetFileNameWithoutExtension(path).Replace($"{baseFileName}_", "");
                    var lines = System.IO.File.ReadAllLines(path);
                    if (lines.Length == 0) continue;

                    var header = lines[0];

                    if (!headerWritten)
                    {
                        writer.WriteLine($"{sourceColumnName},{header}"); // Add Source column
                        headerWritten = true;
                    }

                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        writer.WriteLine($"{sourceLabel},{line}");
                    }

                    // Cleanup
                    try { System.IO.File.Delete(path); } catch { }
                }
                AppendLog($"[SUCCESS] Merged CSV saved to {finalPath}\n");
            }
            catch (System.Exception ex)
            {
                AppendLog($"[ERROR] Failed to merge CSVs: {ex.Message}\n");
            }
        }

        private async Task RunYaraScanInternal(string targetMemoryFile, string outputFolder)
        {
            StatusMessage = "Running YARA Scan...";
            string finalYaraRulesPath = YaraRulesPath;
            string? tempRulesFile = null;

            if (System.IO.Directory.Exists(YaraRulesPath))
            {
                AppendLog($"[INFO] {YaraRulesPath} is a directory. Generating temporary master rule file...\n");
                var yarFiles = System.IO.Directory.GetFiles(YaraRulesPath, "*.yar", System.IO.SearchOption.AllDirectories);
                if (yarFiles.Length == 0)
                {
                    AppendLog("[ERROR] No .yar files found in the specified directory.\n");
                    return;
                }

                tempRulesFile = System.IO.Path.Combine(outputFolder, $"GlassDFIR_TempRules_{System.Guid.NewGuid().ToString().Substring(0, 8)}.yar");
                var sb = new System.Text.StringBuilder();
                foreach (var file in yarFiles)
                {
                    sb.AppendLine($"include \"{file.Replace("\\", "/")}\"");
                }
                System.IO.File.WriteAllText(tempRulesFile, sb.ToString());
                finalYaraRulesPath = tempRulesFile;
                AppendLog($"[SUCCESS] Created master rule file with {yarFiles.Length} inclusions.\n");
            }

            string? symbolDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_tool.ExePath) ?? "", "symbols");
            string symbolArg = System.IO.Directory.Exists(symbolDir) ? $"-s \"{symbolDir}\"" : "";
            string volArgs = $"-f \"{targetMemoryFile}\" {symbolArg} -r csv yarascan.YaraScan --yara-file \"{finalYaraRulesPath}\"";

            AppendLog($"\n--- Running YARA Scan ---\n");
            AppendLog($"Command: {_tool.ExePath} {volArgs}\n");

            using var process = new Process();
            process.StartInfo.FileName = _tool.ExePath;
            process.StartInfo.Arguments = volArgs;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            StringBuilder csvOutput = new StringBuilder();
            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    AppendLog(e.Data);
                    csvOutput.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog($"ERROR: {e.Data}"); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            // Cleanup temp rules
            if (!string.IsNullOrEmpty(tempRulesFile) && System.IO.File.Exists(tempRulesFile))
            {
                try { System.IO.File.Delete(tempRulesFile); AppendLog("[INFO] Temporary YARA rules file deleted.\n"); } catch { }
            }

            string yaraOutputFile = $"Volatility_YaraScan_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string fullYaraOutputPath = System.IO.Path.Combine(outputFolder, yaraOutputFile);
            if (csvOutput.Length > 0)
            {
                System.IO.File.WriteAllText(fullYaraOutputPath, csvOutput.ToString());
                AppendLog($"[SUCCESS] Saved YARA Results to {yaraOutputFile}\n");
            }

            StatusMessage = "Completed";
        }
    }

    public partial class RegistryHiveOption : ObservableObject
    {
        [ObservableProperty] private string _name = "";
        [ObservableProperty] private string _path = "";
        [ObservableProperty] private bool _isSelected = true;
    }


    public partial class ToolRunnerViewModel
    {
        [RelayCommand]
        private void BrowseBatch()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "RECmd Batch Files (*.reb)|*.reb|All Files (*.*)|*.*",
                InitialDirectory = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_tool.ExePath) ?? "", "BatchExamples")
            };

            if (dialog.ShowDialog() == true)
            {
                ReCmdBatchPath = dialog.FileName;
            }
        }
    }
}
