using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using static JCTG.Client.Helpers;

namespace JCTG.Client
{
    public class Metatrader
    {
        private readonly AppConfig? _appConfig;
        private readonly MetatraderApi? _api;
        private Dictionary<string, double> _lastTickPrice = new Dictionary<string, double>();

        public Metatrader(AppConfig appConfig) 
        {
            _appConfig = appConfig;
            _api = new MetatraderApi(appConfig.MetaTraderDirPath, appConfig.SleepDelay, appConfig.MaxRetryCommandSeconds, appConfig.LoadOrdersFromFile, appConfig.Verbose);
            _api.OnTickEvent += OnTick;
            _api.OnOrderEvent += OnOrderEvent;
            _api.OnMessageEvent += OnMessage;
        }

        private async Task TimerTaskAsync()
        {
            // Do the API CAll
            var backend = Program.Service?.GetService<AzureFunctionApiClient>();
            if(backend != null && _api != null && _appConfig != null) 
            {
                foreach (var ticker in new List<PairsToWatch>(_appConfig.PairsToWatch))
                {
                    var lastTickPrice = _lastTickPrice.FirstOrDefault(f => f.Key == ticker.TickerInMetatrader);

                    if (lastTickPrice.Value > 0)
                    {
                        var response = await backend.GetMetatraderResponseAsync(_appConfig.AccountId, _appConfig.ClientId, lastTickPrice.Key, lastTickPrice.Value, ticker.TickerInTradingView, ticker.StrategyNr);

                        if (response != null)
                        {
                            if (response.Action == "BUY")
                            {
                                // Make buy order
                                var lotSize = CalculateLotSize(_api.AccountInfo["balance"].Value<double>(), ticker.Risk, lastTickPrice.Value - response.StopLoss);
                                _api.OpenOrder(ticker.TickerInMetatrader, "buy", lotSize, 0, response.StopLoss, response.TakeProfit, 0, response.Comment, 0);
                            }
                        }
                    }
                }

                // Wait a little bit
                await Task.Delay(_appConfig.SleepDelay);

                if(_api.ACTIVE) 
                {
                    await TimerTaskAsync();
                }
            }
        }

        public async Task StartAsync()
        {
            // Define the size of the array based on the number of pairs to watch
            for (int i = 0; i < _appConfig?.PairsToWatch.Count; i++)
                if (!_lastTickPrice.ContainsKey(_appConfig.PairsToWatch[i].TickerInMetatrader))
                    _lastTickPrice.Add(_appConfig.PairsToWatch[i].TickerInMetatrader, 0);

            // Start the system
            if(_api != null)
            {
                await _api.StartAsync();
                _api.SubscribeSymbols(_lastTickPrice.Select(f => f.Key).ToArray());
            }

            // Display account information
            Console.WriteLine("\nAccount info:\n" + _api?.AccountInfo + "\n");

            // Listen to the backend
            await TimerTaskAsync();
        }

        private double CalculateLotSize(double accountBalance, double riskAmount, double stopLossPriceInPips)
        {
            return Math.Round(accountBalance * riskAmount / stopLossPriceInPips, 2);
        }


        private void OnMessage(JObject message)
        {
            if (((string)message["type"]).Equals("ERROR"))
                Print(message["type"] + " | " + message["error_type"] + " | " + message["description"]);
            else if (((string)message["type"]).Equals("INFO"))
                Print(message["type"] + " | " + message["message"]);
        }

        private void OnOrderEvent()
        {
            Print("onOrderEvent: " + _api?.OpenOrders.Count + " open orders");
        }

        private void OnTick(string symbol, double bid, double ask)
        {
            //Print("onTick: " + symbol + " | bid: " + bid + " | ask: " + ask);

            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol cannot be null or whitespace.", nameof(symbol));
            _lastTickPrice[symbol] = ask;
        }
    }
}
