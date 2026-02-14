using CommunityToolkit.Mvvm.ComponentModel;

namespace GlassDFIR.Models
{
    public partial class PluginOption : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private bool _isSelected;

        public PluginOption(string name, string description, bool isSelected = false)
        {
            Name = name;
            Description = description;
            IsSelected = isSelected;
        }
    }
}
