using JCTG.Entity;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Websocket.Client;
using static JCTG.Client.Helpers;

namespace JCTG.Client
{
    public class Metatrader
    {

        private readonly AppConfig? _appConfig;
        private readonly List<MetatraderApi> _apis;
        private readonly List<MetatraderRequest> _logPairs;
        object myLock = new object();

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
            if (_appConfig != null && _apis != null)
            {
                // Get web socket client
                var client = Program.Service?.GetService<WebsocketClient>();

                // Do null reference check
                if (client != null)
                {
                    // Disable the auto disconnect and reconnect because the sample would like the client to stay online even no data comes in
                    client.ReconnectTimeout = null;

                    // Enable the message receive
                    client.MessageReceived.Subscribe(msg =>
                    {
                        // Do null reference check
                        if (msg != null && msg.Text != null)
                        {
                            // Get response
                            var response = JsonConvert.DeserializeObject<MetatraderMessage>(msg.Text);

                            // Do null reference check
                            if (response != null)
                            {
                                // Iterate through the api's
                                foreach (var api in _apis)
                                {
                                    // Get the right ticker back from the local database
                                    var ticker = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(response.Instrument) && f.StrategyNr == response.StrategyType);

                                    // If this broker is listening to this signal and the account size is greater then zero
                                    if (ticker != null && api.AccountInfo != null && api.AccountInfo.Balance > 0)
                                    {
                                        // Get the metadata tick
                                        var metadataTick = api.MarketData.FirstOrDefault(f => f.Key == ticker.TickerInMetatrader).Value;

                                        // Calculate spread
                                        var spread = Math.Round(Math.Abs(metadataTick.Ask - metadataTick.Bid), 4, MidpointRounding.AwayFromZero);

                                        // If is it a buy
                                        if (response.OrderType == "BUY")
                                        {
                                            // Calculate the lot size
                                            var lotSize = CalculateLotSize(api.ClientId, api.AccountInfo.Balance, ticker.Risk, metadataTick.Ask, response.StopLoss, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, metadataTick.Ask - metadataTick.Bid);

                                            // do 0.0 check
                                            if (lotSize > 0.0)
                                            {
                                                // Calculate SL Price
                                                var slPrice = CalculateSLForLong(
                                                            mtPrice: metadataTick.Ask,
                                                            mtATR: GetAtr(metadataTick.ATR5M, metadataTick.ATR15M, metadataTick.ATR1H, metadataTick.ATRD, response.StrategyType),
                                                            mtSpread: spread,
                                                            mtTickSize: metadataTick.TickSize,
                                                            signalPrice: response.Price,
                                                            signalSL: response.StopLoss,
                                                            signalATR: GetAtr(response.ATR5M, response.ATR15M, response.ATR1H, response.ATRD, response.StrategyType)
                                                            );

                                                // Calculate TP Price
                                                var tpPrice = CalculateTPForLong(
                                                            mtPrice: metadataTick.Ask,
                                                            mtATR: GetAtr(metadataTick.ATR5M, metadataTick.ATR15M, metadataTick.ATR1H, metadataTick.ATRD, response.StrategyType),
                                                            mtTickSize: metadataTick.TickSize,
                                                            signalPrice: response.Price,
                                                            signalTP: response.TakeProfit,
                                                            signalATR: GetAtr(response.ATR5M, response.ATR15M, response.ATR1H, response.ATRD, response.StrategyType)
                                                            );

                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                Print("Ticker      : " + ticker.TickerInMetatrader);
                                                Print("Order       : BUY MARKET ORDER");
                                                Print("Lot Size    : " + lotSize);
                                                Print("Ask price   : " + metadataTick.Ask);
                                                Print("Stop Loss   : " + slPrice);
                                                Print("Take Profit : " + tpPrice);
                                                Print("Magic       : " + response.Magic);
                                                Print("Strategy    : " + ticker.StrategyNr);
                                                Print("------------------------------------------------");



                                                // Open order
                                                api.ExecuteOrder(ticker.TickerInMetatrader, OrderType.Buy, lotSize, 0, slPrice, tpPrice, (int)response.Magic);
                                            }
                                        }

                                        // If is it a sell
                                        else if (response.OrderType == "SELL")
                                        {
                                            // Calculate the lot size
                                            var lotSize = CalculateLotSize(api.ClientId, api.AccountInfo.Balance, ticker.Risk, metadataTick.Ask, response.StopLoss, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, metadataTick.Ask - metadataTick.Bid);

                                            // do 0.0 check
                                            if (lotSize > 0.0)
                                            {
                                                // Calculate SL Price
                                                var slPrice = CalculateSLForLong(
                                                            mtPrice: metadataTick.Ask,
                                                            mtATR: GetAtr(metadataTick.ATR5M, metadataTick.ATR15M, metadataTick.ATR1H, metadataTick.ATRD, response.StrategyType),
                                                            mtSpread: spread,
                                                            mtTickSize: metadataTick.TickSize,
                                                            signalPrice: response.Price,
                                                            signalSL: response.StopLoss,
                                                            signalATR: GetAtr(response.ATR5M, response.ATR15M, response.ATR1H, response.ATRD, response.StrategyType)
                                                            );

                                                // Calculate TP Price
                                                var tpPrice = CalculateTPForLong(
                                                            mtPrice: metadataTick.Ask,
                                                            mtATR: GetAtr(metadataTick.ATR5M, metadataTick.ATR15M, metadataTick.ATR1H, metadataTick.ATRD, response.StrategyType),
                                                            mtTickSize: metadataTick.TickSize,
                                                            signalPrice: response.Price,
                                                            signalTP: response.TakeProfit,
                                                            signalATR: GetAtr(response.ATR5M, response.ATR15M, response.ATR1H, response.ATRD, response.StrategyType)
                                                            );

                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
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
                                                api.ExecuteOrder(ticker.TickerInMetatrader, OrderType.Sell, lotSize, 0, slPrice, tpPrice, (int)response.Magic);
                                            }
                                        }

                                        // Modify Stop Loss to Break Even
                                        else if (response.OrderType == "MODIFYSLTOBE" && response.TicketId.HasValue)
                                        {
                                            // Check if the ticket still exist as open order
                                            var ticketId = api.OpenOrders.FirstOrDefault(f => f.Key == response.TicketId.Value);

                                            // Null reference check
                                            if (ticketId.Key > 0 && ticketId.Value.Type != null)
                                            {
                                                // If type is SELL, the SL should be set as BE minus Spread
                                                var sl = ticketId.Value.OpenPrice;
                                                if (ticketId.Value.Type.Equals("SELL", StringComparison.CurrentCultureIgnoreCase))
                                                {
                                                    sl = ticketId.Value.OpenPrice + spread;
                                                }

                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("--------- SEND MODIFY SL TO BE ORDER TO METATRADER ---------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
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
                                                api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, sl, ticketId.Value.TakeProfit);
                                            }
                                        }

                                        // Modify Stop Loss
                                        else if (response.OrderType == "MODIFYSL" && response.TicketId.HasValue)
                                        {
                                            // Check if the ticket still exist as open order
                                            var ticketId = api.OpenOrders.FirstOrDefault(f => f.Key == response.TicketId.Value);

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
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
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
                                                api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, response.StopLoss, ticketId.Value.TakeProfit);
                                            }
                                        }

                                        // Close trade
                                        else if (response.OrderType == "CLOSE" && response.TicketId.HasValue)
                                        {

                                            // Check if the ticket still exist as open order
                                            var ticketId = api.OpenOrders.FirstOrDefault(f => f.Key == response.TicketId.Value);

                                            // Null reference check
                                            if (ticketId.Key > 0)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("--------- SEND CLOSE ORDER TO METATRADER ---------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                Print("Ticker      : " + ticker.TickerInMetatrader);
                                                Print("Order       : CLOSE ORDER");
                                                Print("Magic       : " + response.Magic);
                                                Print("Ticket id   : " + ticketId.Key);
                                                Print("------------------------------------------------");

                                                // Modify order
                                                api.CloseOrder(ticketId.Key);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    });

                    // Start the web socket
                    await client.Start();
                }
            }
        }

        public double CalculateLotSize(long clientId, double accountBalance, double riskPercent, double openPrice, double stopLossPrice, double tickValue, double tickSize, double lotStep, double minLotSizeAllowed, double maxLotSizeAllowed, double spread)
        {
            // Throw exception when negative balance
            if (accountBalance <= 0)
                return 0.0;

            // Calculate the initial lot size
            double riskAmount = accountBalance * (riskPercent / 100.0);
            double stopLossDistance = Math.Abs(openPrice - stopLossPrice);
            double stopLossDistanceInTicks = stopLossDistance / tickSize;
            double lotSize = riskAmount / (stopLossDistanceInTicks * tickValue);


            var remainder = lotSize % lotStep;
            var adjustedLotSize = remainder == 0 ? lotSize : lotSize - remainder + (remainder >= lotStep / 2 ? lotStep : 0);

            // Round to 2 decimal places
            adjustedLotSize = Math.Round(adjustedLotSize, 2);

            // Send log to the server
            if (clientId > 0 && _appConfig != null)
            {
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = string.Format($"AccountBalance={accountBalance},RiskPercent={riskPercent},RiskAmount={riskAmount},SLInTicks={stopLossDistanceInTicks},Spread={spread},TickValue={tickValue},TickSize={tickSize},LotSize={lotSize},AdjustedLotSize={adjustedLotSize}"),
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


        /// <summary>
        /// Calculate the stop loss price for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK price</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="mtSpread">Spread value</param>
        /// <param name="mtTickSize">Tick size</param>
        /// <param name="signalPrice">Signal ENTRY price</param>
        /// <param name="signalSL">Signal SL price</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Stop loss price</returns>
        public static double CalculateSLForLong(double mtPrice, double mtATR, double mtSpread, double mtTickSize, double signalPrice, double signalSL, double signalATR)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            var atrMultiplier = mtATR > 0 && signalATR > 0 && mtATR > signalATR ? 1.1 : 1.0;

            // Calculate SL price using MetaTrader price minus risk to take
            var slPrice = mtPrice - ((signalPrice - signalSL) * atrMultiplier);

            // Calculate the number of ticks
            var numberOfTicks = Math.Floor(slPrice / mtTickSize);

            // Multiply back to get the rounded value
            slPrice = numberOfTicks * mtTickSize;

            // Check if SL is not higher then entry price
            if (slPrice > mtPrice)
                slPrice = mtPrice;

            // Return SL Price minus spread
            return slPrice - (2 * mtSpread);
        }


        /// <summary>
        /// Calculate the stop loss price for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK price</param>
        /// <param name="mtATR">MetaTrader ATR5M</param>
        /// <param name="mtSpread">The spread value</param>
        /// <param name="signalPrice">Signal ENTRY price</param>
        /// <param name="signalSL">Signal SL price</param>
        /// <param name="signalATR">Signal ATR5M</param>
        /// <returns>Stop loss price</returns>
        public static double CalculateSLForShort(double mtPrice, double mtATR, double mtSpread, double mtTickSize, double signalPrice, double signalSL, double signalATR)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            var atrMultiplier = mtATR > 0 && signalATR > 0 && mtATR > signalATR ? 1.1 : 1.0;

            // Calculate SL price using MetaTrader price minus risk to take
            var slPrice = mtPrice + ((signalSL - signalPrice) * atrMultiplier);

            // Calculate the number of ticks
            var numberOfTicks = Math.Ceiling(slPrice / mtTickSize);

            // Multiply back to get the rounded value
            slPrice = numberOfTicks * mtTickSize;

            // Check if SL is not lower then entry price
            if (slPrice < mtPrice)
                slPrice = mtPrice;

            // Return SL Price plus spread
            return slPrice + (2 * mtSpread);
        }

        /// <summary>
        /// Calculate the take profit price for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK price</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="mtTickSize">Tick size</param>
        /// <param name="signalPrice">Signal ENTRY price</param>
        /// <param name="signalTP">Signal TP price</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Take profit price</returns>
        public static double CalculateTPForLong(double mtPrice, double mtATR, double mtTickSize, double signalPrice, double signalTP, double signalATR)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            var atrMultiplier = mtATR > 0 && signalATR > 0 && mtATR > signalATR ? 1.1 : 1.0;

            // Calculate TP price using MetaTrader price minus risk to take
            var tpPrice = mtPrice + ((signalTP - signalPrice) * atrMultiplier);

            // Calculate the number of ticks
            var numberOfTicks = Math.Floor(tpPrice / mtTickSize);

            // Multiply back to get the rounded value
            tpPrice = numberOfTicks * mtTickSize;

            // Return SL Price minus spread
            return tpPrice;
        }

        /// <summary>
        /// Calculate the take profit price for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK price</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="signalPrice">Signal ENTRY price</param>
        /// <param name="signalTP">Signal TP price</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Take profit price</returns>
        public static double CalculateTPForShort(double mtPrice, double mtATR,  double mtTickSize, double signalPrice, double signalTP, double signalATR)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            var atrMultiplier = mtATR > 0 && signalATR > 0 && mtATR > signalATR ? 1.1 : 1.0;

            // Calculate TP price using MetaTrader price minus risk to take
            var tpPrice = mtPrice - ((signalPrice - signalTP) * atrMultiplier);

            // Calculate the number of ticks
            var numberOfTicks = Math.Ceiling(tpPrice / mtTickSize);

            // Multiply back to get the rounded value
            tpPrice = numberOfTicks * mtTickSize;

            // Return SL Price plus spread
            return tpPrice;
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
                    Print("Object   : " + log.Message);
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

                // Send log to the server
                new AzureFunctionApiClient().SendLog(new LogRequest()
                {
                    AccountID = _appConfig.AccountId,
                    ClientID = clientId,
                    Message = string.Format($"Symbol={order.Symbol},Ticket={ticketId},Lots={order.Lots},Type={order.Type},Magic={order.Magic},Price={order.OpenPrice},TP={order.TakeProfit},SL={order.StopLoss}"),
                    Type = "MT - CREATE ORDER",
                });

                // Send to tradingjournal
                SendOrderToBackend(clientId, ticketId, order, false);
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

                // Send to tradingjournal
                SendOrderToBackend(clientId, ticketId, order, false);
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

                // Send to tradingjournal
                SendOrderToBackend(clientId, ticketId, order, true);
            }
        }

        private void SendOrderToBackend(long clientId, long ticketId, Order order, bool isTradeClosed)
        {
            if (_appConfig != null && _apis != null && _apis.Count(f => f.ClientId == clientId && f.MarketData != null) == 1)
            {
                // Add to the trading journal
                var marketdata = _apis.First(f => f.ClientId == clientId).MarketData.FirstOrDefault(f => f.Key == order.Symbol);

                // Get setup from appconfig
                var pair = _appConfig.Brokers.Where(f => f.ClientId == clientId).SelectMany(f => f.Pairs).Where(f => f.TickerInMetatrader == order.Symbol).FirstOrDefault();

                // Do null reference check
                if (pair != null && !string.IsNullOrEmpty(marketdata.Key))
                {
                    // Init object
                    new AzureFunctionApiClient().SendTradeJournal(new TradeJournalRequest()
                    {
                        AccountID = _appConfig.AccountId,
                        ClientID = clientId,
                        Comment = order.Comment,
                        Commission = order.Commission,
                        CurrentPrice = marketdata.Value.Ask,
                        Lots = order.Lots,
                        Magic = order.Magic,
                        OpenPrice = order.OpenPrice,
                        OpenTime = order.OpenTime,
                        Pnl = order.Pnl,
                        SL = order.StopLoss,
                        StrategyType = pair.StrategyNr,
                        Spread = Math.Round(Math.Abs(marketdata.Value.Bid - marketdata.Value.Ask), 4, MidpointRounding.AwayFromZero),
                        Swap = order.Swap,
                        Symbol = order.Symbol ?? "NONE",
                        TicketId = ticketId,
                        Timeframe = pair.Timeframe,
                        TP = order.TakeProfit,
                        Type = order.Type != null ? order.Type.ToUpper() : "NONE",
                        Risk = pair.Risk,
                        IsTradeClosed = isTradeClosed,
                    });
                }
            }
        }

        private double GetAtr(double atr5M, double atr15M, double atr1H, double atrD, StrategyType type)
        {
            if (type == StrategyType.Strategy1) // SWING - STOCKS
                return atr1H; // 4H
            else if (type == StrategyType.Strategy2) // SWING - INDICES
                return atr1H;
            else if (type == StrategyType.Strategy3)
                return atr5M;
            else if (type == StrategyType.Strategy4) // DAYTRADE - INDICES
                return atr5M;
            else if (type == StrategyType.Strategy5) // DAYTRADE - CRYPTO
                return atr5M;
            return atr15M;
        }
    }
}
