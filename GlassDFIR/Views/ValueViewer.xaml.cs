using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace GlassDFIR.Views
{
    public partial class ValueViewer : Window
    {
        private string _originalText = "";
        private List<Run> _matchRuns = new List<Run>();
        private int _currentMatchIndex = -1;

        public ValueViewer(string value)
        {
            InitializeComponent();
            _originalText = value ?? "";
            
            // Load initial text
            LoadText(_originalText);

            // Focus RichTextBox on load? actually focus SearchBox if they want to search?
            // Standard: Focus content.
            Loaded += (s, e) => ValueRichTextBox.Focus();
        }

        private void LoadText(string text)
        {
            var doc = new FlowDocument();
            var args = new Paragraph();
            args.Inlines.Add(new Run(text));
            doc.Blocks.Add(args);
            
            // Set styles to match dark theme explicitly if needed, though inherited from Control
            doc.PagePadding = new Thickness(0);
            ValueRichTextBox.Document = doc;
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            // RichTextBox copy logic
            ValueRichTextBox.SelectAll();
            ValueRichTextBox.Copy();
            // Deselect?
            ValueRichTextBox.Selection.Select(ValueRichTextBox.Document.ContentEnd, ValueRichTextBox.Document.ContentEnd);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Title Bar Logic
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        // Search Logic
        // RichTextBox doesn't have KeyDown event in the same simple way for 'F', but we can handle it on the Window or bubbling
        // The Window KeyDown handler or standard shortcuts
        
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                ToggleSearch();
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindNext_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CloseSearch_Click(sender, e);
            }
        }

        private void ToggleSearch()
        {
            if (SearchBar.Visibility == Visibility.Collapsed)
            {
                SearchBar.Visibility = Visibility.Visible;
                SearchBox.Focus();
                SearchBox.SelectAll();
                UpdateHighlighting(); // Highlight on open if text exists
            }
            else
            {
                CloseSearch_Click(this, new RoutedEventArgs());
            }
        }

        private void CloseSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBar.Visibility = Visibility.Collapsed;
            _matchRuns.Clear();
            _currentMatchIndex = -1;
            MatchCountText.Text = "";
            
            // Restore original clean text (remove highlights)
            LoadText(_originalText);
            ValueRichTextBox.Focus();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateHighlighting();
        }

        private void UpdateHighlighting()
        {
            _matchRuns.Clear();
            _currentMatchIndex = -1;
            string query = SearchBox.Text;

            if (string.IsNullOrEmpty(query))
            {
                MatchCountText.Text = "";
                LoadText(_originalText);
                return;
            }

            var doc = new FlowDocument();
            doc.PagePadding = new Thickness(0);
            var paragraph = new Paragraph();

            string text = _originalText;
            int index = 0;

            while (index < text.Length)
            {
                int matchIndex = text.IndexOf(query, index, StringComparison.OrdinalIgnoreCase);
                if (matchIndex == -1)
                {
                    // No more matches, add remaining text
                    paragraph.Inlines.Add(new Run(text.Substring(index)));
                    break;
                }

                // Add text before match
                if (matchIndex > index)
                {
                    paragraph.Inlines.Add(new Run(text.Substring(index, matchIndex - index)));
                }

                // Add match (Highlighted)
                var run = new Run(text.Substring(matchIndex, query.Length))
                {
                    Background = Brushes.Yellow,
                    Foreground = Brushes.Black,
                    FontWeight = FontWeights.Bold
                };
                paragraph.Inlines.Add(run);
                _matchRuns.Add(run);

                index = matchIndex + query.Length;
            }

            doc.Blocks.Add(paragraph);
            ValueRichTextBox.Document = doc;

            if (_matchRuns.Count > 0)
            {
                _currentMatchIndex = 0;
                MatchCountText.Text = $"1/{_matchRuns.Count}";
                ScrollToCurrentMatch();
            }
            else
            {
                MatchCountText.Text = "0/0";
            }
        }

        private void FindNext_Click(object sender, RoutedEventArgs e)
        {
            if (_matchRuns.Count == 0) return;

            _currentMatchIndex++;
            if (_currentMatchIndex >= _matchRuns.Count)
            {
                _currentMatchIndex = 0; // Wrap around
            }

            MatchCountText.Text = $"{_currentMatchIndex + 1}/{_matchRuns.Count}";
            ScrollToCurrentMatch();
        }

        private void ScrollToCurrentMatch()
        {
            if (_currentMatchIndex >= 0 && _currentMatchIndex < _matchRuns.Count)
            {
                var run = _matchRuns[_currentMatchIndex];
                run.BringIntoView();
                
                // Optional: Select it too?
                // ValueRichTextBox.Selection.Select(run.ContentStart, run.ContentEnd);
            }
        }
    }
}
