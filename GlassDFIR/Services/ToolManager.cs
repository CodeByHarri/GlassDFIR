#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GlassDFIR.Models;

namespace GlassDFIR.Services
{
    public class ToolManager : IToolManager
    {
        private List<ToolModule> _tools = new List<ToolModule>();
        private readonly string _toolsRoot;
        private readonly string _ezRoot;
        private readonly string _hayabusaRoot;
        private readonly string _volatilityRoot;
        private readonly string _yaraRoot;

        private readonly ICaseManager _caseManager;

        public ToolManager(ICaseManager caseManager)
        {
            _caseManager = caseManager;
            _toolsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            _ezRoot = Path.Combine(_toolsRoot, "EZ");
            _hayabusaRoot = Path.Combine(_toolsRoot, "Hayabusa");
            _volatilityRoot = Path.Combine(_toolsRoot, "Volatility");
            _yaraRoot = Path.Combine(_toolsRoot, "Yara");
        }

        public IEnumerable<ToolModule> GetAvailableTools()
        {
            return _tools;
        }

        public Task ScanForToolsAsync()
        {
            return Task.Run(() =>
            {
                var scannedTools = new List<ToolModule>();

                // Ensure central output directory exists
                string globalOutputDir = _caseManager.GetGlobalOutputPath();
                if (!Directory.Exists(globalOutputDir)) Directory.CreateDirectory(globalOutputDir);

                string outputDir = _caseManager.GetCurrentOutputPath();

                // 1. Hayabusa
                var hayabusaPath = FindExeRecursively(_hayabusaRoot, "hayabusa*.exe");
                scannedTools.Add(new ToolModule
                {
                    Name = "Hayabusa",
                    Description = "Live Scanner",
                    Category = "Threat Hunting",

                    DefaultInputPath = @"C:\Windows\System32\winevt\Logs",
                    DefaultOutputPath = Path.Combine(outputDir, $"Hayabusa_{DateTime.Now:yyyyMMdd_HHmmss}.csv"),
                    ArgumentsTemplate = "csv-timeline -f \"{Input}\" -o \"{Output}\"", // Template might be overridden in Runner for specific logic
                    ExePath = hayabusaPath,
                    IsReady = !string.IsNullOrEmpty(hayabusaPath),
                    IsCsvAvailable = Directory.GetFiles(outputDir, "Hayabusa_*.csv").Any()
                });

                // 2. Zimmerman Tools
                var ezTools = new[]
                {
                    // File System & Disk
                    ("MFTECmd", "MFTECmd.exe", "NTFS Metadata ($MFT, $Boot, $J)", "File System & Disk", @"C:\$MFT", "-f \"{Input}\" --csv \"{OutputFolder}\" --csvf \"{OutputFile}\""),
                    ("RBCmd", "RBCmd.exe", "Recycle Bin ($I files)", "File System & Disk", @"C:\$Recycle.Bin", "-d \"{Input}\" --csv \"{OutputFolder}\" --csvf \"{OutputFile}\""),
                    ("LECmd", "LECmd.exe", "LNK Files (Access/Opening)", "File System & Disk", @"C:\Users\*\AppData\Roaming\Microsoft\Windows\Recent", "-d \"{Input}\" --csv \"{OutputFolder}\" --csvf \"{OutputFile}\" -q"),
                    ("JLECmd", "JLECmd.exe", "Jump Lists", "File System & Disk", @"C:\Users\*\AppData\Roaming\Microsoft\Windows\Recent\AutomaticDestinations", "-d \"{Input}\" --csv \"{OutputFolder}\" --csvf \"{OutputFile}\""),

                    // Execution & Persistence
                    ("AmcacheParser", "AmcacheParser.exe", "Amcache.hve (Execution Evidence)", "Execution & Persistence", @"C:\Windows\AppCompat\Programs\Amcache.hve", "-f \"{Input}\" --csv \"{OutputFolder}\" --csvf \"{OutputFile}\""),
                    ("AppCompatCacheParser", "AppCompatCacheParser.exe", "ShimCache (Execution Evidence)", "Execution & Persistence", @"SYSTEM", "--csv \"{OutputFolder}\" --csvf \"{OutputFile}\""),
                    ("PECmd", "PECmd.exe", "Prefetch (Run Count)", "Execution & Persistence", @"C:\Windows\Prefetch", "-d \"{Input}\" --csv \"{OutputFolder}\" --csvf \"{OutputFile}\""),


                    // System Activity & Logs
                    ("EvtxECmd", "EvtxECmd.exe", "Windows Event Logs (Backbone)", "System Activity & Logs", @"C:\Windows\System32\winevt\Logs", "-d \"{Input}\" --csv \"{OutputFolder}\" --csvf \"{OutputFile}\""),
                    ("SrumECmd", "SrumECmd.exe", "Network & Process Usage", "System Activity & Logs", @"C:\Windows\System32\sru\SRUDB.dat", "-f \"{Input}\" --csv \"{OutputFolder}\""),
                    ("SumECmd", "SumECmd.exe", "User Access Logs", "System Activity & Logs", @"C:\Windows\System32\LogFiles\Sum", "-d \"{Input}\" --csv \"{OutputFolder}\""),
                    ("WxTCmd", "WxTCmd.exe", "Windows 10 Timeline", "System Activity & Logs", @"C:\Users\*\AppData\Local\ConnectedDevicesPlatform\*", "-f \"{Input}\" --csv \"{OutputFolder}\""),

                    // Registry
                    ("RECmd", "RECmd.exe", "Registry Hives (SAM, SYSTEM, etc.)", "Registry", @"C:\Windows\System32\config", "-d \"{Input}\" --csv \"{OutputFolder}\" --csvf \"{OutputFile}\""),
                    ("SBECmd", "SBECmd.exe", "ShellBags (Folder History)", "Registry", @"C:\Users\*\AppData\Local\Microsoft\Windows\UsrClass.dat", "-d \"{Input}\" --csv \"{OutputFolder}\" --csvf \"{OutputFile}\""),

                    // Application & Web
                    // Application & Web

                    ("BrowserHistory", "", "Native Browser History (Chrome/Edge/Firefox)", "Application", @"C:\Users", "")

                };


                foreach (var tool in ezTools)
                {
                    string path = null;
                    if (!string.IsNullOrEmpty(tool.Item2))
                    {
                        // Search for the tool exe recursively in EZ root
                        path = FindExeRecursively(_ezRoot, tool.Item2);
                    }

                    bool isBrowserHistory = tool.Item1.Equals("BrowserHistory", StringComparison.OrdinalIgnoreCase);
                    bool isReady = !string.IsNullOrEmpty(path) || isBrowserHistory;

                    scannedTools.Add(new ToolModule
                    {
                        Name = tool.Item1,
                        Description = tool.Item3,
                        Category = tool.Item4,
                        DefaultInputPath = tool.Item5,
                        DefaultOutputPath = Path.Combine(outputDir, $"{tool.Item1}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"),
                        ArgumentsTemplate = tool.Item6,
                        ExePath = isBrowserHistory ? "Internal (Native)" : (path ?? string.Empty),
                        IsReady = isReady,
                        IsCsvAvailable = Directory.GetFiles(outputDir, $"{tool.Item1}_*.csv").Any()
                    });
                }



                // 3. Volatility 3
                var volPath = FindExeRecursively(_volatilityRoot, "vol.exe");
                scannedTools.Add(new ToolModule
                {
                    Name = "Volatility",
                    Description = "Memory Forensics Framework",
                    Category = "Memory Analysis",
                    DefaultInputPath = string.Empty,
                    DefaultOutputPath = Path.Combine(outputDir, "Volatility_Results"),
                    ArgumentsTemplate = "-f \"{Input}\" -r csv {Plugin}",
                    ExePath = volPath ?? string.Empty,
                    IsReady = !string.IsNullOrEmpty(volPath),
                    IsCsvAvailable = Directory.Exists(outputDir) && Directory.GetFiles(outputDir, "Volatility_*.csv").Any()
                });

                // 4. WinPmem (for live capture)
                var winpmemPath = FindExeRecursively(_volatilityRoot, "winpmem*.exe");
                scannedTools.Add(new ToolModule
                {
                    Name = "WinPmem",
                    Description = "Live Memory Capture",
                    Category = "Memory Analysis",
                    DefaultInputPath = @"\\.\pmem",
                    DefaultOutputPath = Path.Combine(outputDir, $"Memory_{DateTime.Now:yyyyMMdd_HHmmss}.raw"),
                    ArgumentsTemplate = "-o \"{Output}\"",
                    ExePath = winpmemPath ?? string.Empty,
                    IsReady = !string.IsNullOrEmpty(winpmemPath),
                    IsCsvAvailable = false
                });

                _tools = scannedTools;
            });
        }

        private string FindExeRecursively(string rootPath, string exeName)
        {
            if (!Directory.Exists(rootPath)) return null;
            try
            {
                var files = Directory.GetFiles(rootPath, exeName, SearchOption.AllDirectories);
                return files.FirstOrDefault();
            }
            catch 
            {
                return null;
            }
        }

        public bool IsToolAvailable(string toolName)
        {
            return _tools.Any(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase) && t.IsReady);
        }

        public bool IsEzToolsInstalled()
        {
            // Simple check: minimal requirement is the folder exists and contains at least one core tool (e.g. MFTECmd)
            return !string.IsNullOrEmpty(FindExeRecursively(_ezRoot, "MFTECmd.exe"));
        }

        public bool IsHayabusaInstalled()
        {
             return !string.IsNullOrEmpty(FindExeRecursively(_hayabusaRoot, "hayabusa*.exe"));
        }

        public bool IsVolatilityInstalled()
        {
            var volExists = !string.IsNullOrEmpty(FindExeRecursively(_volatilityRoot, "vol.exe"));
            var pmemExists = !string.IsNullOrEmpty(FindExeRecursively(_volatilityRoot, "winpmem*.exe"));
            return volExists && pmemExists;
        }
    }
}
