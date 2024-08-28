namespace JCTG.Client
{
    public class SpreadMonitor(long clientId, bool isLong, string instrument, int magic)
    {
        public static SpreadMonitor InitAndStart(long clientId, bool isLong, string instrument, int magic, double intervalInSeconds = 1)
        {
            var monitor = new SpreadMonitor(clientId, isLong, instrument, magic);
            monitor.Start(intervalInSeconds * 1000);
            return monitor;
        }

        public bool IsStarted { get; private set; } = false;
        public long ClientId { get; private set; } = clientId;
        public string Instrument { get; private set; } = instrument;
        public bool IsLong { get; private set; } = isLong;
        public int Magic { get; private set; } = magic;
        public decimal CurrentSpread { get; private set; }
        public decimal PreviousSpread { get; private set; } = decimal.MaxValue; // Initialize to a high value so that any valid spread will be lower
        private bool _isSpreadHigh;
        private DateTime _highSpreadStartTime;
        private Timer? _timer = null;

        // Event that is triggered when the spread decreases
        public delegate void OnMonitorSpreadChangedEventHandler(long clientId, bool isLong, string instrument, int magic);
        public event OnMonitorSpreadChangedEventHandler? OnSpreadChanged;

        // Starts monitoring the spread
        public void Start(double intervalMilliseconds = 1000)
        {
            IsStarted = true;
            _timer = new Timer(CheckSpread, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(intervalMilliseconds));
        }

        // Stops monitoring the spread
        public void Stop()
        {
            IsStarted = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        // Call this method to update the current spread based on the latest bid and ask prices
        public void UpdateSpread(decimal spread)
        {
            CurrentSpread = spread;
        }

        private void CheckSpread(object? state)
        {
            // Check if the spread decreased compared to the previous spread
            if (CurrentSpread != PreviousSpread)
            {
                OnSpreadChanged?.Invoke(ClientId, IsLong, Instrument, Magic);
            }

            // Save the current spread as the previous spread for the next check
            PreviousSpread = CurrentSpread;
        }
    }
}
