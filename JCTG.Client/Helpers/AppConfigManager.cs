using JCTG.Models;
using Newtonsoft.Json;

namespace JCTG.Client
{
    public class AppConfigManager
    {
        private readonly Timer timer;
        private TerminalConfig? terminalConfig = null;

        public delegate void OnTimeEventHandler(TerminalConfig config);
        public event OnTimeEventHandler? OnTerminalConfigChange;

        public AppConfigManager(TerminalConfig? terminalConfig = null)
        {
            this.terminalConfig = terminalConfig;
            this.timer = new(async o => await CheckTimeAndInvokeEventAsync(o), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        public void Stop()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async Task CheckTimeAndInvokeEventAsync(object? state)
        {
            TerminalConfig? config = await HttpCall.GetTerminalConfigAsync();
            if (config != null) 
            {
                if(terminalConfig == null || !AreObjectsEqualBySerialization(config, terminalConfig))
                {
                    terminalConfig = config;

                    // Throw event
                    OnTerminalConfigChange?.Invoke(config);
                }
            }
            
        }

        private static bool AreObjectsEqualBySerialization<T>(T object1, T object2)
        {
            var json1 = JsonConvert.SerializeObject(object1);
            var json2 = JsonConvert.SerializeObject(object2);
            return json1 == json2;
        }
    }
}
