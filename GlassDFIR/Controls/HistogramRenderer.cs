using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace GlassDFIR.Controls
{
    public class HistogramRenderer : FrameworkElement
    {
        public static readonly DependencyProperty BinsProperty =
            DependencyProperty.Register("Bins", typeof(List<TimeBin>), typeof(HistogramRenderer), 
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinTimeProperty =
            DependencyProperty.Register("MinTime", typeof(DateTime), typeof(HistogramRenderer),
                new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxTimeProperty =
            DependencyProperty.Register("MaxTime", typeof(DateTime), typeof(HistogramRenderer),
                new FrameworkPropertyMetadata(DateTime.MaxValue, FrameworkPropertyMetadataOptions.AffectsRender));

        public List<TimeBin>? Bins
        {
            get => (List<TimeBin>?)GetValue(BinsProperty);
            set => SetValue(BinsProperty, value);
        }

        public DateTime MinTime
        {
            get => (DateTime)GetValue(MinTimeProperty);
            set => SetValue(MinTimeProperty, value);
        }

        public DateTime MaxTime
        {
            get => (DateTime)GetValue(MaxTimeProperty);
            set => SetValue(MaxTimeProperty, value);
        }

        public Brush BarBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7aa2f7"));

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (Bins == null || Bins.Count == 0 || ActualWidth == 0 || ActualHeight == 0) return;

            var bins = Bins;
            double maxCount = bins.Max(b => b.Count);
            if (maxCount == 0) maxCount = 1;

            // Use provided Min/Max or fallback to bins
            var minTime = MinTime == DateTime.MinValue ? bins.First().Time : MinTime;
            var maxTime = MaxTime == DateTime.MaxValue ? bins.Last().Time : MaxTime;
            
            var totalDurationTicks = (maxTime - minTime).Ticks;
            if (totalDurationTicks == 0) totalDurationTicks = 1;

            double width = ActualWidth;
            double height = ActualHeight;
            
            // Calculate a dynamic bar width based on number of bins
            double barWidth = Math.Max(1, (width / bins.Count) - 0.5);
            
            // pre-freeze brush for performance
            if (BarBrush.IsFrozen == false && BarBrush.CanFreeze) BarBrush.Freeze();

            foreach (var bin in bins)
            {
                // Only draw bins within the min/max range
                if (bin.Time < minTime || bin.Time > maxTime) continue;

                double normalizedTime = (double)(bin.Time - minTime).Ticks / totalDurationTicks;
                double x = normalizedTime * width; 
                
                double normalizedHeight = (double)bin.Count / maxCount;
                double barHeight = Math.Max(2, normalizedHeight * height);

                // Draw directly to context
                dc.DrawRectangle(BarBrush, null, new Rect(x, height - barHeight, barWidth, barHeight));
            }
        }
    }
}
