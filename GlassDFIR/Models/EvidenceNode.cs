using System.Collections.ObjectModel;
using System.IO;

namespace GlassDFIR.Models
{
    public class EvidenceNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public string DisplaySize => FormatSize(Size);

        public ObservableCollection<EvidenceNode> Children { get; set; } = new ObservableCollection<EvidenceNode>();

        private static string FormatSize(long bytes)
        {
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
