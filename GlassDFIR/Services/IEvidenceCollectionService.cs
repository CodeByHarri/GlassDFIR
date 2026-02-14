using CommunityToolkit.Mvvm.ComponentModel;
using GlassDFIR.Models;

namespace GlassDFIR.Services
{
    public interface IEvidenceCollectionService
    {
        IEnumerable<UserProfile> GetAvailableProfiles();
        IEnumerable<CollectionArtifact> GetAvailableArtifacts(IEnumerable<UserProfile>? selectedProfiles = null);
        Task CollectArtifactsAsync(IEnumerable<CollectionArtifact> artifacts, string destinationPath, Action<string> logCallback, Action<double> progressCallback);
    }

    public enum ArtifactStatus
    {
        Pending,
        Loading,
        Ready,
        NotFound,
        Collected
    }

    public partial class CollectionArtifact : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _sourcePath = string.Empty;

        [ObservableProperty]
        private bool _isDirectory;

        [ObservableProperty]
        private bool _requiresVss;

        [ObservableProperty]
        private string _toolRecommendation = string.Empty;

        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SizeDisplay))]
        private long _estimatedSize;

        [ObservableProperty]
        private string _category = "General";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SizeDisplay))]
        private ArtifactStatus _status = ArtifactStatus.Pending;

        public string SizeDisplay
        {
            get
            {
                if (Status == ArtifactStatus.Loading) return "Loading...";
                if (Status == ArtifactStatus.NotFound) return "File Not Found";
                if (Status == ArtifactStatus.Collected) return "Collected ✅";
                return FormatSize(EstimatedSize);
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
