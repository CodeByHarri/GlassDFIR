namespace GlassDFIR.Models
{
    public class ToolModule
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DefaultInputPath { get; set; } = string.Empty;
        public string DefaultOutputPath { get; set; } = string.Empty;
        public string ArgumentsTemplate { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public bool IsReady { get; set; }
        public bool IsCsvAvailable { get; set; }
    }
}
