using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassDFIR.Services;

namespace GlassDFIR.ViewModels
{
    public class AsnFilter
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public partial class RapidSearchViewModel : BladeViewModel
    {
        private readonly ISearchService _searchService;
        private readonly IAsnLookupService _asnLookupService;
        private readonly MainViewModel _mainViewModel;
        
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _searchPerformed;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string? _selectedAsn;

        public ObservableCollection<RapidSearchResult> Results { get; } = new ObservableCollection<RapidSearchResult>();
        public System.ComponentModel.ICollectionView FilteredResults { get; }
        public ObservableCollection<AsnFilter> AsnFilters { get; } = new ObservableCollection<AsnFilter>();

        public RapidSearchViewModel(ISearchService searchService, IAsnLookupService asnLookupService, MainViewModel mainViewModel)
        {
            _searchService = searchService;
            _asnLookupService = asnLookupService;
            _mainViewModel = mainViewModel;
            Title = "IP Search";
            Icon = "⚡";

            FilteredResults = System.Windows.Data.CollectionViewSource.GetDefaultView(Results);
            FilteredResults.Filter = obj => 
            {
                if (obj is RapidSearchResult res)
                {
                    if (string.IsNullOrEmpty(SelectedAsn) || SelectedAsn == "All ASNs") return true;
                    return res.ASNName == SelectedAsn || $"{res.ASN} - {res.ASNName}" == SelectedAsn;
                }
                return false;
            };
        }

        partial void OnSelectedAsnChanged(string? value)
        {
            FilteredResults.Refresh();
        }

        [RelayCommand]
        private async Task RunAsnLookup()
        {
            IsBusy = true;
            SearchPerformed = false;
            StatusMessage = "Initializing ASN Database...";
            Results.Clear();
            AsnFilters.Clear();

            try
            {
                if (!_asnLookupService.IsReady)
                {
                    await _asnLookupService.InitializeAsync();
                }

                StatusMessage = "Scanning logs for IP addresses...";
                
                // IP Regex
                string ipRegex = @"\b(?:\d{1,3}\.){3}\d{1,3}\b";
                var searchResults = await _searchService.SearchAsync(ipRegex);

                StatusMessage = "Mapping IPs to ASNs...";
                var list = new List<RapidSearchResult>();
                
                foreach (var res in searchResults)
                {
                    // Extract the IP from the match context or content
                    var match = Regex.Match(res.Content, ipRegex);
                    if (match.Success)
                    {
                        string ip = match.Value;
                        
                        // Filter out common private ranges
                        if (IsPrivateIp(ip)) continue;

                        var asnInfo = _asnLookupService.GetAsnInfo(ip);
                        if (asnInfo != null)
                        {
                            list.Add(new RapidSearchResult
                            {
                                IP = ip,
                                ASN = asnInfo.Value.ASN,
                                ASNName = asnInfo.Value.Name,
                                ToolName = res.ToolName,
                                Source = res.Source,
                                Content = res.Content,
                                MatchContextBefore = res.MatchContextBefore,
                                MatchContextAfter = res.MatchContextAfter,
                                LineReference = res.LineReference
                            });
                        }
                    }
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    foreach (var item in list) Results.Add(item);
                    
                    var groups = list.GroupBy(r => r.ASNName)
                                   .Select(g => new AsnFilter { Name = g.Key, Count = g.Count() })
                                   .OrderByDescending(f => f.Count);

                    AsnFilters.Add(new AsnFilter { Name = "All ASNs", Count = list.Count });
                    foreach (var filter in groups) AsnFilters.Add(filter);
                    
                    SearchPerformed = true;
                    SelectedAsn = "All ASNs";
                    StatusMessage = $"Found {Results.Count} IP-ASN matches.";
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool IsPrivateIp(string ip)
        {
            if (ip.StartsWith("10.")) return true;
            if (ip.StartsWith("192.168.")) return true;
            if (ip.StartsWith("127.")) return true;
            if (ip.StartsWith("169.254.")) return true;
            if (ip.StartsWith("172."))
            {
                var parts = ip.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int scnd))
                {
                    return scnd >= 16 && scnd <= 31;
                }
            }
            return false;
        }

        [RelayCommand]
        private void GoToResult(RapidSearchResult result)
        {
            if (result == null) return;
            var categoryBlades = _mainViewModel.Blades.OfType<CategoryBladeViewModel>().ToList();
            TimelineViewModel? targetBlade = null;
            foreach (var category in categoryBlades)
            {
                targetBlade = category.SubBlades.OfType<TimelineViewModel>()
                    .FirstOrDefault(v => v.HeaderText.Contains(result.ToolName) || v.Title == result.ToolName);
                if (targetBlade != null) break;
            }

            if (targetBlade != null)
            {
                _mainViewModel.CurrentBlade = targetBlade;
                _ = targetBlade.LoadCsv(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlassDFIR_Output", result.Source));
            }
        }
    }
}
