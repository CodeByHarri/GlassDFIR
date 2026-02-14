using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace GlassDFIR.Controls
{
    public static class TextHighlighter
    {
        // 1. HighlightText Attached Property
        public static readonly DependencyProperty HighlightTextProperty =
            DependencyProperty.RegisterAttached(
                "HighlightText",
                typeof(string),
                typeof(TextHighlighter),
                new PropertyMetadata(string.Empty, OnPropertyChanged));

        public static string GetHighlightText(DependencyObject obj)
        {
            return (string)obj.GetValue(HighlightTextProperty);
        }

        public static void SetHighlightText(DependencyObject obj, string value)
        {
            obj.SetValue(HighlightTextProperty, value);
        }

        // 2. Text Attached Property (Source of truth for content)
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached(
                "Text",
                typeof(string),
                typeof(TextHighlighter),
                new PropertyMetadata(string.Empty, OnPropertyChanged));

        public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
        public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock tb)
            {
                UpdateHighlighting(tb);
            }
        }

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached("IsUpdating", typeof(bool), typeof(TextHighlighter), new PropertyMetadata(false));
            
        private static bool GetIsUpdating(DependencyObject d) => (bool)d.GetValue(IsUpdatingProperty);
        private static void SetIsUpdating(DependencyObject d, bool value) => d.SetValue(IsUpdatingProperty, value);

        private static void UpdateHighlighting(TextBlock textBlock)
        {
            if (GetIsUpdating(textBlock)) return;

            string text = GetText(textBlock);
            string highlight = GetHighlightText(textBlock);

            SetIsUpdating(textBlock, true);
            try
            {
                textBlock.Inlines.Clear();

                if (string.IsNullOrEmpty(text))
                {
                    if (!string.IsNullOrEmpty(textBlock.Text)) textBlock.Text = "";
                    return;
                }

                if (string.IsNullOrEmpty(highlight))
                {
                    textBlock.Text = text; 
                    return;
                }

                int index = 0;
                while (index < text.Length)
                {
                    int matchIndex = text.IndexOf(highlight, index, StringComparison.OrdinalIgnoreCase);

                    if (matchIndex == -1)
                    {
                        textBlock.Inlines.Add(new Run(text.Substring(index)));
                        break;
                    }

                    if (matchIndex > index)
                    {
                        textBlock.Inlines.Add(new Run(text.Substring(index, matchIndex - index)));
                    }

                    var run = new Run(text.Substring(matchIndex, highlight.Length))
                    {
                        Background = Brushes.Yellow,
                        Foreground = Brushes.Black,
                        FontWeight = FontWeights.Bold
                    };
                    textBlock.Inlines.Add(run);

                    index = matchIndex + highlight.Length;
                }
            }
            finally
            {
                SetIsUpdating(textBlock, false);
            }
        }
    }
}
