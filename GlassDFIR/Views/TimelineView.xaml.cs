using System.Windows.Controls;

namespace GlassDFIR.Views
{
    public partial class TimelineView : UserControl
    {
        public TimelineView()
        {
            InitializeComponent();
        }

        private void ManageRules_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = new FormattingManagerWindow(DataGrid.FormattingRules, DataGrid.DefaultRules);
            win.Owner = System.Windows.Application.Current.MainWindow;
            win.ShowDialog();
        }

        private void ExportFiltered_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DataGrid.ExportToCSV();
        }
    }
}
