using CommunityToolkit.Mvvm.ComponentModel;

namespace GlassDFIR.Models
{
    public partial class UserProfile : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private bool _isSelected = true;

        public override string ToString() => Name;
    }
}
