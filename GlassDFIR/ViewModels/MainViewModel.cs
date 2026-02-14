using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassDFIR.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;

namespace GlassDFIR.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IToolManager _toolManager;
        private readonly IDownloaderService _downloaderService;
        private readonly ICsvParsingService _csvParsingService;
        private readonly ICaseManager _caseManager;
        private readonly IVssService _vssService;
        private readonly IEvidenceCollectionService _evidenceCollectionService;
        private readonly IBrowserHistoryService _browserHistoryService;

        [ObservableProperty]
        private ObservableCollection<BladeViewModel> _blades = new();

        [ObservableProperty]
        private BladeViewModel? _currentBlade;

        [ObservableProperty]
        private string? _activeCaseDisplay;

        partial void OnCurrentBladeChanged(BladeViewModel? value)
        {
            foreach (var blade in Blades)
            {
                blade.IsActive = (blade == value);
                if (blade is CategoryBladeViewModel category)
                {
                    foreach (var sub in category.SubBlades)
                    {
                        // A sub-blade is "Active" if the current blade is a ToolRunner for its tool
                        if (value is ToolRunnerViewModel runner && sub is ToolBladeViewModel toolSub)
                        {
                            sub.IsActive = (toolSub.Tool.Name == runner.Title); // Runner title is tool name
                        }
                        else
                        {
                            sub.IsActive = false;
                        }
                    }
                }
            }
        }


        public MainViewModel(IToolManager toolManager, IDownloaderService downloaderService, ICsvParsingService csvParsingService, ISearchService searchService, IAsnLookupService asnLookupService, ICaseManager caseManager, IVssService vssService, IEvidenceCollectionService evidenceCollectionService, IBrowserHistoryService browserHistoryService)
        {
            _toolManager = toolManager;
            _downloaderService = downloaderService;
            _csvParsingService = csvParsingService;
            _caseManager = caseManager;
            _vssService = vssService;
            _evidenceCollectionService = evidenceCollectionService;
            _browserHistoryService = browserHistoryService;

            _caseManager.CaseChanged += () => {
                ActiveCaseDisplay = _caseManager.CurrentCase?.ActiveDisplay;
            };

            // Load existing cases on startup
            _ = _caseManager.InitializeAsync();
            
            var searchViewModel = new SearchViewModel(searchService, this, _csvParsingService) { Title = "Global Search" };
            var rapidSearchViewModel = new RapidSearchViewModel(searchService, asnLookupService, this) { Title = "IP Search" };
            var queryHistoryViewModel = new QueryHistoryViewModel(_csvParsingService, _caseManager);

            var searchCategory = new CategoryBladeViewModel { Title = "Master Search", Icon = "🔍" };
            searchCategory.SubBlades.Add(searchViewModel);
            searchCategory.SubBlades.Add(rapidSearchViewModel);
            searchCategory.SubBlades.Add(queryHistoryViewModel);

            var dashboard = new DashboardViewModel(_toolManager, this, _csvParsingService, _downloaderService, _caseManager, _vssService, _browserHistoryService);
            var caseMgmt = new CaseManagementViewModel(_caseManager, _toolManager, this, _csvParsingService, _evidenceCollectionService, _vssService, _browserHistoryService);

            var dashCategory = new CategoryBladeViewModel { Title = "Dashboard", Icon = "🏠" };
            dashCategory.SubBlades.Add(dashboard);
            dashCategory.SubBlades.Add(caseMgmt);

            Blades.Add(dashCategory);
            Blades.Add(searchCategory);
            
            // Initialize Categories
            var huntCategory = new CategoryBladeViewModel { Title = "Threat Hunting", Icon = "🦅" };
            var fsCategory = new CategoryBladeViewModel { Title = "File System & Disk", Icon = "💾" };
            var execCategory = new CategoryBladeViewModel { Title = "Execution & Persistence", Icon = "⚡" };
            var logCategory = new CategoryBladeViewModel { Title = "System Activity & Logs", Icon = "📊" };
            var regCategory = new CategoryBladeViewModel { Title = "Registry", Icon = "🧱" };
            var memCategory = new CategoryBladeViewModel { Title = "Memory Analysis", Icon = "🧠" };


            Blades.Add(huntCategory);
            Blades.Add(fsCategory);
            Blades.Add(execCategory);
            Blades.Add(logCategory);
            Blades.Add(regCategory);

            Blades.Add(memCategory);

            // Initialize Application Category
            var appCategory = new CategoryBladeViewModel { Title = "Application", Icon = "📱" };
            Blades.Add(appCategory);

            // TimelineBlade is no longer added to main collection
            
            CurrentBlade = dashboard;


            // Populate categories after tools are scanned (initial population)
            PopulateCategories();


            // Start monitoring for Hayabusa output
            StartMonitoring();
        }

        private async void PopulateCategories()
        {
            // Wait a bit for the first scan to complete if it's already running, 
            // or trigger one if needed. Dashboard usually starts it.
            await Task.Delay(1000); 
            
            var tools = _toolManager.GetAvailableTools().ToList();
            if (!tools.Any())
            {
                await _toolManager.ScanForToolsAsync();
                tools = _toolManager.GetAvailableTools().ToList();
            }

            foreach (var category in Blades.OfType<CategoryBladeViewModel>())
            {
                // Skip Threat Hunting category for automatic tool population
                // as the user wants execution managed from the Dashboard.
                if (category.Title == "Threat Hunting")
                {
                    var timelineVm = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                    timelineVm.HeaderText = "Hunt with Hayabusa";
                    timelineVm.Title = "Hunt with Hayabusa";
                    timelineVm.Icon = "🦅";
                    timelineVm.InitializeFromTool("Hayabusa", "Hayabusa_*.csv");
                    category.SubBlades.Add(timelineVm);
                    continue;
                }

                var categoryTools = tools.Where(t => t.Category == category.Title);
                foreach (var tool in categoryTools)
                {
                    if (tool.Name == "MFTECmd")
                    {
                        var mftViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        mftViewer.HeaderText = "Master File Table ($MFT) (MFTECmd)";
                        mftViewer.Title = "Master File Table ($MFT) (MFTECmd)";
                        mftViewer.Icon = "🧱";
                        mftViewer.InitializeFromTool("MFTECmd", "MFTECmd_*_MFT.csv");
                        category.SubBlades.Add(mftViewer);

                        var jViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        jViewer.HeaderText = "UsnJournal ($J) (MFTECmd)";
                        jViewer.Title = "UsnJournal ($J) (MFTECmd)";
                        jViewer.Icon = "📝";
                        jViewer.InitializeFromTool("MFTECmd", "MFTECmd_*_J.csv");
                        category.SubBlades.Add(jViewer);

                        var sdsViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        sdsViewer.HeaderText = "Security Descriptors ($SDS) (MFTECmd)";
                        sdsViewer.Title = "Security Descriptors ($SDS) (MFTECmd)";
                        sdsViewer.Icon = "🔒";
                        sdsViewer.InitializeFromTool("MFTECmd", "MFTECmd_*_SDS.csv");
                        category.SubBlades.Add(sdsViewer);
                    }
                    else if (tool.Name == "RBCmd")
                    {
                        var rbViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        rbViewer.HeaderText = "Recycle Bin ($I) (RBCmd)";
                        rbViewer.Title = "Recycle Bin ($I) (RBCmd)";
                        rbViewer.Icon = "♻️";
                        rbViewer.InitializeFromTool("RBCmd", "RBCmd_*.csv");
                        category.SubBlades.Add(rbViewer);
                    }
                    else if (tool.Name == "LECmd")
                    {
                        var lnkViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        lnkViewer.HeaderText = "LNK Files (LECmd)";
                        lnkViewer.Title = "LNK Files (LECmd)";
                        lnkViewer.Icon = "🔗";
                        lnkViewer.InitializeFromTool("LECmd", "LECmd_*.csv");
                        category.SubBlades.Add(lnkViewer);
                    }
                    else if (tool.Name == "JLECmd")
                    {
                        var jlViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        jlViewer.HeaderText = "Jump Lists (JLECmd)";
                        jlViewer.Title = "Jump Lists (JLECmd)";
                        jlViewer.Icon = "📑";
                        jlViewer.InitializeFromTool("JLECmd", "JLECmd_*.csv");
                        category.SubBlades.Add(jlViewer);
                    }
                    else if (tool.Name == "AmcacheParser")
                    {
                        var amcacheViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        amcacheViewer.HeaderText = "Amcache Execution (AmcacheParser)";
                        amcacheViewer.Title = "Amcache Execution (AmcacheParser)";
                        amcacheViewer.Icon = "⚡";
                        amcacheViewer.InitializeFromTool("AmcacheParser", "AmcacheParser_*.csv");
                        category.SubBlades.Add(amcacheViewer);
                    }
                    else if (tool.Name == "AppCompatCacheParser")
                    {
                        var shimViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        shimViewer.HeaderText = "ShimCache (AppCompatCacheParser)";
                        shimViewer.Title = "ShimCache (AppCompatCacheParser)";
                        shimViewer.Icon = "🧩";
                        shimViewer.InitializeFromTool("AppCompatCacheParser", "AppCompatCacheParser_*.csv");
                        category.SubBlades.Add(shimViewer);
                    }
                    else if (tool.Name == "PECmd")
                    {
                        var prefetchViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        prefetchViewer.HeaderText = "Prefetch (PECmd)";
                        prefetchViewer.Title = "Prefetch (PECmd)";
                        prefetchViewer.Icon = "🚀";
                        prefetchViewer.InitializeFromTool("PECmd", "PECmd_*.csv");
                        category.SubBlades.Add(prefetchViewer);
                    }
                    else if (tool.Name == "EvtxECmd")
                    {
                        var evtxViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        evtxViewer.HeaderText = "Windows Event Logs (EvtxECmd)";
                        evtxViewer.Title = "Windows Event Logs (EvtxECmd)";
                        evtxViewer.Icon = "📋";
                        evtxViewer.InitializeFromTool("EvtxECmd", "EvtxECmd_*.csv");
                        category.SubBlades.Add(evtxViewer);
                    }
                    else if (tool.Name == "SrumECmd")
                    {
                        var srumViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        srumViewer.HeaderText = "Network & App Usage (SrumECmd)";
                        srumViewer.Title = "Network & App Usage (SrumECmd)";
                        srumViewer.Icon = "📡";
                        srumViewer.InitializeFromTool("SrumECmd", "SrumECmd_*.csv");
                        category.SubBlades.Add(srumViewer);
                    }
                    else if (tool.Name == "SumECmd")
                    {
                        var sumViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        sumViewer.HeaderText = "User Access Logs (SumECmd)";
                        sumViewer.Title = "User Access Logs (SumECmd)";
                        sumViewer.Icon = "👤";
                        sumViewer.InitializeFromTool("SumECmd", "SumECmd_*.csv");
                        category.SubBlades.Add(sumViewer);
                    }
                    else if (tool.Name == "WxTCmd")
                    {
                        var wxtViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        wxtViewer.HeaderText = "Windows Timeline (WxTCmd)";
                        wxtViewer.Title = "Windows Timeline (WxTCmd)";
                        wxtViewer.Icon = "📅";
                        wxtViewer.InitializeFromTool("WxTCmd", "WxTCmd_*.csv");
                        category.SubBlades.Add(wxtViewer);
                    }
                    else if (tool.Name == "SBECmd")
                    {
                        var sbeViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        sbeViewer.HeaderText = "ShellBags (SBECmd)";
                        sbeViewer.Title = "ShellBags (SBECmd)";
                        sbeViewer.Icon = "📂";
                        sbeViewer.InitializeFromTool("SBECmd", "SBECmd_*.csv");
                        category.SubBlades.Add(sbeViewer);
                    }
                    else if (tool.Name == "RECmd")
                    {
                        var reViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        reViewer.HeaderText = "Registry Hives (RECmd)";
                        reViewer.Title = "Registry Hives (RECmd)";
                        reViewer.Icon = "🧱";
                        reViewer.InitializeFromTool("RECmd", "RECmd_*.csv");
                        category.SubBlades.Add(reViewer);
                    }
                    else if (tool.Name == "Volatility")
                    {
                        var plugins = new[]
                        {
                            ("Process List (pslist)", "pslist", "📋"),
                            ("Process Tree (pstree)", "pstree", "🌳"),
                            ("Process Scan (psscan)", "psscan", "🔍"),
                            ("Command Line (cmdline)", "cmdline", "💻"),
                            ("DLL List (dlllist)", "dlllist", "📦"),
                            ("Handles (handles)", "handles", "🔑"),
                            ("Network Scan (netscan)", "netscan", "🌐"),
                            ("Malware Find (malfind)", "malfind", "🦠"),
                            ("Service Scan (svcscan)", "svcscan", "⚙️"),
                            ("Registry Hivelist (hivelist)", "hivelist", "🧱"),
                            ("UserAssist (userassist)", "userassist", "👤"),
                            ("Amcache (amcache)", "amcache", "⚡")
                        };

                        foreach (var (pName, pCode, pIcon) in plugins)
                        {
                            var viewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                            viewer.HeaderText = $"{pName} (Volatility)";
                            viewer.Title = pName;
                            viewer.Icon = pIcon;
                            viewer.InitializeFromTool($"Volatility - {pCode}", $"Volatility_{pCode}_*.csv");
                            category.SubBlades.Add(viewer);
                        }
                    }

                    else if (tool.Name == "BrowserHistory")
                    {
                        var webViewer = ((App)System.Windows.Application.Current).Services.GetRequiredService<TimelineViewModel>();
                        webViewer.HeaderText = "Web History (All Browsers)";
                        webViewer.Title = "Web History";
                        webViewer.Icon = "🌐";
                        webViewer.InitializeFromTool("BrowserHistory", "BrowserHistory_*.csv");
                        category.SubBlades.Add(webViewer);
                    }
                    else
                    {
                        category.SubBlades.Add(new ToolBladeViewModel(tool));
                    }
                }
            }
        }

        private async void StartMonitoring()
        {
            while (true)
            {
                // Find all timeline/viewer blades in the sidebar and check for new output
                var viewers = Blades.OfType<CategoryBladeViewModel>()
                                     .SelectMany(c => c.SubBlades)
                                     .OfType<TimelineViewModel>()
                                     .ToList();

                foreach (var viewer in viewers)
                {
                    if (!string.IsNullOrEmpty(viewer.ToolName) && !string.IsNullOrEmpty(viewer.FilePattern))
                    {
                        viewer.InitializeFromTool(viewer.ToolName, viewer.FilePattern);
                    }
                }

                await Task.Delay(5000); // Check every 5 seconds
            }
        }

        [RelayCommand]
        private void Navigate(BladeViewModel blade)
        {
            if (blade is CategoryBladeViewModel category)
            {
                category.IsExpanded = !category.IsExpanded;
                return;
            }

            if (blade is ToolBladeViewModel toolBlade)
            {
                OpenBlade(new ToolRunnerViewModel(toolBlade.Tool, _csvParsingService, this, _toolManager, _caseManager, _vssService, _browserHistoryService));
                return;
            }

            CurrentBlade = blade;
        }


        [RelayCommand]
        public void CloseBlade(BladeViewModel blade)
        {
            if (blade == null || blade is DashboardViewModel || blade is SearchViewModel) return;

            // If we are closing the currently executing blade, go back to dashboard FIRST
            if (CurrentBlade == blade)
            {
                CurrentBlade = Blades[0]; // Back to Dashboard
            }

            if (Blades.Contains(blade))
            {
                Blades.Remove(blade);
            }
        }

        public void OpenBlade(BladeViewModel blade)
        {
            if (!Blades.Contains(blade))
            {
                Blades.Add(blade);
            }
            CurrentBlade = blade;
        }
    }
}
