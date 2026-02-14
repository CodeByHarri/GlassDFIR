using CommunityToolkit.Mvvm.ComponentModel;

namespace GlassDFIR.ViewModels
{
    public abstract partial class BladeViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _icon = string.Empty;

        [ObservableProperty]
        private string _iconColor = "#c0caf5"; // Default PrimaryTextBrush

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isEnabled = true;
    }
}
