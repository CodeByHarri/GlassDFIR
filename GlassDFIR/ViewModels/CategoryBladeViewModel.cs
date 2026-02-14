using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GlassDFIR.ViewModels
{
    public partial class CategoryBladeViewModel : BladeViewModel
    {
        [ObservableProperty]
        private ObservableCollection<BladeViewModel> _subBlades = new();

        [ObservableProperty]
        private bool _isExpanded;

        public CategoryBladeViewModel()
        {
            // Categories are not content blades themselves, but containers
            IsEnabled = true;
        }
    }
}
