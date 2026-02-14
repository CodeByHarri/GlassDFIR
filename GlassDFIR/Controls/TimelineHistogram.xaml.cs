using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Data;

namespace GlassDFIR.Controls
{
    public partial class TimelineHistogram : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(System.Collections.IEnumerable), typeof(TimelineHistogram), new PropertyMetadata(null, OnItemsSourceChanged));

        public System.Collections.IEnumerable ItemsSource
        {
            get => (System.Collections.IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public event EventHandler<TimeRangeEventArgs?>? FilterChanged;
        public event EventHandler<string>? DateColumnChanged;

        private List<object> _rawData = new List<object>();
        private string _dateColumn = string.Empty;
        private List<TimeBin> _bins = new List<TimeBin>();
        private TimeSpan _currentBinSize = TimeSpan.Zero; // Auto
        
        private System.Windows.Threading.DispatcherTimer _debounceTimer;

        public TimelineHistogram()
        {
            InitializeComponent();
            
            _debounceTimer = new System.Windows.Threading.DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(250); // Debounce delay
            _debounceTimer.Tick += (s, args) => 
            {
                _debounceTimer.Stop();
                TriggerFilterEvent();
            };

            SizeChanged += (s, e) => { if (IsLoaded) DrawChart(); };
            Loaded += (s, e) => DrawChart();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TimelineHistogram control)
            {
                control.ReloadData();
            }
        }

        private async void ReloadData()
        {
            var source = ItemsSource;
            if (source == null) 
            {
                _rawData = new List<object>();
                DateColumnSelector.Items.Clear();
                BarRenderer.Bins = new List<TimeBin>();
                BarRenderer.InvalidateVisual();
                return;
            }

            // Process data in background
            await Task.Run(() => 
            {
                var list = new List<object>();
                foreach (var item in source) list.Add(item);
                _rawData = list;
                
                // These methods now handle their own internal data but return UI-relevant info or update fields safely
                // Actually, I'll refactor them to be more task-friendly
            });

            await PopulateDateColumnsAsync();
            await BinDataAsync();
            DrawChart();
        }

        private async Task PopulateDateColumnsAsync()
        {
            var data = _rawData;
            if (data == null || data.Count == 0) return;

            var dateProps = await Task.Run(() => 
            {
                var props = new List<string>();
                var item = data[0];

                if (item is DataRowView drv)
                {
                    foreach (DataColumn col in drv.Row.Table.Columns)
                    {
                        if (col.DataType == typeof(DateTime)) props.Add(col.ColumnName);
                        else if (col.DataType == typeof(string))
                        {
                            var val = drv[col.ColumnName]?.ToString();
                            if (!string.IsNullOrEmpty(val) && DateTime.TryParse(val, out _))
                                props.Add(col.ColumnName);
                        }
                    }
                }
                else
                {
                    var typeProps = item.GetType().GetProperties();
                    foreach(var p in typeProps)
                    {
                        if (p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?)) props.Add(p.Name);
                        else if (p.PropertyType == typeof(string))
                        {
                            var val = p.GetValue(item)?.ToString();
                            if (!string.IsNullOrEmpty(val) && DateTime.TryParse(val, out _))
                                props.Add(p.Name);
                        }
                    }
                }
                return props.Distinct().ToList();
            });

            var currentSelection = DateColumnSelector.SelectedItem as string;
            DateColumnSelector.Items.Clear();
            foreach (var prop in dateProps)
            {
                DateColumnSelector.Items.Add(prop);
            }

            if (DateColumnSelector.Items.Count > 0)
            {
                if (!string.IsNullOrEmpty(currentSelection) && DateColumnSelector.Items.Contains(currentSelection))
                    DateColumnSelector.SelectedItem = currentSelection;
                else
                    DateColumnSelector.SelectedIndex = 0;
            }
        }

        private async void DateColumnSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateColumnSelector.SelectedItem is string col)
            {
                _dateColumn = col;
                await BinDataAsync();
                DrawChart();
                DateColumnChanged?.Invoke(this, col);
            }
        }

        private async Task BinDataAsync(DateTime? start = null, DateTime? end = null)
        {
            var dataInProgress = _rawData;
            var columnInProgress = _dateColumn;
            var binSizeOverride = _currentBinSize;

            if (string.IsNullOrEmpty(columnInProgress) || dataInProgress == null || dataInProgress.Count == 0) 
            {
                _bins = new List<TimeBin>();
                return;
            }

            _bins = await Task.Run(() => 
            {
                var times = new List<DateTime>();
                foreach (var item in dataInProgress)
                {
                    DateTime? dt = null;
                    if (item is DataRowView drv)
                    {
                        if (drv.Row.Table.Columns.Contains(columnInProgress))
                        {
                            var val = drv[columnInProgress];
                            if (val != DBNull.Value)
                            {
                                if (val is DateTime d) dt = d;
                                else if (val is string s && DateTime.TryParse(s, out var parsed)) dt = parsed;
                            }
                        }
                    }
                    else
                    {
                        var prop = item.GetType().GetProperty(columnInProgress);
                        var val = prop?.GetValue(item);
                        if (val is DateTime d) dt = d;
                        else if (val is string s && DateTime.TryParse(s, out var parsed)) dt = parsed;
                    }

                    if (dt.HasValue && dt.Value > DateTime.MinValue && dt.Value < DateTime.MaxValue)
                    {
                        if (start.HasValue && dt.Value < start.Value) continue;
                        if (end.HasValue && dt.Value > end.Value) continue;
                        times.Add(dt.Value);
                    }
                }

                if (times.Count == 0) return new List<TimeBin>();

                times.Sort();
                var rangeMin = start ?? times.First();
                var rangeMax = end ?? times.Last();
                var duration = rangeMax - rangeMin;
                
                TimeSpan binSize = binSizeOverride;
                if (binSize == TimeSpan.Zero)
                {
                    if (duration.TotalMinutes < 2) binSize = TimeSpan.FromSeconds(1);
                    else if (duration.TotalMinutes < 10) binSize = TimeSpan.FromSeconds(10);
                    else if (duration.TotalHours < 1) binSize = TimeSpan.FromMinutes(1);
                    else if (duration.TotalHours < 6) binSize = TimeSpan.FromMinutes(5);
                    else if (duration.TotalHours < 24) binSize = TimeSpan.FromMinutes(30);
                    else if (duration.TotalDays < 7) binSize = TimeSpan.FromHours(1);
                    else if (duration.TotalDays < 30) binSize = TimeSpan.FromHours(6);
                    else binSize = TimeSpan.FromDays(1);
                }

                if (binSize.TotalSeconds == 0) binSize = TimeSpan.FromMinutes(1);

                var buckets = times.GroupBy(t => RoundDown(t, binSize))
                                   .Select(g => new TimeBin { Time = g.Key, Count = g.Count() })
                                   .OrderBy(b => b.Time)
                                   .ToList();

                if (buckets.Count == 1)
                    buckets.Add(new TimeBin { Time = buckets[0].Time.Add(binSize), Count = 0 });

                return buckets;
            });
        }

        private DateTime RoundDown(DateTime dt, TimeSpan d)
        {
            return new DateTime((dt.Ticks / d.Ticks) * d.Ticks);
        }

        private void DrawChart()
        {
            AxisCanvas.Children.Clear();

            var start = StartDatePicker.SelectedDate ?? (_bins.FirstOrDefault()?.Time ?? DateTime.MinValue);
            var end = EndDatePicker.SelectedDate ?? (_bins.LastOrDefault()?.Time ?? DateTime.MaxValue);

            if (_bins == null || _bins.Count == 0) 
            {
                BarRenderer.Bins = new List<TimeBin>();
                return;
            }

            // Push data to renderer
            BarRenderer.Bins = _bins;
            BarRenderer.MinTime = start;
            BarRenderer.MaxTime = end;

            double width = ActualWidth;
            if (width <= 0) width = 800; // fallback

            // Draw simple axis labels (Start, 25%, 50%, 75%, End)
            long totalTicks = (end - start).Ticks;
            
            // Start (Left aligned)
            AddAxisLabel(start.ToString("yyyy-MM-dd HH:mm:ss"), 0, TextAlignment.Left);
            
            // 25%
            var time25 = start.AddTicks((long)(totalTicks * 0.25));
            AddAxisLabel(time25.ToString("yyyy-MM-dd HH:mm:ss"), width * 0.25, TextAlignment.Center);

            // 50%
            var time50 = start.AddTicks((long)(totalTicks * 0.5));
            AddAxisLabel(time50.ToString("yyyy-MM-dd HH:mm:ss"), width * 0.5, TextAlignment.Center);

            // 75%
            var time75 = start.AddTicks((long)(totalTicks * 0.75));
            AddAxisLabel(time75.ToString("yyyy-MM-dd HH:mm:ss"), width * 0.75, TextAlignment.Center);

            // End (Right aligned)
            AddAxisLabel(end.ToString("yyyy-MM-dd HH:mm:ss"), width, TextAlignment.Right);

            // Update range pickers ONLY if they aren't set (initial load)
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
            {
                 var minTime = _bins.First().Time;
                 var maxTime = _bins.Last().Time;
                 UpdateDatePickers(minTime, maxTime);
            }
        }

        private void AddAxisLabel(string text, double x, TextAlignment alignment)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = (Brush)FindResource("SecondaryTextBrush"),
                FontSize = 10
            };

            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double textWidth = tb.DesiredSize.Width;

            double resultX = x;
            if (alignment == TextAlignment.Center)
            {
                resultX = x - (textWidth / 2);
            }
            else if (alignment == TextAlignment.Right)
            {
                resultX = x - textWidth;
            }

            // Clamp to bounds to prevent clipping
            if (resultX < 0) resultX = 0;
            if (resultX + textWidth > AxisCanvas.ActualWidth && AxisCanvas.ActualWidth > 0) 
                resultX = AxisCanvas.ActualWidth - textWidth;

            Canvas.SetLeft(tb, resultX);
            Canvas.SetTop(tb, 0);
            AxisCanvas.Children.Add(tb);
        }


        private async void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingDatePickers) return;
            await ApplyFilterAsync();
        }

        private bool _isUpdatingDatePickers = false;

        private void UpdateDatePickers(DateTime min, DateTime max)
        {
            _isUpdatingDatePickers = true;
            try
            {
                if (StartDatePicker.SelectedDate == null || StartDatePicker.SelectedDate < min)
                    StartDatePicker.SelectedDate = min;
                
                if (EndDatePicker.SelectedDate == null || EndDatePicker.SelectedDate > max)
                    EndDatePicker.SelectedDate = max;
            }
            finally
            {
                _isUpdatingDatePickers = false;
            }
        }

        private async Task ApplyFilterAsync()
        {
            if (_bins == null || StartDatePicker == null || EndDatePicker == null) return;

            var start = StartDatePicker.SelectedDate ?? DateTime.MinValue;
            var end = EndDatePicker.SelectedDate ?? DateTime.MaxValue;

            // Visual Validation
            if (start > end)
            {
                DateValidationError.Visibility = Visibility.Visible;
                StartDatePicker.BorderBrush = new SolidColorBrush(Color.FromRgb(247, 118, 142)); // #f7768e red
                EndDatePicker.BorderBrush = new SolidColorBrush(Color.FromRgb(247, 118, 142));
                return; // Stop here, don't update chart or fire event
            }
            else
            {
                DateValidationError.Visibility = Visibility.Collapsed;
                StartDatePicker.BorderBrush = new SolidColorBrush(Color.FromRgb(65, 72, 104)); // Restore #414868
                EndDatePicker.BorderBrush = new SolidColorBrush(Color.FromRgb(65, 72, 104));
            }

            // Re-bin data based on new timeframe for dynamic zoom
            await BinDataAsync(start, end);
            DrawChart();

            // Adjust end to end of day if it's the same day (optional, or just exact date)
            if (start.Date == end.Date) end = end.Date.AddDays(1).AddTicks(-1);
            else end = end.Date.AddDays(1).AddTicks(-1); // Inclusive of the end day

            ResetBtn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7aa2f7")); // Highlight active

            // Debounce the actual filter event
            _pendingArgs = new TimeRangeEventArgs { Start = start, End = end, PropertyName = _dateColumn };
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private TimeRangeEventArgs? _pendingArgs;

        private void TriggerFilterEvent()
        {
            if (_pendingArgs != null)
                FilterChanged?.Invoke(this, _pendingArgs);
        }

        private async void ResetFilter_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingDatePickers = true;
            try
            {
                StartDatePicker.SelectedDate = null;
                EndDatePicker.SelectedDate = null;
                DateValidationError.Visibility = Visibility.Collapsed;
                StartDatePicker.BorderBrush = new SolidColorBrush(Color.FromRgb(65, 72, 104));
                EndDatePicker.BorderBrush = new SolidColorBrush(Color.FromRgb(65, 72, 104));
            }
            finally
            {
                _isUpdatingDatePickers = false;
            }

            await BinDataAsync(null, null); // Full range
            DrawChart();
            
            _debounceTimer.Stop();
            _pendingArgs = null;
            FilterChanged?.Invoke(this, null);
        }

        private async void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
             if (_bins == null || _bins.Count == 0) return;
             
             // Get current view range
             var start = StartDatePicker.SelectedDate;
             var end = EndDatePicker.SelectedDate;
             var duration = (end ?? _bins.Last().Time) - (start ?? _bins.First().Time);

             if (_currentBinSize == TimeSpan.Zero) _currentBinSize = duration.Divide(10); 
             else _currentBinSize = _currentBinSize.Divide(2);
             
             if (_currentBinSize.TotalSeconds < 1) _currentBinSize = TimeSpan.FromSeconds(1);

             await BinDataAsync(start, end); 
             DrawChart();
        }

        private async void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
             if (_bins == null || _bins.Count == 0) return;
             
             // Get current view range
             var start = StartDatePicker.SelectedDate;
             var end = EndDatePicker.SelectedDate;
             
             if (_currentBinSize == TimeSpan.Zero) _currentBinSize = TimeSpan.FromDays(1);
             else _currentBinSize = _currentBinSize.Multiply(2);
             
             await BinDataAsync(start, end);
             DrawChart();
        }
    }
}
