using System;

namespace GlassDFIR.Controls
{
    public class TimeRangeEventArgs : EventArgs
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public required string PropertyName { get; set; }
    }
}
