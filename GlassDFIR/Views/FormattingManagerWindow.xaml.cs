using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using GlassDFIR.Controls;

namespace GlassDFIR.Views
{
    public partial class FormattingManagerWindow : Window
    {
        private ObservableCollection<FormattingRule> _rules;
        private System.Collections.Generic.List<FormattingRule> _defaultRules = new();

        public FormattingManagerWindow(ObservableCollection<FormattingRule> rules, System.Collections.Generic.List<FormattingRule>? defaultRules = null)
        {
            InitializeComponent();
            _rules = rules;
            _defaultRules = defaultRules ?? new System.Collections.Generic.List<FormattingRule>();
            RulesList.ItemsSource = _rules;
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (_defaultRules == null || _defaultRules.Count == 0)
            {
                MessageBox.Show("No default rules are configured.", "Defaults Unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("This will replace all current rules with the defaults. Continue?", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _rules.Clear();
                foreach (var rule in _defaultRules)
                {
                    _rules.Add(rule);
                }
            }
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FormattingRule rule)
            {
                _rules.Remove(rule);
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear all formatting rules?", "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _rules.Clear();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
