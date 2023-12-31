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

                        // Init the events
                        _api.OnOrderCreateEvent += OnOrderCreateEvent;
                        _api.OnOrderUpdateEvent += OnOrderUpdateEvent;
                        _api.OnOrderRemoveEvent += OnOrderRemoveEvent;
                        _api.OnLogEvent += OnLogEvent;

                        // Thread Sleep
                        await Task.Delay(1000);
                    }
                }
            }
        }

        public async Task ListenToTheServerAsync()
        {
            // Do null reference checks
            if(_appConfig != null)
            {
                // Loop through the api
                Parallel.ForEach(_apis, async _api =>
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

                                // Do we have custom session times?
                                if (ticker.OverrideSession)
                                {
                                    // Init request object
                                    var mtRequest = new MetatraderRequest()
                                    {
                                        AccountID = _appConfig.AccountId,
                                        ClientID = _api.ClientId,
                                        TickerInMetatrader = ticker.TickerInMetatrader,
                                        Ask = metadataTick.Ask,
                                        Bid = metadataTick.Bid,
                                        TickSize = metadataTick.TickSize,
                                        StrategyType = ticker.StrategyNr,
                                        TickerInTradingview = ticker.TickerInTradingView,
                                        TickerInFMP = ticker.TickerInFMP,
                                        Atr5M = metadataTick.ATR5M,
                                        Atr15M = metadataTick.ATR15M,
                                        Atr1H = metadataTick.ATR1H,
                                        AtrD = metadataTick.ATRD,
                                    };

                                    // Is current time within the sessions of the app config ?
                                    if (IsCurrentUTCTimeInSession(ticker.Sessions, currentUtcTime))
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

                                            // Send to the server
                                            new AzureFunctionApiClient().SendLog(new LogRequest()
                                            {
                                                AccountID = _appConfig.AccountId,
                                                ClientID = mtRequest.ClientID,
                                                Message = string.Format($"Symbol={mtRequest.TickerInTradingview}"),
                                                Type = "CONSOLE - START LISTENING TO MARKET",
                                            });
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

                                            // Send to the server
                                            new AzureFunctionApiClient().SendLog(new LogRequest()
                                            {
                                                AccountID = _appConfig.AccountId,
                                                ClientID = mtRequest.ClientID,
                                                Message = string.Format($"Symbol={mtRequest.TickerInTradingview}"),
                                                Type = "CONSOLE - STOP LISTENING TO MARKET",
                                            });
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
                                        Ask = metadataTick.Ask,
                                        StrategyType = ticker.StrategyNr,
                                        TickerInTradingview = ticker.TickerInTradingView,
                                        TickerInFMP = ticker.TickerInFMP,
                                        Atr5M = metadataTick.ATR5M,
                                        Atr15M = metadataTick.ATR15M,
                                        Atr1H = metadataTick.ATR1H,
                                        AtrD = metadataTick.ATRD,
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

                                        // Send to the server
                                        new AzureFunctionApiClient().SendLog(new LogRequest()
                                        {
                                            AccountID = _appConfig.AccountId,
                                            ClientID = mtRequest.ClientID,
                                            Message = string.Format($"Symbol={mtRequest.TickerInTradingview}"),
                                            Type = "CONSOLE - START LISTENING TO MARKET",
                                        });
                                    }
                                }
                            }
                        }

                        // Check if the markets are open, and we need to do a server call
                        if (mtRequests.Count != 0)
                        {
                            // Send the information to the backend
                            var mtResponse = await new AzureFunctionApiClient().GetMetatraderResponseAsync(mtRequests);

                            // Do null reference check
                            if (mtResponse != null && _api.OpenOrders != null && mtResponse.Count == mtRequests.Count)
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
                                            var lotSize = CalculateLotSize(_api.ClientId, _api.AccountInfo.Balance, ticker.Risk, metadataTick.Ask, response.StopLoss, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, metadataTick.Ask - metadataTick.Bid);

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
                                            Print("Point size  : " + metadataTick.TickSize);
                                            Print("Lot step    : " + metadataTick.LotStep);
                                            Print("Magic       : " + response.Magic);
                                            Print("Strategy    : " + ticker.StrategyNr);
                                            Print("------------------------------------------------");


                                            // Open order
                                            _api.ExecuteOrder(ticker.TickerInMetatrader, OrderType.Buy, lotSize, 0, response.StopLoss, response.TakeProfit, (int)response.Magic);
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
                                            var lotSize = CalculateLotSize(_api.ClientId, _api.AccountInfo.Balance, ticker.Risk, metadataTick.Ask, response.StopLoss, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, metadataTick.Ask - metadataTick.Bid);

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
                                            Print("Point size  : " + metadataTick.TickSize);
                                            Print("Lot step    : " + metadataTick.LotStep);
                                            Print("Magic       : " + response.Magic);
                                            Print("Strategy    : " + ticker.StrategyNr);
                                            Print("------------------------------------------------");


                                            // Open order
                                            _api.ExecuteOrder(ticker.TickerInMetatrader, OrderType.Sell, lotSize, 0, response.StopLoss, response.TakeProfit, (int)response.Magic);
                                        }
                                    }

                                    // If mtResponse from server is MODIFYSLTOBE -> MODIFY SL TO BE order in metatrader
                                    else if (response.Action == "MODIFYSLTOBE")
                                    {
                                        // Get the right ticker back from the local database
                                        var ticker = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(response.TickerInMetatrader));

                                        // Do null reference check
                                        if (ticker != null && response.TicketId.HasValue)
                                        {
                                            // Check if the ticket still exist as open order
                                            var ticketId = _api.OpenOrders.FirstOrDefault(f => f.Key == response.TicketId.Value);

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
                                                Print("Ask       : " + ticketId.Value.OpenPrice);
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

                                        // Do null reference check
                                        if (ticker != null && response.TicketId.HasValue)
                                        {
                                            // Check if the ticket still exist as open order
                                            var ticketId = _api.OpenOrders.FirstOrDefault(f => f.Key == response.TicketId.Value);

                                            // Null reference check
                                            if (ticketId.Key > 0)
                                            {
                                                // If SL is 
                                                if (ticketId.Value.Type?.ToUpper() == "BUY" && response.StopLoss > ticketId.Value.OpenPrice)
                                                    response.StopLoss = ticketId.Value.OpenPrice;
                                                else if (ticketId.Value.Type?.ToUpper() == "SELL" && response.StopLoss < ticketId.Value.OpenPrice)
                                                    response.StopLoss = ticketId.Value.OpenPrice;

                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("--------- SEND MODIFY SL TO BE ORDER TO METATRADER ---------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == _api.ClientId).Name);
                                                Print("Ticker      : " + ticker.TickerInMetatrader);
                                                Print("Order       : MODIFY SL TO BE ORDER");
                                                Print("Lot Size    : " + ticketId.Value.Lots);
                                                Print("Ask       : " + ticketId.Value.OpenPrice);
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
                                        if (ticker != null && response.TicketId.HasValue)
                                        {
                                            // Check if the ticket still exist as open order
                                            var ticketId = _api.OpenOrders.FirstOrDefault(f => f.Key == response.TicketId.Value);

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

                    // Do null reference checks
                    if (_api != null && _api.OpenOrders != null && _api.MarketData != null)
                    {
                        // Init request to Azure Function
                        var tjRequests = new List<TradeJournalRequest>();

                        // Loop through each open order
                        foreach (var order in new Dictionary<long, Order>(_api.OpenOrders))
                        {
                            // Get the market data
                            var marketdata = _api.MarketData.FirstOrDefault(f => f.Key == order.Value.Symbol);

                            // Get setup from appconfig
                            var pair = _appConfig.Brokers.Where(f => f.ClientId == _api.ClientId).SelectMany(f => f.Pairs).Where(f => f.TickerInMetatrader == order.Value.Symbol).FirstOrDefault();

                            // Do null reference check
                            if(pair != null) 
                            {
                                // Init object
                                tjRequests.Add(new TradeJournalRequest()
                                {
                                    AccountID = _appConfig.AccountId,
                                    ClientID = _api.ClientId,
                                    Comment = order.Value.Comment,
                                    Commission = order.Value.Commission,
                                    CurrentPrice = order.Value.Type?.ToUpper() == "SELL" ? marketdata.Value.Bid : marketdata.Value.Ask,
                                    Lots = order.Value.Lots,
                                    Magic = order.Value.Magic,
                                    OpenPrice = order.Value.OpenPrice,
                                    OpenTime = order.Value.OpenTime,
                                    Pnl = order.Value.Pnl,
                                    SL = order.Value.StopLoss,
                                    StrategyType = pair.StrategyNr,
                                    Spread = Math.Round(Math.Abs(marketdata.Value.Bid - marketdata.Value.Ask), 4, MidpointRounding.AwayFromZero),
                                    Swap = order.Value.Swap,
                                    Symbol = order.Value.Symbol != null ? order.Value.Symbol : "NONE",
                                    TicketId = order.Key,
                                    Timeframe = pair.Timeframe,
                                    TP = order.Value.TakeProfit,
                                    Type = order.Value.Type != null ? order.Value.Type.ToUpper() : "NONE",
                                    Risk = pair.Risk,
                                });
                            }
                        }

                        // If there any open orders, send them to the backend
                        if (tjRequests.Count != 0)
                            new AzureFunctionApiClient().SendTradeJournals(tjRequests);
                    }
                });

                // Wait a little bit
                await Task.Delay(_appConfig.SleepDelay);
                await ListenToTheServerAsync();
            }
        }

        public double CalculateLotSize(long clientId, double accountBalance, double riskPercent, double openPrice, double stopLossPrice, double tickValue, double pipSize, double lotStep, double minLotSizeAllowed, double maxLotSizeAllowed, double spread)
        {
            // Throw exception when negative balance
            if (accountBalance <= 0)
                throw new ArgumentException("Account balance should be greater then 0", "accountBalance");

            // Calculate the initial lot size
            double riskAmount = accountBalance * (riskPercent / 100.0);
            double stopLossDistance = Math.Abs(openPrice - stopLossPrice);
            double stopLossDistanceInPips = stopLossDistance / pipSize;
            double totalStopLossPips = stopLossDistanceInPips + Math.Abs(spread) / pipSize;
            double lotSize = riskAmount / (totalStopLossPips * tickValue);

            // Code fom MQL4 : LotSize = MathRound(LotSize / MarketInfo(Symbol(), MODE_LOTSTEP)) * MarketInfo(Symbol(), MODE_LOTSTEP);
            // C sharp = Math.Round(lotSize / lotStep) * lotStep
            var remainder = lotSize % lotStep;
            var adjustedLotSize = remainder == 0 ? lotSize : lotSize - remainder + (remainder >= lotStep / 2 ? lotStep : 0);

            // Round to 2 decimal places
            adjustedLotSize = Math.Round(adjustedLotSize, 2);

            // Send log to the server
            if(clientId > 0 && _appConfig != null)
            {
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = string.Format($"AccountBalance={accountBalance},RiskPercent={riskPercent},RiskAmount={riskAmount},SLInPips={totalStopLossPips},Spread={spread},TickValue={tickValue},LotSize={lotSize},PipSize={pipSize},AdjustedLotSize={adjustedLotSize}"),
                    Type = "MT - LOT SIZE",
                });
            }

            // Ensure the lot size is not less than the minimum allowed and not more than the maximum allowed
            if (adjustedLotSize < minLotSizeAllowed)
                return minLotSizeAllowed;
            else if (adjustedLotSize > maxLotSizeAllowed)
                return maxLotSizeAllowed;
            else
                return adjustedLotSize;
        }

        public bool IsCurrentUTCTimeInSession(Dictionary<string, SessionTimes> sessions, DateTime utcNow)
        {
            // Get the current day of the week
            string dayOfWeek = utcNow.DayOfWeek.ToString();

            // Check if the day of the week is in the config file + the day of the week in the config file can not be null
            if (sessions.ContainsKey(dayOfWeek) && sessions[dayOfWeek] != null)
            {
                // Get current session
                var sessionToday = sessions[dayOfWeek];

                // Get open and close time
                var openTime = sessionToday.Open ?? TimeSpan.Zero;
                var closeTime = sessionToday.Close ?? new TimeSpan(23, 59, 59);

                // Get the datetime based on the open or close settings in the app config
                var openDateTime = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day) + openTime;
                var closeDateTime = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day) + closeTime;

                // Handling case where session ends on the next day
                if (closeTime < openTime)
                {
                    openDateTime = openDateTime.AddDays(-1);
                }

                // Compare
                if (utcNow >= openDateTime && utcNow <= closeDateTime)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            // Always return false
            return false;
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


                // Send to the server
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = log.Message,
                    ErrorType = log.ErrorType,
                    Type = string.Format($"MT - {log.Type}"),
                });
            }
        }

        private void OnOrderCreateEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                Print(Environment.NewLine);
                Print("-------------- CREATE ORDER ---------------------");
                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == clientId).Name);
                Print("Time      : " + DateTime.UtcNow);
                Print("Symbol    : " + order.Symbol);
                Print("Ticket    : " + ticketId);
                Print("Lot       : " + order.Lots);
                Print("Type      : " + order.Type);
                Print("Magic     : " + order.Magic);
                Print("------------------------------------------------");

                // Send to the server
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss}"),
                    Type = "MT - CREATE ORDER",
                });
            }
        }

        private void OnOrderUpdateEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                Print(Environment.NewLine);
                Print("-------------- UPDATE ORDER ---------------------");
                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == clientId).Name);
                Print("Time      : " + DateTime.UtcNow);
                Print("Symbol    : " + order.Symbol);
                Print("Ticket    : " + ticketId);
                Print("Lot       : " + order.Lots);
                Print("SL        : " + order.StopLoss);
                Print("TP        : " + order.TakeProfit);
                Print("Magic     : " + order.Magic);
                Print("------------------------------------------------");

                // Send to the server
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss}"),
                    Type = "MT - UDPATE ORDER",
                });
            }
        }

        private void OnOrderRemoveEvent(long clientId, long ticketId, Order order)
        {
            // Do null reference check
            if (_appConfig != null)
            {
                // Print on the screen
                Print(Environment.NewLine);
                Print("-------------- CLOSED ORDER ---------------------");
                Print("Broker    : " + _appConfig.Brokers.First(f => f.ClientId == clientId).Name);
                Print("Time      : " + DateTime.UtcNow);
                Print("Symbol    : " + order.Symbol);
                Print("Ticket    : " + ticketId);
                Print("Lot       : " + order.Lots);
                Print("SL        : " + order.StopLoss);
                Print("TP        : " + order.TakeProfit);
                Print("Magic     : " + order.Magic);
                Print("------------------------------------------------");

                // Send to the server
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss}"),
                    Type = "MT - CLOSE ORDER",
                });
            }
        }
    }
}
