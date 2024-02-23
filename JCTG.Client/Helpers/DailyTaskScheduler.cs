using JCTG.Models;

namespace JCTG.Client
{
    public class DailyTaskScheduler
    {
        private DateTime? lastExecutionDate = null;
        private readonly Timer timer;
        private readonly string symbol;
        private readonly TimeSpan targetTime;
        private readonly long clientId;
        private readonly StrategyType strategyType;

        public delegate void OnTimeEventHandler(long clientId, string symbol, StrategyType strategyType);
        public event OnTimeEventHandler? OnTimeEvent;

        public DailyTaskScheduler(long clientId, string symbol, TimeSpan targetTime, StrategyType strategyType) 
        {
            this.clientId = clientId;
            this.symbol = symbol;
            this.targetTime = targetTime;
            this.strategyType = strategyType;
            this.timer = new(CheckTimeAndExecuteOnceDaily, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        public void Stop()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void CheckTimeAndExecuteOnceDaily(object? state)
        {
            DateTime now = DateTime.UtcNow;
            DateTime targetDateTimeToday = now.Date + targetTime; // Combining today's date with the target time

            // Check if we have already executed at the target time today
            if (lastExecutionDate.HasValue && lastExecutionDate.Value.Date == now.Date)
            {
                return; // Skip execution for today as it's already been executed at the target time
            }

            if (now >= targetDateTimeToday)
            {
                // Update the last execution date to today
                lastExecutionDate = now;

                // Throw event
                OnTimeEvent?.Invoke(this.clientId, this.symbol, this.strategyType);
            }
        }

        public static bool CanOpenTrade(TimeSpan? CloseAllTradesAt, int? DoNotOpenTradeXMinutesBeforeClose)
        {
            // Check if both parameters have values
            if (CloseAllTradesAt.HasValue && DoNotOpenTradeXMinutesBeforeClose.HasValue)
            {
                // Get the current UTC time of day
                TimeSpan currentTime = DateTime.UtcNow.TimeOfDay;

                // Calculate the time we should not open trades from
                TimeSpan noTradeStartTime = CloseAllTradesAt.Value - TimeSpan.FromMinutes(DoNotOpenTradeXMinutesBeforeClose.Value);

                // If the current time is after the no trade start time and before the close all trades time, return false
                if (currentTime >= noTradeStartTime && currentTime <= CloseAllTradesAt.Value)
                {
                    return false;
                }
            }

            // Otherwise, it's safe to open trades
            return true;
        }

    }
}
