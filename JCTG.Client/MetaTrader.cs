using Microsoft.Extensions.DependencyInjection;
using static JCTG.Client.Helpers;

namespace JCTG.Client
{
    public class Metatrader
    {
        private readonly AppConfig? _appConfig;
        private readonly List<MetatraderApi> _apis;

        public Metatrader(AppConfig appConfig)
        {
            // Init APP Config + API
            _appConfig = appConfig;
            _apis = new List<MetatraderApi>();

            // Foreach broker, init the API
            foreach (var api in _appConfig.Brokers)
            {
                // Init API
                var _api = new MetatraderApi(api.MetaTraderDirPath, api.ClientId, appConfig.SleepDelay, appConfig.MaxRetryCommandSeconds, appConfig.LoadOrdersFromFile, appConfig.Verbose);
                
                // Init the events
                _api.OnOrderEvent += OnOrderEvent;
                _api.OnLogEvent += OnLogEvent;

                // Add to the list
                _apis.Add(_api);
            }
        }


        private async Task TimerTaskAsync()
        {
            // Do the API CAll
            var backend = Program.Service?.GetService<AzureFunctionApiClient>();

            // Do null reference checks
            if (backend != null && _appConfig != null)
            {
                // Loop through the api
                foreach (var _api in _apis)
                {
                    // Do null reference checks
                    if (_api != null && _api.AccountInfo != null && _api.MarketData != null && _api.MarketData.Any())
                    {
                        // Loop through each pair
                        foreach (var ticker in new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs)))
                        {
                            // Get the metadata tick
                            var metadataTick = _api.MarketData.FirstOrDefault(f => f.Key == ticker.TickerInMetatrader).Value;

                            // Do null reference check
                            if (metadataTick != null && metadataTick.Ask > 0)
                            {
                                // Send the information to the backend
                                var response = await backend.GetMetatraderResponseAsync(_appConfig.AccountId, _api.ClientId, ticker.TickerInMetatrader, metadataTick.Ask, ticker.TickerInTradingView, ticker.StrategyNr);

                                // Do null reference check
                                if (response != null)
                                {
                                    // If response from server is BUY -> BUY in metatrader
                                    if (response.Action == "BUY")
                                    {
                                        // Make buy order
                                        var lotSize = CalculateLotSize(_api.AccountInfo.Balance, ticker.Risk, metadataTick.Ask - response.StopLoss, metadataTick.TickValue, metadataTick.MinLotSize, metadataTick.VolumeStep);

                                        // Print on the screen
                                        Print(Environment.NewLine);
                                        Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == _api.ClientId).Name);
                                        Print("Ticker      : " + ticker.TickerInMetatrader, true);
                                        Print("Order       : BUY MARKET ORDER");
                                        Print("Stop Loss   : " + response.StopLoss);
                                        Print("Take Profit : " + response.TakeProfit);
                                        Print("Magic       : " + response.Magic);
                                        Print("------------------------------------------------");


                                        // Open order
                                        _api.ExecuteOrder(ticker.TickerInMetatrader, OrderType.Buy, lotSize, 0, response.StopLoss, response.TakeProfit, response.Magic);
                                    }
                                }
                            }
                        }
                    }
                }

                // Wait a little bit
                await Task.Delay(_appConfig.SleepDelay);
                await TimerTaskAsync();
            }
        }

        public async Task StartAsync()
        {
            // Check if app config is not null
            if (_appConfig != null)
            {
                // Start the system
                foreach (var _api in _apis)
                {
                    // Get the broker from the local database
                    var broker = _appConfig?.Brokers.FirstOrDefault(f => f.ClientId == _api.ClientId);

                    // do null reference checks
                    if (_api != null && broker != null && broker.Pairs.Any())
                    {
                        // Start the API
                        await _api.StartAsync();

                        // Subscribe foreach pair
                        _api.SubscribeForTicks(broker.Pairs.Select(f => f.TickerInMetatrader).ToList());
                    }
                }

                // Listen to the backend
                await TimerTaskAsync();
            }
        }

        private double CalculateLotSize(double accountBalance, double riskAmount, double stopLossPriceInPips, double valuePerPip, double minLotSizeAllowed, double volumeStep)
        {
            // Calculate the initial lot size
            var initialLotSize = accountBalance * riskAmount / stopLossPriceInPips * valuePerPip;

            // Round down to the nearest multiple of volumeStep
            var adjustedLotSize = Math.Floor(initialLotSize / volumeStep) * volumeStep;

            // Round to 2 decimal places
            adjustedLotSize = Math.Round(adjustedLotSize, 2);

            // Ensure the lot size is not less than the minimum allowed
            if (adjustedLotSize < minLotSizeAllowed)
                return minLotSizeAllowed;
            else
                return adjustedLotSize;
        }


        private void OnLogEvent(int clientId, long id, Log log)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                Print(Environment.NewLine);
                Print("------------------- LOG ------------------------");
                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == clientId).Name);
                Print("Time      : " + log.Time);
                Print("Type      : " + log.Type);
                if (!string.IsNullOrEmpty(log.Message))
                    Print("Message   : " + log.Message);
                if (!string.IsNullOrEmpty(log.ErrorType))
                    Print("Error Type : " + log.ErrorType);
                if (!string.IsNullOrEmpty(log.Description))
                   Print("Description: " + log.Description);
                Print("------------------------------------------------");
            }
        }

        private void OnOrderEvent(int clientId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                Print(Environment.NewLine);
                Print("------------------- ORDER ----------------------");
                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == clientId).Name);
                Print("Time      : " + DateTime.UtcNow);
                Print("Symbol    : " + order.Symbol);
                Print("Type      : " + order.Type);
                Print("Magic     : " + order.Magic);
                Print("------------------------------------------------");
            }
        }
    }
}
