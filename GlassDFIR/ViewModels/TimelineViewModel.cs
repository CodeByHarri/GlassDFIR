using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassDFIR.Services;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace GlassDFIR.ViewModels
{
    public class InterestingEntitiesResult
    {
        public List<GlassDFIR.Models.EntityCount> Ips { get; set; } = new();
        public List<GlassDFIR.Models.EntityCount> Emails { get; set; } = new();
        public List<GlassDFIR.Models.EntityCount> Domains { get; set; } = new();
        public List<GlassDFIR.Models.EntityCount> Files { get; set; } = new();
    }

    public partial class TimelineViewModel : BladeViewModel
    {
        private readonly ICsvParsingService _csvParsingService;
        private readonly ICaseManager _caseManager;

        [ObservableProperty]
        private DataTable? _results;

        [ObservableProperty]
        private string? _filterText;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _currentFilePath;

        [ObservableProperty]
        private string? _toolName;

        [ObservableProperty]
        private string? _filePattern;

        [ObservableProperty]
        private string _headerText = "Results Viewer";

        public ListCollectionView? ResultsView { get; private set; }

        private InterestingEntitiesResult? _cachedEntities;
        private Task<InterestingEntitiesResult>? _analysisTask;

        public TimelineViewModel(ICsvParsingService csvParsingService, ICaseManager caseManager)
        {
            _csvParsingService = csvParsingService;
            _caseManager = caseManager;
            Title = "CSV Viewer";
            Icon = "📄";
            IsEnabled = true; // Always enabled so we can click to load manually
        }

        partial void OnResultsChanged(DataTable? value)
        {
            _cachedEntities = null;
            _analysisTask = null;

            if (value != null)
            {
                ResultsView = new ListCollectionView(value.DefaultView);
                ResultsView.Filter = FilterData;
                
                // Pre-calculate entities in background
                _analysisTask = AnalyzeEntitiesAsync(value);
            }
            else
            {
                ResultsView = null;
            }
            OnPropertyChanged(nameof(ResultsView));
            UpdateIconColor();
        }


        private void UpdateIconColor()
        {
            if (Results != null)
            {
                IconColor = "#9ece6a"; // Success Green
            }
            else if (CsvFileFound)
            {
                IconColor = "#bb9af7"; // CsvAvailable Purple
            }
            else
            {
                IconColor = "#c0caf5"; // Default
            }
        }

        partial void OnFilterTextChanged(string? value)
        {
            ResultsView?.Refresh();
        }

        private bool FilterData(object obj)
        {
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            if (obj is DataRowView row)
            {
                foreach (var item in row.Row.ItemArray)
                {
                    if (item != null && item.ToString()?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
            }
            return false;
        }

        [RelayCommand]
        public async Task LoadCsv(string filePath)
        {
            await LoadCsvInternal(filePath, false);
        }

        private async Task LoadCsvInternal(string filePath, bool silent)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            IsLoading = true;
            CurrentFilePath = filePath;
            
            try
            {
                var data = await _csvParsingService.ParseCsvToDataTableAsync(filePath);
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    Results = data;
                });
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    System.Windows.MessageBox.Show($"Failed to load CSV: {ex.Message}", "Loading Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void BrowseCsv()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = $"Select {HeaderText} CSV"
            };

            if (dialog.ShowDialog() == true)
            {
                _ = LoadCsv(dialog.FileName);
            }
        }

        [ObservableProperty]
        private bool _csvFileFound;

        partial void OnCsvFileFoundChanged(bool value) => UpdateIconColor();

        public void InitializeFromTool(string toolName, string filePattern)
        {
            ToolName = toolName;
            FilePattern = filePattern;

            string outputDir = _caseManager.GetCurrentOutputPath();
            if (Directory.Exists(outputDir))
            {
                var files = Directory.GetFiles(outputDir, filePattern);
                CsvFileFound = files.Any();
                
                // If we don't have results yet OR a newer file is available, auto-load the latest one
                // AND we aren't already loading
                if (CsvFileFound && !IsLoading)
                {
                    var latest = files.OrderByDescending(f => f).First();
                    if (Results == null || latest != CurrentFilePath)
                    {
                        _ = LoadCsvInternal(latest, true);
                    }
                }
            }
            else
            {
                CsvFileFound = false;
            }
        }

        [ObservableProperty]
        private string _loadingMessage = "Parsing CSV...";

        [RelayCommand]
        private async Task ShowInterestingEntities()
        {
            if (Results == null) return;

            InterestingEntitiesResult? results = _cachedEntities;

            if (results == null)
            {
                LoadingMessage = "Analyzing Interesting Entities (This may take a moment)...";
                IsLoading = true;

                try
                {
                    // If a background task is already running, wait for it
                    if (_analysisTask != null)
                    {
                        results = await _analysisTask;
                    }
                    else
                    {
                        results = await AnalyzeEntitiesAsync(Results);
                    }
                    _cachedEntities = results;
                }
                finally
                {
                    IsLoading = false;
                    LoadingMessage = "Parsing CSV...";
                }
            }

            if (results != null)
            {
                var win = new Views.InterestingEntitiesWindow(results.Ips, results.Emails, results.Domains, results.Files);
                win.Owner = System.Windows.Application.Current.MainWindow;
                win.ShowDialog();
            }
        }

        private async Task<InterestingEntitiesResult> AnalyzeEntitiesAsync(DataTable data)
        {
            return await Task.Run(() => 
            {
                var ipCounts = new Dictionary<string, int>();
                var emailCounts = new Dictionary<string, int>();
                var domainCounts = new Dictionary<string, int>();
                var fileCounts = new Dictionary<string, int>();

                var ipRegex = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", RegexOptions.Compiled);
                var emailRegex = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled);
                var domainRegex = new Regex(@"\b((?!-)[A-Za-z0-9-]{1,63}(?<!-)\.)+[A-Za-z]{2,6}\b", RegexOptions.Compiled);
                
                var suspiciousExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                { 
                    ".exe", ".dll", ".bat", ".ps1", ".vbs", ".js", ".sys", ".scr", ".cmd", ".sh", ".py" 
                };

                var validTlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "com", "org", "net", "edu", "gov", "mil", "int", "io", "co", "uk", "ca", "de", "jp", "fr", "au", "us", "ru", "ch", "it", "nl", "se", "no", "es", "br", "gr", "cn", "tw", "kr", "in", "eu", "info", "biz", "xyz", "cloud", "online", "site", "top", "club", "vip", "win", "shop", "work", "support", "tech", "pro", "me", "tv", "cc", "ai", "ly"
                };

                foreach (DataRow row in data.Rows)
                {
                    foreach (var item in row.ItemArray)
                    {
                        if (item == null) continue;
                        string text = item.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        // IP Addresses
                        foreach (Match match in ipRegex.Matches(text))
                        {
                            string ip = match.Value;
                            ipCounts[ip] = ipCounts.GetValueOrDefault(ip) + 1;
                        }

                        // Emails
                        foreach (Match match in emailRegex.Matches(text))
                        {
                            string email = match.Value;
                            emailCounts[email] = emailCounts.GetValueOrDefault(email) + 1;
                        }

                        // Domains
                        foreach (Match match in domainRegex.Matches(text))
                        {
                            string domain = match.Value;
                            if (char.IsDigit(domain.Last())) continue; 

                            int lastDot = domain.LastIndexOf('.');
                            if (lastDot == -1) continue;
                            string tld = domain.Substring(lastDot + 1);
                            if (!validTlds.Contains(tld)) continue;

                            domainCounts[domain] = domainCounts.GetValueOrDefault(domain) + 1;
                        }

                        // Files
                        var tokens = text.Split(new[] { ' ', '\t', '\n', '\r', '"', '\'', '<', '>', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var token in tokens)
                        {
                            try 
                            {
                                string ext = Path.GetExtension(token);
                                if (suspiciousExtensions.Contains(ext))
                                {
                                    fileCounts[token] = fileCounts.GetValueOrDefault(token) + 1;
                                }
                            }
                            catch { }
                        }
                    }
                }

                return new InterestingEntitiesResult
                {
                    Ips = ipCounts.Select(kv => new GlassDFIR.Models.EntityCount { Value = kv.Key, Count = kv.Value }).OrderByDescending(x => x.Count).ToList(),
                    Emails = emailCounts.Select(kv => new GlassDFIR.Models.EntityCount { Value = kv.Key, Count = kv.Value }).OrderByDescending(x => x.Count).ToList(),
                    Domains = domainCounts.Select(kv => new GlassDFIR.Models.EntityCount { Value = kv.Key, Count = kv.Value }).OrderByDescending(x => x.Count).ToList(),
                    Files = fileCounts.Select(kv => new GlassDFIR.Models.EntityCount { Value = kv.Key, Count = kv.Value }).OrderByDescending(x => x.Count).ToList()
                };
            });
        }
    }
}
