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

        public async Task ListToTheClientsAsync()
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
            }
        }

        public async Task ListenToTheServerAsync()
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
                    if (_api != null && _api.AccountInfo != null && _api.MarketData != null && _api.MarketData.Count != 0)
                    {
                        // Init request to Azure Function
                        var mtRequests = new List<MetatraderRequest>();

                        // Loop through each pair
                        foreach (var ticker in new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs)))
                        {
                            // Get the metadata tick
                            var metadataTick = _api.MarketData.FirstOrDefault(f => f.Key == ticker.TickerInMetatrader).Value;

                            // Do null reference check
                            if (metadataTick != null && metadataTick.Ask > 0)
                            {
                                mtRequests.Add(new MetatraderRequest()
                                {
                                    AccountID = _appConfig.AccountId,
                                    ClientID = _api.ClientId,
                                    TickerInMetatrader = ticker.TickerInMetatrader,
                                    Price = metadataTick.Ask,
                                    StrategyType = ticker.StrategyNr,
                                    TickerInTradingview = ticker.TickerInTradingView,
                                });
                            }
                        }

                        // Send the information to the backend
                        var mtResponse = await backend.GetMetatraderResponseAsync(mtRequests);

                        // Do null reference check
                        if (mtResponse != null && mtResponse.Count == mtRequests.Count)
                        {
                            // Ittirate through the mtResponse
                            foreach (var response in mtResponse)
                            {
                                // If mtResponse from server is BUY -> BUY in metatrader
                                if (response.Action == "BUY")
                                {
                                    // Get the right ticker back from the local database
                                    var ticker = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(response.TickerInMetatrader));

                                    // Get the metadata tick
                                    var metadataTick = _api.MarketData.FirstOrDefault(f => f.Key == response.TickerInMetatrader).Value;

                                    // Do null reference check
                                    if (ticker != null)
                                    {
                                        // Make buy order
                                        var lotSize = CalculateLotSize(_api.AccountInfo.Balance, ticker.Risk, metadataTick.Ask, response.StopLoss, metadataTick.TickValue, metadataTick.PointSize, metadataTick.LotStep, metadataTick.MinLotSize);

                                        // Print on the screen
                                        Print(Environment.NewLine);
                                        Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == _api.ClientId).Name);
                                        Print("Ticker      : " + ticker.TickerInMetatrader);
                                        Print("Order       : BUY MARKET ORDER");
                                        Print("Lot Size    : " + lotSize);
                                        Print("Ask price   : " + metadataTick.Ask);
                                        Print("Stop Loss   : " + response.StopLoss);
                                        Print("Take Profit : " + response.TakeProfit);
                                        Print("Tick value  : " + metadataTick.TickValue);
                                        Print("Point size  : " + metadataTick.PointSize);
                                        Print("Lot step    : " + metadataTick.LotStep);
                                        Print("Magic       : " + response.Magic);
                                        Print("Strategy    : " + ticker.StrategyNr);
                                        Print("------------------------------------------------");


                                        // Open order
                                        _api.ExecuteOrder(ticker.TickerInMetatrader, OrderType.Buy, lotSize, 0, response.StopLoss, response.TakeProfit, response.Magic);
                                    }
                                }

                                // If mtResponse from server is MODIFY -> MODIFY order in metatrader
                                else if (response.Action == "MODIFY")
                                {
                                    // Get the right ticker back from the local database
                                    var ticker = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(response.TickerInMetatrader));

                                    // Get the metadata tick
                                    var metadataTick = _api.MarketData.FirstOrDefault(f => f.Key == response.TickerInMetatrader).Value;

                                    // Do null reference check
                                    if (ticker != null)
                                    {
                                        // Make buy order
                                        var lotSize = CalculateLotSize(_api.AccountInfo.Balance, ticker.Risk, metadataTick.Ask, response.StopLoss, metadataTick.TickValue, metadataTick.PointSize, metadataTick.LotStep, metadataTick.MinLotSize);

                                        // Print on the screen
                                        Print(Environment.NewLine);
                                        Print("--------- SEND MODIFY ORDER TO METATRADER ---------");
                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == _api.ClientId).Name);
                                        Print("Ticker      : " + ticker.TickerInMetatrader);
                                        Print("Order       : MODIFY ORDER");
                                        Print("Lot Size    : " + lotSize);
                                        Print("Ask price   : " + metadataTick.Ask);
                                        Print("Stop Loss   : " + response.StopLoss);
                                        Print("Take Profit : " + response.TakeProfit);
                                        Print("Tick value  : " + metadataTick.TickValue);
                                        Print("Point size  : " + metadataTick.PointSize);
                                        Print("Lot step    : " + metadataTick.LotStep);
                                        Print("Magic       : " + response.Magic);
                                        Print("Strategy    : " + ticker.StrategyNr);
                                        Print("------------------------------------------------");


                                        // Modify order
                                        _api.ModifyOrder(0,  lotSize, 0, response.StopLoss, response.TakeProfit);
                                    }
                                }

                                // If mtResponse from server is CLOSE_ALL -> CLOSE_ALL order in metatrader
                                else if (response.Action == "CLOSE_ALL")
                                {
                                    // Print on the screen
                                    Print(Environment.NewLine);
                                    Print("--------- SEND CLOSE ALL TO METATRADER ---------");
                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == _api.ClientId).Name);
                                    Print("Order       : CLOSE ALL");
                                    Print("------------------------------------------------");


                                    // Close all order
                                    _api.CloseAllOrders();
                                }
                            }
                        }
                        else
                        {
                            OnLogEvent(_api.ClientId, 0, new Log()
                            {
                                Type = "ERROR",
                                ErrorType = "error",
                                Time = DateTime.UtcNow,
                                Message = "There is an error with receiving information from the server."
                            });
                        }
                    }
                }

                // Wait a little bit
                await Task.Delay(_appConfig.SleepDelay);
                await ListenToTheServerAsync();
            }
        }


        public double CalculateLotSize(double accountBalance, double riskPercent, double askPrice, double stopLossPrice, double tickValue, double pointSize, double lotStep, double minLotSizeAllowed)
        {
            // Throw exception when negative balance
            if (accountBalance <= 0)
                throw new ArgumentException("Account balance should be greater then 0", "accountBalance");

            // Calculate the initial lot size
            double riskAmount = accountBalance * (riskPercent / 100.0);
            double stopLossPriceInPips = Math.Abs(askPrice - stopLossPrice) / pointSize;
            double initialLotSize = riskAmount / (stopLossPriceInPips * tickValue);

            // Find the nearest multiple of lotStep
            var remainder = initialLotSize % lotStep;
            var adjustedLotSize = remainder == 0 ? initialLotSize : initialLotSize - remainder + (remainder >= lotStep / 2 ? lotStep : 0);

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
                Print("Lot       : " + order.Lots);
                Print("Type      : " + order.Type);
                Print("Magic     : " + order.Magic);
                Print("------------------------------------------------");
            }
        }
    }
}
