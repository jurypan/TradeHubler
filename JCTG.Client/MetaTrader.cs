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
            _appConfig = appConfig;
            _apis = new List<MetatraderApi>();
            foreach (var api in _appConfig.BrokersToWatch)
            {
                var _api = new MetatraderApi(api.MetaTraderDirPath, api.ClientId, appConfig.SleepDelay, appConfig.MaxRetryCommandSeconds, appConfig.LoadOrdersFromFile, appConfig.Verbose);
                _api.OnOrderEvent += OnOrderEvent;
                _api.OnLogEvent += OnLogEvent;

                _apis.Add(_api);
            }
        }


        private async Task TimerTaskAsync()
        {
            // Do the API CAll
            var backend = Program.Service?.GetService<AzureFunctionApiClient>();
            if (backend != null && _appConfig != null)
            {
                foreach (var _api in _apis)
                {
                    if (_api != null &&  _api.AccountInfo != null && _api.MarketData != null && _api.MarketData.Any())
                    {
                        foreach (var ticker in new List<PairsToWatch>(_appConfig.PairsToWatch.Where(f => f.ClientId == _api.ClientId)))
                        {
                            var marketDataTicker = _api.MarketData.FirstOrDefault(f => f.Key == ticker.TickerInMetatrader).Value;

                            if (marketDataTicker.Ask > 0)
                            {
                                var response = await backend.GetMetatraderResponseAsync(_appConfig.AccountId, _api.ClientId, ticker.TickerInMetatrader, marketDataTicker.Ask, ticker.TickerInTradingView, ticker.StrategyNr);

                                if (response != null)
                                {
                                    if (response.Action == "BUY")
                                    {
                                        // Make buy order
                                        var lotSize = CalculateLotSize(_api.AccountInfo.Balance, ticker.Risk, marketDataTicker.Ask - response.StopLoss, marketDataTicker.TickValue, marketDataTicker.MinLotSize, marketDataTicker.VolumeStep);

                                        // Console
                                        Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                        Print($"ticker={ticker.TickerInMetatrader},order='buy',lz={lotSize},price=0,sl={response.StopLoss},tp={response.TakeProfit},magic=0,comment={response.Comment}");
                                        Print("------------------------------------------------");

                                        // Open order
                                        _api.OpenOrder(ticker.TickerInMetatrader, OrderType.Buy, lotSize, 0, response.StopLoss, response.TakeProfit, 0, response.Comment);
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
            // Start the system
            foreach (var _api in _apis)
            {
                if (_api != null && _appConfig != null && _appConfig?.PairsToWatch != null)
                {
                    await _api.StartAsync();
                    _api.SubscribeForTicks(_appConfig.PairsToWatch.Where(f => f.ClientId == _api.ClientId).Select(f => f.TickerInMetatrader).ToList());

                    if(_api.ACTIVE)
                    {
                        // Display account information
                        Print("--------------- ACCOUNT INFO ------------------");
                        Print("Account Info: " + _api.AccountInfo?.Name);
                        Print("Number: " + _api.AccountInfo?.Number);
                        Print("Balance: " + _api.AccountInfo?.Balance);
                        Print("Currency: " + _api.AccountInfo?.Currency);
                        Print("Equity: " + _api.AccountInfo?.Equity);
                        Print("Free Margin: " + _api.AccountInfo?.FreeMargin);
                        Print("Leverage: " + _api.AccountInfo?.Leverage);
                        Print("-----------------------------------------------");

                        // Display open orders
                        if(_api.OpenOrders != null)
                        {
                            foreach (var openOrder in _api.OpenOrders)
                            {
                                // Display account information
                                Print("--------------- OPEN ORDER --------------------");
                                Print("ID: " + openOrder.Key);
                                Print("Type: " + openOrder.Value.Type);
                                Print("Symbol: " + openOrder.Value.Symbol);
                                Print("Lots: " + openOrder.Value.Lots);
                                Print("Pnl: " + openOrder.Value.Pnl, true);
                                Print("Commission: " + openOrder.Value.Commission);
                                Print("Swap: " + openOrder.Value.Swap);
                                Print("Magic: " + openOrder.Value.Magic);
                                Print("Comment: " + openOrder.Value.Comment);
                                Print("-----------------------------------------------");
                            }
                        }
                    }
                }
            }

            // Listen to the backend
            await TimerTaskAsync();
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


        private void OnLogEvent(long id, Log log)
        {
            Print("------------------- LOG ------------------------");
            Print(log.Time + " | " + log.Type);
            if (!string.IsNullOrEmpty(log.Message))
                Print("Message: " + log.Message);
            if (!string.IsNullOrEmpty(log.ErrorType))
                Print("Error Type: " + log.ErrorType);
            if (!string.IsNullOrEmpty(log.Description))
                Print("Description: " + log.Description);
            Print("------------------------------------------------");
        }

        private void OnOrderEvent(Order order)
        {
            Print("------------- !! NEW ORDER !! ------------------");
            Print("Open order: " + order.Symbol + " open orders");
            Print(order.OpenTime + " | " + order.Type + " | " + order.Pnl);
            Print("------------------------------------------------");
        }
    }
}
