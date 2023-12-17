using Microsoft.Extensions.DependencyInjection;
using static JCTG.Client.Helpers;

namespace JCTG.Client
{
    public class Metatrader
    {
        private readonly AppConfig? _appConfig;
        private readonly List<MetatraderApi> _apis;
        private readonly List<MetatraderRequest> _logPairs;

        public Metatrader(AppConfig appConfig)
        {
            // Init APP Config + API
            _appConfig = appConfig;
            _apis = [];
            _logPairs = [];

            // Foreach broker, init the API
            foreach (var api in _appConfig.Brokers)
            {
                // Init API
                var _api = new MetatraderApi(api.MetaTraderDirPath, api.ClientId, appConfig.SleepDelay, appConfig.MaxRetryCommandSeconds, appConfig.LoadOrdersFromFile, appConfig.Verbose);

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

                        // Get order history
                        _api.GetHistoricTrades(30);

                        // Init the events
                        _api.OnOrderEvent += OnOrderEvent;
                        _api.OnLogEvent += OnLogEvent;
                        _api.OnTradeEvent += async (c) =>
                        {
                            await OnTradeEvent(c);
                        };
                        _api.OnTradeDataEvent += OnTradeDataEvent;
                        

                        // Thread Sleep
                        await Task.Delay(1000);
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
                                // Get current UTC time
                                var currentUtcTime = DateTime.Now.AddHours(1);

                                // Get current day of the week
                                var currentDay = currentUtcTime.ToString("dddd");

                                // Do we have custom session times?
                                if (ticker.OverrideSession)
                                {
                                    // Get session
                                    var session = GetSessionForDay(ticker.Sessions, currentDay);

                                    // Do null reference check
                                    if (session != null)
                                    {
                                        // Adjust for sessions that close after midnight
                                        if (session.Close == null || session.Close < session.Open)
                                        {
                                            session.Close = session.Close?.Add(new TimeSpan(24, 0, 0)) ?? new TimeSpan(24, 0, 0);
                                            if (currentUtcTime.TimeOfDay < session.Open)
                                            {
                                                currentUtcTime = currentUtcTime.AddDays(-1);
                                            }
                                        }

                                        // Current UTC time is within the session hours.
                                        var mtRequest = new MetatraderRequest()
                                        {
                                            AccountID = _appConfig.AccountId,
                                            ClientID = _api.ClientId,
                                            TickerInMetatrader = ticker.TickerInMetatrader,
                                            Price = metadataTick.Ask,
                                            Spread = Math.Abs(metadataTick.Ask - metadataTick.Bid),
                                            StrategyType = ticker.StrategyNr,
                                            TickerInTradingview = ticker.TickerInTradingView,
                                        };

                                        if ((session.Open == null || currentUtcTime.TimeOfDay >= session.Open) && (session.Close == null || currentUtcTime.TimeOfDay <= session.Close))
                                        {
                                            // Add to the list
                                            mtRequests.Add(mtRequest);

                                            // Add to the log
                                            if (!_logPairs.Any(f => f.AccountID == mtRequest.AccountID
                                                                        && f.ClientID == mtRequest.ClientID
                                                                        && f.TickerInMetatrader == mtRequest.TickerInMetatrader
                                                                        && f.TickerInTradingview == mtRequest.TickerInTradingview
                                                                        && f.StrategyType == mtRequest.StrategyType))
                                            {
                                                _logPairs.Add(mtRequest);

                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("------------- START LISTENING TO NEW MARKET ----------------");
                                                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == mtRequest.ClientID).Name);
                                                Print("Time      : " + DateTime.UtcNow);
                                                Print("Symbol    : " + mtRequest.TickerInTradingview);
                                                Print("------------------------------------------------");
                                            }
                                        }
                                        else
                                        {
                                            // Add to the log
                                            if (_logPairs.Any(f => f.AccountID == mtRequest.AccountID
                                                                        && f.ClientID == mtRequest.ClientID
                                                                        && f.TickerInMetatrader == mtRequest.TickerInMetatrader
                                                                        && f.TickerInTradingview == mtRequest.TickerInTradingview
                                                                        && f.StrategyType == mtRequest.StrategyType))
                                            {
                                                _logPairs.Remove(_logPairs.First(f => f.AccountID == mtRequest.AccountID
                                                                        && f.ClientID == mtRequest.ClientID
                                                                        && f.TickerInMetatrader == mtRequest.TickerInMetatrader
                                                                        && f.TickerInTradingview == mtRequest.TickerInTradingview
                                                                        && f.StrategyType == mtRequest.StrategyType));

                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("------------- STOP LISTENING TO MARKET ----------------");
                                                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == mtRequest.ClientID).Name);
                                                Print("Time      : " + DateTime.UtcNow);
                                                Print("Symbol    : " + mtRequest.TickerInTradingview);
                                                Print("------------------------------------------------");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Current UTC time is within the session hours.
                                    var mtRequest = new MetatraderRequest()
                                    {
                                        AccountID = _appConfig.AccountId,
                                        ClientID = _api.ClientId,
                                        TickerInMetatrader = ticker.TickerInMetatrader,
                                        Price = metadataTick.Ask,
                                        StrategyType = ticker.StrategyNr,
                                        TickerInTradingview = ticker.TickerInTradingView,
                                    };
                                    mtRequests.Add(mtRequest);

                                    // Add to the log
                                    if (!_logPairs.Any(f => f.AccountID == mtRequest.AccountID
                                                                && f.ClientID == mtRequest.ClientID
                                                                && f.TickerInMetatrader == mtRequest.TickerInMetatrader
                                                                && f.TickerInTradingview == mtRequest.TickerInTradingview
                                                                && f.StrategyType == mtRequest.StrategyType))
                                    {
                                        _logPairs.Add(mtRequest);

                                        // Print on the screen
                                        Print(Environment.NewLine);
                                        Print("------------- START LISTENING TO NEW MARKET ----------------");
                                        Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == mtRequest.ClientID).Name);
                                        Print("Time      : " + DateTime.UtcNow);
                                        Print("Symbol    : " + mtRequest.TickerInTradingview);
                                        Print("------------------------------------------------");
                                    }
                                }
                            }
                        }

                        // Check if the markets are open, and we need to do a server call
                        if (mtRequests.Any())
                        {
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
                                            var lotSize = CalculateLotSize(_api.AccountInfo.Balance, ticker.Risk, metadataTick.Ask, response.StopLoss, metadataTick.TickValue, metadataTick.PointSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize);

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

                                    // If mtResponse from server is BUY -> BUY in metatrader
                                    else if (response.Action == "SELL")
                                    {
                                        // Get the right ticker back from the local database
                                        var ticker = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(response.TickerInMetatrader));

                                        // Get the metadata tick
                                        var metadataTick = _api.MarketData.FirstOrDefault(f => f.Key == response.TickerInMetatrader).Value;

                                        // Do null reference check
                                        if (ticker != null)
                                        {
                                            // Make buy order
                                            var lotSize = CalculateLotSize(_api.AccountInfo.Balance, ticker.Risk, metadataTick.Ask, response.StopLoss, metadataTick.TickValue, metadataTick.PointSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize);

                                            // Print on the screen
                                            Print(Environment.NewLine);
                                            Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                            Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == _api.ClientId).Name);
                                            Print("Ticker      : " + ticker.TickerInMetatrader);
                                            Print("Order       : SELL MARKET ORDER");
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
                                            _api.ExecuteOrder(ticker.TickerInMetatrader, OrderType.Sell, lotSize, 0, response.StopLoss, response.TakeProfit, response.Magic);
                                        }
                                    }

                                    // If mtResponse from server is MODIFYSLTOBE -> MODIFY SL TO BE order in metatrader
                                    else if (response.Action == "MODIFYSLTOBE")
                                    {
                                        // Get the right ticker back from the local database
                                        var ticker = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(response.TickerInMetatrader));

                                        // Get the metadata tick
                                        var metadataTick = _api.MarketData.FirstOrDefault(f => f.Key == response.TickerInMetatrader).Value;

                                        // Do null reference check
                                        if (ticker != null)
                                        {
                                            // Get ticket Id
                                            var ticketId = _api.OpenOrders.FirstOrDefault(f => f.Value.Symbol == response.TickerInMetatrader && f.Value.Magic == response.Magic);

                                            // Null reference check
                                            if (ticketId.Key > 0)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("--------- SEND MODIFY SL TO BE ORDER TO METATRADER ---------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == _api.ClientId).Name);
                                                Print("Ticker      : " + ticker.TickerInMetatrader);
                                                Print("Order       : MODIFY SL TO BE ORDER");
                                                Print("Lot Size    : " + ticketId.Value.Lots);
                                                Print("Price       : " + ticketId.Value.OpenPrice);
                                                Print("Stop Loss   : " + ticketId.Value.OpenPrice);
                                                Print("Take Profit : " + ticketId.Value.TakeProfit);
                                                Print("Magic       : " + ticketId.Value.Magic);
                                                Print("Strategy    : " + ticker.StrategyNr);
                                                Print("Ticket id   : " + ticketId.Key);
                                                Print("------------------------------------------------");

                                                // Modify order
                                                _api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, ticketId.Value.OpenPrice, ticketId.Value.TakeProfit);
                                            }
                                        }
                                    }

                                    // If mtResponse from server is MODIFY SL -> MODIFY SL order in metatrader
                                    else if (response.Action == "MODIFYSL")
                                    {
                                        // Get the right ticker back from the local database
                                        var ticker = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(response.TickerInMetatrader));

                                        // Get the metadata tick
                                        var metadataTick = _api.MarketData.FirstOrDefault(f => f.Key == response.TickerInMetatrader).Value;

                                        // Do null reference check
                                        if (ticker != null)
                                        {
                                            // Get ticket Id
                                            var ticketId = _api.OpenOrders.FirstOrDefault(f => f.Value.Symbol == response.TickerInMetatrader && f.Value.Magic == response.Magic);

                                            // Null reference check
                                            if (ticketId.Key > 0)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("--------- SEND MODIFY SL TO BE ORDER TO METATRADER ---------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == _api.ClientId).Name);
                                                Print("Ticker      : " + ticker.TickerInMetatrader);
                                                Print("Order       : MODIFY SL TO BE ORDER");
                                                Print("Lot Size    : " + ticketId.Value.Lots);
                                                Print("Price       : " + ticketId.Value.OpenPrice);
                                                Print("Stop Loss   : " + response.StopLoss);
                                                Print("Take Profit : " + ticketId.Value.TakeProfit);
                                                Print("Magic       : " + ticketId.Value.Magic);
                                                Print("Strategy    : " + ticker.StrategyNr);
                                                Print("Ticket id   : " + ticketId.Key);
                                                Print("------------------------------------------------");

                                                // Modify order
                                                _api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, response.StopLoss, ticketId.Value.TakeProfit);
                                            }
                                        }
                                    }

                                    // If mtResponse from server is CLOSE_ALL -> CLOSE_ALL order in metatrader
                                    else if (response.Action == "CLOSE")
                                    {
                                        // Get the right ticker back from the local database
                                        var ticker = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(response.TickerInMetatrader));

                                        // Do null reference check
                                        if (ticker != null)
                                        {
                                            // Get ticket Id
                                            var ticketId = _api.OpenOrders.FirstOrDefault(f => f.Value.Symbol != null && f.Value.Symbol.Equals(response.TickerInMetatrader) && f.Value.Magic == response.Magic);

                                            // Null reference check
                                            if (ticketId.Key > 0)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("--------- SEND CLOSE ORDER TO METATRADER ---------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == _api.ClientId).Name);
                                                Print("Ticker      : " + ticker.TickerInMetatrader);
                                                Print("Order       : CLOSE ORDER");
                                                Print("Magic       : " + response.Magic);
                                                Print("Ticket id   : " + ticketId.Key);
                                                Print("------------------------------------------------");

                                                // Modify order
                                                _api.CloseOrder(ticketId.Key);
                                            }
                                        }
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
                }

                // Wait a little bit
                await Task.Delay(_appConfig.SleepDelay);
                await ListenToTheServerAsync();
            }
        }


        public double CalculateLotSize(double accountBalance, double riskPercent, double askPrice, double stopLossPrice, double tickValue, double pointSize, double lotStep, double minLotSizeAllowed, double maxLotSizeAllowed)
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

            // Ensure the lot size is not less than the minimum allowed and not more than the maximum allowed
            if (adjustedLotSize < minLotSizeAllowed)
                return minLotSizeAllowed;
            else if (adjustedLotSize > maxLotSizeAllowed)
                return maxLotSizeAllowed;
            else
                return adjustedLotSize;
        }



        private void OnLogEvent(long clientId, long id, Log log)
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

        private void OnOrderEvent(long clientId, Order order)
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

        private async Task OnTradeEvent(long clientId)
        {
            // Do the API CAll
            var backend = Program.Service?.GetService<AzureFunctionApiClient>();

            // Do null reference check
            if (_appConfig != null && backend != null)
            {
                // Get the api from the collection
                var api = _apis.FirstOrDefault(f => f.ClientId == clientId);

                // Do null reference check
                if (api != null)
                {
                    // Prepare trade journals
                    var tradeJournals = new List<TradeJournalRequest>();

                    // Loop through the trades
                    foreach (var trade in api.Trades)
                    {
                        // Get broker from the app config
                        var broker = _appConfig.Brokers.FirstOrDefault(f => f.ClientId == clientId);

                        // Do null reference check
                        if (broker != null && trade.Value.OpenPrice > 0 && trade.Value.ClosePrice > 0)
                        {
                            // Get the right timeframe from the app config / borker
                            var pair = broker.Pairs.FirstOrDefault(f => f.TickerInMetatrader == trade.Value.Symbol);

                            // Do null reference check
                            if (pair != null)
                            {
                                api.GetHistoricData(trade.Value.Symbol, pair.Timeframe, trade.Value.OpenTime, trade.Value.CloseTime);

                                // Add 
                                tradeJournals.Add(new TradeJournalRequest()
                                {
                                    AccountID = _appConfig.AccountId,
                                    ClientID = broker.ClientId,
                                    ClosePrice = trade.Value.ClosePrice,
                                    CloseTime = trade.Value.CloseTime.LocalDateTime,
                                    Comment = trade.Value.Comment,
                                    Commission = trade.Value.Commission,
                                    Lots = trade.Value.Lots,
                                    Magic = trade.Value.Magic,
                                    OpenPrice = trade.Value.OpenPrice,
                                    OpenTime = trade.Value.OpenTime.LocalDateTime,
                                    Pnl = trade.Value.Pnl,
                                    SL = trade.Value.SL,
                                    Swap = trade.Value.Swap,
                                    Symbol = trade.Value.Symbol,
                                    TP = trade.Value.TP,
                                    Type = trade.Value.Type,
                                    StrategyType = pair.StrategyNr,
                                    Timeframe = pair.Timeframe,
                                });
                            }
                        }
                    }

                    // Send to the server
                    await backend.SendTradeJournalAsync(tradeJournals);
                }
            }
        }

        private void OnTradeDataEvent(long clientId, string symbol, string timeFrame, Newtonsoft.Json.Linq.JObject data)
        {

        }


        private SessionTimes? GetSessionForDay(Dictionary<string, SessionTimes> sessions, string day)
        {
            // Null reference check
            if (sessions == null || !sessions.ContainsKey(day) || sessions[day] == null)
            {
                return null;
            }

            // Get session
            var session = sessions[day];

            // Null reference check
            if (session.Open == null)
            {
                // Check previous day for closing time
                var previousDay = GetPreviousDay(day);
                if (sessions.ContainsKey(previousDay) && sessions[previousDay] != null)
                {
                    session.Open = sessions[previousDay].Close;
                }
            }

            // Null reference check
            if (session.Close == null)
            {
                // Check next day for opening time
                var nextDay = GetNextDay(day);
                if (sessions.ContainsKey(nextDay) && sessions[nextDay] != null)
                {
                    session.Close = sessions[nextDay].Open;
                }
            }

            return session;
        }

        private string GetPreviousDay(string day)
        {
            var days = new List<string> { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            int index = days.IndexOf(day);
            return days[(index - 1 + days.Count) % days.Count];
        }

        private string GetNextDay(string day)
        {
            var days = new List<string> { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            int index = days.IndexOf(day);
            return days[(index + 1) % days.Count];
        }
    }
}
