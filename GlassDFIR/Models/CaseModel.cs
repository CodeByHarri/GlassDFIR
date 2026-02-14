using System;
using System.IO;

namespace GlassDFIR.Models
{
    public class CaseModel
    {
        public string Id { get; set; } = string.Empty; // e.g., 0001
        public string Name { get; set; } = string.Empty; // e.g., Indigo Elephant
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; }

        public string FolderName => $"{Id}_{Name.Replace(" ", "")}";
        
        // Root path is set by CaseManager
        public string? BasePath { get; set; }

        public string CasePath => BasePath != null ? Path.Combine(BasePath, "Cases", FolderName) : string.Empty;
        public string EvidencePath => Path.Combine(CasePath, "Evidence");
        public string OutputPath => Path.Combine(CasePath, "Output");

        public string ActiveDisplay => $"ID{Id}: {Name}";
    }
}
