using System;
using GlassDFIR.Models;

namespace GlassDFIR.ViewModels
{
    public partial class ToolBladeViewModel : BladeViewModel
    {
        public ToolModule Tool { get; }

        public ToolBladeViewModel(ToolModule tool)
        {
            Tool = tool;
            Title = $"{tool.Description} ({tool.Name})";
            Icon = "🛠️";
            IsEnabled = tool.IsReady;
        }
    }
}
