using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassDFIR.Models;
using GlassDFIR.Services;

using System.Security.Principal;

namespace GlassDFIR.ViewModels
{
    public partial class CaseManagementViewModel : BladeViewModel
    {
        private readonly ICaseManager _caseManager;
        private readonly IToolManager _toolManager;
        private readonly MainViewModel _mainViewModel;
        private readonly ICsvParsingService _csvParsingService;
        private readonly IEvidenceCollectionService _evidenceCollectionService;
        private readonly IVssService _vssService;
        private readonly IBrowserHistoryService _browserHistoryService;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isAdmin;

        [ObservableProperty]
        private CaseModel? _activeCase;

        public ObservableCollection<CaseModel> Cases { get; } = new ObservableCollection<CaseModel>();
        public ObservableCollection<FileInfo> EvidenceFiles { get; } = new ObservableCollection<FileInfo>();
        public ObservableCollection<FileInfo> GeneratedCsvs { get; } = new ObservableCollection<FileInfo>();
        public ObservableCollection<RecommendedTool> RecommendedTools { get; } = new ObservableCollection<RecommendedTool>();
        public ObservableCollection<CollectionArtifact> AvailableArtifacts { get; } = new ObservableCollection<CollectionArtifact>();
        public ObservableCollection<UserProfile> AvailableProfiles { get; } = new ObservableCollection<UserProfile>();
        public ObservableCollection<EvidenceNode> EvidenceTree { get; } = new ObservableCollection<EvidenceNode>();

        [ObservableProperty]
        private bool _showCollectionOverlay;

        [ObservableProperty]
        private string _collectionStatus = string.Empty;

        [ObservableProperty]
        private double _collectionProgress;

        public CaseManagementViewModel(ICaseManager caseManager, IToolManager toolManager, MainViewModel mainViewModel, ICsvParsingService csvParsingService, IEvidenceCollectionService evidenceCollectionService, IVssService vssService, IBrowserHistoryService browserHistoryService)
        {
            _caseManager = caseManager;
            _toolManager = toolManager;
            _mainViewModel = mainViewModel;
            _csvParsingService = csvParsingService;
            _evidenceCollectionService = evidenceCollectionService;
            _vssService = vssService;
            _browserHistoryService = browserHistoryService;
            Title = "Case Management";
            Icon = "💼";

            // Check Admin Status
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    IsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                IsAdmin = false;
            }

            _caseManager.CaseChanged += () => {
                LoadCases();
                ActiveCase = _caseManager.CurrentCase;
                if (ActiveCase != null)
                {
                    ScanEvidence();
                    ScanGeneratedCsvs();
                    _ = RefreshArtifacts();
                }
            };

            LoadProfiles();
            _ = RefreshArtifacts();

            LoadCases();
            ActiveCase = _caseManager.CurrentCase;
            
            if (ActiveCase != null)
            {
                ScanEvidence();
            }
        }

        private void LoadCases()
        {
            Cases.Clear();
            foreach (var c in _caseManager.GetCases())
            {
                Cases.Add(c);
            }
        }

        [RelayCommand]
        public async Task CreateNewCase()
        {
            IsBusy = true;
            var newCase = await _caseManager.CreateCaseAsync();
            await _caseManager.ActivateCaseAsync(newCase);
            ActiveCase = newCase;
            LoadCases();
            ScanEvidence();
            ScanGeneratedCsvs();
            IsBusy = false;
        }

        [RelayCommand]
        public async Task ActivateCase(CaseModel? @case)
        {
            if (@case == null) return;
            await _caseManager.ActivateCaseAsync(@case);
            ActiveCase = @case;
            LoadCases();
            ScanEvidence();
            ScanGeneratedCsvs();
        }

        [RelayCommand]
        public async Task CloseCase()
        {
            await _caseManager.ActivateCaseAsync(null);
            ActiveCase = null;
            LoadCases();
        }

        [RelayCommand]
        public async Task DeleteCase(CaseModel? @case)
        {
            if (@case == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete case '{@case.Name}'?\n\nThis will permanently delete ALL data in:\n{@case.CasePath}",
                "Confirm Deletion",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                IsBusy = true;
                await _caseManager.DeleteCaseAsync(@case);
                if (ActiveCase == @case) ActiveCase = null;
                LoadCases();
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void ScanEvidence()
        {
            if (ActiveCase == null) return;

            EvidenceFiles.Clear();
            EvidenceTree.Clear();
            RecommendedTools.Clear();

            if (Directory.Exists(ActiveCase.EvidencePath))
            {
                // Build Tree
                var rootDir = new DirectoryInfo(ActiveCase.EvidencePath);
                foreach (var dir in rootDir.GetDirectories())
                {
                    var categoryNode = new EvidenceNode 
                    { 
                        Name = dir.Name, 
                        FullPath = dir.FullName, 
                        IsDirectory = true,
                        Size = GetDirSize(dir)
                    };
                    
                    BuildTreeRecursive(dir, categoryNode);
                    EvidenceTree.Add(categoryNode);
                }

                // Old Flat List (keep for compatibility if needed, else remove)
                foreach (var file in rootDir.GetFiles("*", SearchOption.AllDirectories))
                {
                    if (file.Name == "collection_log.txt") continue;
                    EvidenceFiles.Add(file);
                    RecommendFor(file);
                }
            }
        }

        private void BuildTreeRecursive(DirectoryInfo dir, EvidenceNode parentNode)
        {
            foreach (var subDir in dir.GetDirectories())
            {
                var node = new EvidenceNode
                {
                    Name = subDir.Name,
                    FullPath = subDir.FullName,
                    IsDirectory = true,
                    Size = GetDirSize(subDir)
                };
                BuildTreeRecursive(subDir, node);
                parentNode.Children.Add(node);
            }

            foreach (var file in dir.GetFiles())
            {
                parentNode.Children.Add(new EvidenceNode
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    Size = file.Length
                });
            }
        }

        private long GetDirSize(DirectoryInfo dir)
        {
            try
            {
                return dir.GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
            catch { return 0; }
        }

        [RelayCommand]
        public void ScanGeneratedCsvs()
        {
            if (ActiveCase == null) return;

            GeneratedCsvs.Clear();
            if (Directory.Exists(ActiveCase.OutputPath))
            {
                var files = Directory.GetFiles(ActiveCase.OutputPath, "*.csv", SearchOption.AllDirectories)
                                    .Select(f => new FileInfo(f));

                foreach (var file in files)
                {
                    GeneratedCsvs.Add(file);
                }
            }
        }

        private void RecommendFor(FileInfo file)
        {
            var ext = file.Extension.ToLower();
            var name = file.Name.ToUpper();

            if (name.Contains("$MFT")) AddRecommendation("MFTECmd", "Master File Table Analysis");
            if (name.Contains("$J")) AddRecommendation("MFTECmd", "USN Journal Analysis");
            if (name.Contains("$LOGFILE")) AddRecommendation("MFTECmd", "LogFile Analysis");
            if (ext == ".mem" || ext == ".raw" || ext == ".dmp") AddRecommendation("Volatility", "Memory Forensics");
            if (ext == ".evtx") AddRecommendation("EvtxECmd", "Event Log Parsing");
            if (ext == ".lnk") AddRecommendation("LECmd", "LNK File Parsing");
            if (name.Contains("SYSTEM") || name.Contains("SOFTWARE") || name.Contains("SAM") || name.Contains("SECURITY")) 
                AddRecommendation("RECmd", "Registry Hive Analysis");
        }

        private void AddRecommendation(string toolName, string reason)
        {
            if (RecommendedTools.Any(r => r.ToolName == toolName)) return;

            var tool = _toolManager.GetAvailableTools().FirstOrDefault(t => t.Name == toolName);
            if (tool != null)
            {
                RecommendedTools.Add(new RecommendedTool 
                { 
                    ToolName = toolName, 
                    Reason = reason, 
                    Tool = tool 
                });
            }
        }

        [RelayCommand]
        private void RunTool(ToolModule? tool)
        {
            if (tool == null) return;
            _mainViewModel.OpenBlade(new ToolRunnerViewModel(tool, _csvParsingService, _mainViewModel, _toolManager, _caseManager, _vssService, _browserHistoryService));
        }

        [RelayCommand]
        private void OpenCaseFolder()
        {
            if (ActiveCase != null && Directory.Exists(ActiveCase.CasePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", ActiveCase.CasePath);
            }
        }

        [RelayCommand]
        private void OpenEvidenceFolder()
        {
            if (ActiveCase != null && Directory.Exists(ActiveCase.EvidencePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", ActiveCase.EvidencePath);
            }
        }

        [RelayCommand]
        private void ShowCollection()
        {
            if (AvailableProfiles.Count == 0) LoadProfiles();
            _ = RefreshArtifacts();
            ShowCollectionOverlay = true;
        }

        [RelayCommand]
        public async Task RefreshArtifacts()
        {
            AvailableArtifacts.Clear();
            var artifacts = _evidenceCollectionService.GetAvailableArtifacts(AvailableProfiles);
            foreach (var art in artifacts)
            {
                AvailableArtifacts.Add(art);
            }
            
            await UpdateArtifactStatuses();
        }

        private void LoadProfiles()
        {
            AvailableProfiles.Clear();
            foreach (var p in _evidenceCollectionService.GetAvailableProfiles())
            {
                p.IsSelected = true; // Default to all selected
                AvailableProfiles.Add(p);
            }
        }

        private async Task UpdateArtifactStatuses()
        {
            await Task.Run(() =>
            {
                foreach (var art in AvailableArtifacts)
                {
                    try
                    {
                        // Check if already collected
                        if (ActiveCase != null)
                        {
                             string collectedPath;
                             if (art.IsDirectory)
                                 collectedPath = Path.Combine(ActiveCase.EvidencePath, art.Category.Replace(" ", "_"), art.Name.Replace(" ", "_"));
                             else
                                 // Single files now also live in their own subfolder to prevent collisions
                                 collectedPath = Path.Combine(ActiveCase.EvidencePath, art.Category.Replace(" ", "_"), art.Name.Replace(" ", "_"), Path.GetFileName(art.SourcePath));

                             if (Directory.Exists(collectedPath) || File.Exists(collectedPath))
                             {
                                 art.Status = ArtifactStatus.Collected;
                                 art.IsSelected = false; // Auto-deselect if already collected
                                 continue;
                             }
                        }

                        // Check existence and size on host
                        if (art.IsDirectory)
                        {
                            if (Directory.Exists(art.SourcePath))
                            {
                                art.EstimatedSize = Directory.GetFiles(art.SourcePath, "*", SearchOption.TopDirectoryOnly).Sum(f => new FileInfo(f).Length);
                                art.Status = ArtifactStatus.Ready;
                                art.IsSelected = true; // Auto-select available items
                            }
                            else
                            {
                                art.Status = ArtifactStatus.NotFound;
                                art.IsSelected = false; // Auto-deselect missing items
                            }
                        }
                        else
                        {
                            if (File.Exists(art.SourcePath))
                            {
                                art.EstimatedSize = new FileInfo(art.SourcePath).Length;
                                art.Status = ArtifactStatus.Ready;
                                art.IsSelected = true; // Auto-select available items
                            }
                            else
                            {
                                art.Status = ArtifactStatus.NotFound;
                                art.IsSelected = false; // Auto-deselect missing items
                            }
                        }
                    }
                    catch 
                    {
                        art.Status = ArtifactStatus.Ready; // Fallback
                        art.IsSelected = true;
                    }
                }
            });
        }

        [RelayCommand]
        private void CancelCollection()
        {
            ShowCollectionOverlay = false;
        }

        [RelayCommand]
        private void RestartAsAdmin()
        {
            var exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exeName != null)
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo(exeName)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                try
                {
                    System.Diagnostics.Process.Start(startInfo);
                    System.Windows.Application.Current.Shutdown();
                }
                catch
                {
                    // User cancelled UAC
                }
            }
        }

        [RelayCommand]
        private async Task ExecuteCollection()
        {
            if (ActiveCase == null) return;

            IsBusy = true;
            CollectionStatus = "Starting collection...";
            CollectionProgress = 0;

            try
            {
                await _evidenceCollectionService.CollectArtifactsAsync(
                    AvailableArtifacts, 
                    ActiveCase.EvidencePath, 
                    (log) => CollectionStatus = log,
                    (prog) => CollectionProgress = prog
                );

                ScanEvidence();
                ShowCollectionOverlay = false;
            }
            catch (Exception ex)
            {
                CollectionStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class RecommendedTool
    {
        public string ToolName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public ToolModule? Tool { get; set; }
    }
}
