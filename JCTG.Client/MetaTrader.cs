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

        public Metatrader(AppConfig appConfig)
        {
            // Init APP Config + API
            _appConfig = appConfig;
            _apis = [];

            // Foreach broker, init the API
            foreach (var api in _appConfig.Brokers)
            {
                // Init API
                var _api = new MetatraderApi(api.MetaTraderDirPath, api.ClientId, appConfig.SleepDelay, appConfig.MaxRetryCommandSeconds, appConfig.LoadOrdersFromFile, appConfig.Verbose);

                // Add to the list
                _apis.Add(_api);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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
                    if (_api != null && broker != null && broker.Pairs.Count != 0)
                    {
                        // Init the events
                        _api.OnOrderCreateEvent += OnOrderCreateEvent;
                        _api.OnOrderUpdateEvent += OnOrderUpdateEvent;
                        _api.OnOrderRemoveEvent += OnOrderRemoveEvent;
                        _api.OnLogEvent += OnLogEvent;
                        _api.OnBarDataEvent += OnBarDataEvent;
                        _api.OnTickEvent += OnTickEvent;

                        // Start the API
                        await _api.StartAsync();

                        // Subscribe foreach pair
                        _api.SubscribeForTicks(broker.Pairs.Select(f => f.TickerInMetatrader).ToList());
                        _api.SubscribeForBarData(broker.Pairs.Select(p => new KeyValuePair<string, string>(p.TickerInMetatrader, p.Timeframe)).ToList());
                    }
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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
                    _ = client.MessageReceived.Subscribe(msg =>
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
                                    // Get the right pair back from the local database
                                    var pair = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInTradingView.Equals(response.Instrument) && f.StrategyNr == response.StrategyType);

                                    // Get dynamic risk
                                    var dynRisk = new List<Risk>(_appConfig.Brokers
                                                                             .Where(f => f.ClientId == api.ClientId)
                                                                             .SelectMany(f => f.Risk ?? []));

                                    // If this broker is listening to this signal and the account size is greater then zero
                                    if (pair != null && api.AccountInfo != null && api.AccountInfo.Balance > 0 && api.MarketData != null)
                                    {
                                        // Get the metadata tick
                                        var metadataTick = api.MarketData.FirstOrDefault(f => f.Key == pair.TickerInMetatrader).Value;

                                        if (metadataTick.Ask > 0 && metadataTick.Bid > 0)
                                        {
                                            // Calculate spread
                                            var spread = Math.Round(Math.Abs(metadataTick.Ask - metadataTick.Bid), metadataTick.Digits, MidpointRounding.AwayFromZero);

                                            // If is it a buy
                                            if (response.OrderType == "BUY")
                                            {
                                                // Calculate SL Price
                                                var slPrice = CalculateSLForLong(
                                                            mtPrice: metadataTick.Ask,
                                                            mtSpread: spread,
                                                            mtDigits: metadataTick.Digits,
                                                            signalPrice: response.Price,
                                                            signalSL: response.StopLoss
                                                            );

                                                // Calculate the lot size
                                                var lotSize = CalculateLotSize(api.AccountInfo.Balance, pair.Risk, metadataTick.Ask, slPrice, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk);

                                                // do 0.0 check
                                                if (lotSize > 0.0)
                                                {
                                                    // Calculate TP Price
                                                    var tpPrice = CalculateTPForLong(
                                                                mtPrice: metadataTick.Ask,
                                                                mtDigits: metadataTick.Digits,
                                                                signalPrice: response.Price,
                                                                signalTP: response.TakeProfit
                                                                );

                                                    // Round
                                                    tpPrice = Math.Round(tpPrice, metadataTick.Digits);
                                                    slPrice = Math.Round(slPrice, metadataTick.Digits);

                                                    // Print on the screen
                                                    Print(Environment.NewLine);
                                                    Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                    Print("Order       : BUY MARKET ORDER");
                                                    Print("Lot Size    : " + lotSize);
                                                    Print("Ask         : " + metadataTick.Ask);
                                                    Print("Bid         : " + metadataTick.Bid);
                                                    Print("Digits      : " + metadataTick.Digits);
                                                    Print("Stop Loss   : " + slPrice);
                                                    Print("Take Profit : " + tpPrice);
                                                    Print("Magic       : " + response.Magic);
                                                    Print("Strategy    : " + pair.StrategyNr);
                                                    Print("SignalPrice : " + response.Price);
                                                    Print("SignalSL    : " + response.StopLoss);
                                                    Print("SignalTP    : " + response.TakeProfit);
                                                    Print("------------------------------------------------");

                                                    // Open order
                                                    var comment = string.Format($"{response.SignalID}/{Math.Round(response.Price, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(response.StopLoss, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");
                                                    api.ExecuteOrder(pair.TickerInMetatrader, OrderType.Buy, lotSize, 0, slPrice, tpPrice, (int)response.Magic, comment);
                                                }
                                            }

                                            // If is it a sell
                                            else if (response.OrderType == "SELL")
                                            {
                                                // Calculate SL Price
                                                var slPrice = CalculateSLForShort(
                                                            mtPrice: metadataTick.Ask,
                                                            mtSpread: spread,
                                                            mtDigits: metadataTick.Digits,
                                                            signalPrice: response.Price,
                                                            signalSL: response.StopLoss
                                                            );

                                                // Calculate the lot size
                                                var lotSize = CalculateLotSize(api.AccountInfo.Balance, pair.Risk, metadataTick.Ask, slPrice, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk);

                                                // do 0.0 check
                                                if (lotSize > 0.0)
                                                {
                                                    // Calculate TP Price
                                                    var tpPrice = CalculateTPForShort(
                                                                mtPrice: metadataTick.Ask,
                                                                mtDigits: metadataTick.Digits,
                                                                signalPrice: response.Price,
                                                                signalTP: response.TakeProfit
                                                                );

                                                    // Round
                                                    tpPrice = Math.Round(tpPrice, metadataTick.Digits, MidpointRounding.AwayFromZero);
                                                    slPrice = Math.Round(slPrice, metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                    // Print on the screen
                                                    Print(Environment.NewLine);
                                                    Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                    Print("Order       : SELL MARKET ORDER");
                                                    Print("Lot Size    : " + lotSize);
                                                    Print("Ask         : " + metadataTick.Ask);
                                                    Print("Bid         : " + metadataTick.Bid);
                                                    Print("Digits      : " + metadataTick.Digits);
                                                    Print("Stop Loss   : " + slPrice);
                                                    Print("Take Profit : " + tpPrice);
                                                    Print("Magic       : " + response.Magic);
                                                    Print("Strategy    : " + pair.StrategyNr);
                                                    Print("SignalPrice : " + response.Price);
                                                    Print("SignalSL    : " + response.StopLoss);
                                                    Print("SignalTP    : " + response.TakeProfit);
                                                    Print("------------------------------------------------");


                                                    // Open order
                                                    var comment = string.Format($"{response.SignalID}/{Math.Round(response.Price, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(response.StopLoss, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");
                                                    api.ExecuteOrder(pair.TickerInMetatrader, OrderType.Sell, lotSize, 0, slPrice, tpPrice, (int)response.Magic, comment);
                                                }
                                            }

                                            // Modify Stop Loss to Break Even
                                            else if (response.OrderType == "MODIFYSLTOBE" && response.Magic > 0)
                                            {
                                                // Check if the ticket still exist as open order
                                                var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == response.Magic);

                                                // Null reference check
                                                if (ticketId.Key > 0 && ticketId.Value.Type != null)
                                                {
                                                    // Get the strategy number from the comment field
                                                    string[] components = ticketId.Value.Comment != null ? ticketId.Value.Comment.Split('/') : [];
                                                    var offset = 0.0;
                                                    if (components != null && components.Length == 4)
                                                    {
                                                        _ = decimal.TryParse(components[1], out decimal signalEntryPrice);

                                                        // LONG
                                                        // Signal Price : 1.2
                                                        // Open Price : 1.3
                                                        offset = decimal.ToDouble(signalEntryPrice) - ticketId.Value.OpenPrice;
                                                    }

                                                    // If type is SELL, the SL should be set as BE minus Spread
                                                    var sl = ticketId.Value.OpenPrice - spread + offset;
                                                    if (ticketId.Value.Type.Equals("SELL", StringComparison.CurrentCultureIgnoreCase))
                                                        sl = ticketId.Value.OpenPrice + spread + offset;

                                                    // Round
                                                    sl = Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                    // Print on the screen
                                                    Print(Environment.NewLine);
                                                    Print("--------- SEND MODIFY SL TO BE ORDER TO METATRADER ---------");
                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                    Print("Order       : MODIFY SL TO BE ORDER");
                                                    Print("Lot Size    : " + ticketId.Value.Lots);
                                                    Print("Ask       : " + ticketId.Value.OpenPrice);
                                                    Print("Stop Loss   : " + sl);
                                                    Print("Take Profit : " + ticketId.Value.TakeProfit);
                                                    Print("Magic       : " + ticketId.Value.Magic);
                                                    Print("Strategy    : " + pair.StrategyNr);
                                                    Print("Ticket id   : " + ticketId.Key);
                                                    Print("------------------------------------------------");

                                                    // Modify order
                                                    api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, sl, ticketId.Value.TakeProfit);
                                                }
                                                else
                                                {
                                                    // Print on the screen
                                                    Print(Environment.NewLine);
                                                    Print("--------- UNABLE TO FIND TRADE ---------");
                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                    Print("Order       : MODIFY SL TO BE ORDER");
                                                    Print("------------------------------------------------");
                                                }
                                            }

                                            // Modify Stop Loss
                                            else if (response.OrderType == "MODIFYSL" && response.Magic > 0)
                                            {
                                                // Check if the ticket still exist as open order
                                                var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == response.Magic);

                                                // Null reference check
                                                if (ticketId.Key > 0)
                                                {
                                                    // Get market data
                                                    var marketdata = api.MarketData.FirstOrDefault(f => f.Key == ticketId.Value.Symbol);

                                                    // Null reference check
                                                    if (marketdata.Key != null)
                                                    {
                                                        // Calculate offset
                                                        var offset = Math.Round(response.Price - marketdata.Value.Ask, 4, MidpointRounding.AwayFromZero);

                                                        // Signal minus offset
                                                        var sl = response.StopLoss - offset;

                                                        // If SL is 
                                                        if (ticketId.Value.Type?.ToUpper() == "BUY" && response.StopLoss > ticketId.Value.OpenPrice)
                                                            sl = ticketId.Value.OpenPrice;
                                                        else if (ticketId.Value.Type?.ToUpper() == "SELL" && response.StopLoss < ticketId.Value.OpenPrice)
                                                            sl = ticketId.Value.OpenPrice;

                                                        // Round
                                                        sl = Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- SEND MODIFY SL ORDER TO METATRADER ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Ticker      : " + pair.TickerInMetatrader);
                                                        Print("Order       : MODIFY SL ORDER");
                                                        Print("Lot Size    : " + ticketId.Value.Lots);
                                                        Print("Ask       : " + ticketId.Value.OpenPrice);
                                                        Print("Stop Loss   : " + response.StopLoss);
                                                        Print("Take Profit : " + ticketId.Value.TakeProfit);
                                                        Print("Magic       : " + ticketId.Value.Magic);
                                                        Print("Strategy    : " + pair.StrategyNr);
                                                        Print("Ticket id   : " + ticketId.Key);
                                                        Print("------------------------------------------------");

                                                        // Modify order
                                                        api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, sl, ticketId.Value.TakeProfit);
                                                    }
                                                }
                                                else
                                                {
                                                    // Print on the screen
                                                    Print(Environment.NewLine);
                                                    Print("--------- UNABLE TO FIND TRADE ---------");
                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                    Print("Order       : MODIFY SL TO BE ORDER");
                                                    Print("------------------------------------------------");
                                                }
                                            }

                                            // Close trade
                                            else if (response.OrderType == "CLOSE" && response.Magic > 0)
                                            {
                                                // Check if the ticket still exist as open order
                                                var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == response.Magic);

                                                // Null reference check
                                                if (ticketId.Key > 0)
                                                {
                                                    // Print on the screen
                                                    Print(Environment.NewLine);
                                                    Print("--------- SEND CLOSE ORDER TO METATRADER ---------");
                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                    Print("Order       : CLOSE ORDER");
                                                    Print("Magic       : " + response.Magic);
                                                    Print("Ticket id   : " + ticketId.Key);
                                                    Print("------------------------------------------------");

                                                    // Modify order
                                                    api.CloseOrder(ticketId.Key);
                                                }
                                                else
                                                {
                                                    // Print on the screen
                                                    Print(Environment.NewLine);
                                                    Print("--------- UNABLE TO FIND TRADE ---------");
                                                    Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                    Print("Ticker      : " + pair.TickerInMetatrader);
                                                    Print("Order       : CLOSE ORDER");
                                                    Print("------------------------------------------------");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // Print on the screen
                                            Print(Environment.NewLine);
                                            Print("---------  ERROR NO MARKET DATA ---------");
                                            Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                            Print("Ticker      : " + pair.TickerInMetatrader);
                                            Print("Ask         : " + metadataTick.Ask);
                                            Print("Bid         : " + metadataTick.Bid);
                                            Print("Digits      : " + metadataTick.Digits);
                                            Print("Magic       : " + response.Magic);
                                            Print("Strategy    : " + pair.StrategyNr);
                                            Print("SignalPrice : " + response.Price);
                                            Print("SignalSL    : " + response.StopLoss);
                                            Print("SignalTP    : " + response.TakeProfit);
                                            Print("------------------------------------------------");
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="accountBalance"></param>
        /// <param name="riskPercent"></param>
        /// <param name="openPrice"></param>
        /// <param name="stopLossPrice"></param>
        /// <param name="tickValue"></param>
        /// <param name="tickStep"></param>
        /// <param name="lotStep"></param>
        /// <param name="minLotSizeAllowed"></param>
        /// <param name="maxLotSizeAllowed"></param>
        /// <param name="spread"></param>
        /// <returns></returns>
        public double CalculateLotSize(double accountBalance, double riskPercent, double openPrice, double stopLossPrice, double tickValue, double tickStep, double lotStep, double minLotSizeAllowed, double maxLotSizeAllowed, List<Risk>? riskData = null)
        {
            // Throw exception when negative balance
            if (accountBalance <= 0)
                return 0.0;


            // Calculate risk percentage
            double dynamicRisk = GetClosestRiskPercentage(accountBalance, riskData);

            // Calculate the initial lot size
            double riskAmount = accountBalance * ((riskPercent * dynamicRisk) / 100.0);
            double stopLossDistance = Math.Abs(openPrice - stopLossPrice);
            double stopLossDistanceInTicks = stopLossDistance / tickStep;
            double lotSize = riskAmount / (stopLossDistanceInTicks * tickValue);



            // Adjusting for lot step
            double remainder = lotSize % lotStep;
            double adjustedLotSize = remainder == 0 ? lotSize : lotSize - remainder + (remainder >= lotStep / 2 ? lotStep : 0);
            adjustedLotSize = Math.Round(adjustedLotSize, 2);

            // Bounds checking
            return Math.Clamp(adjustedLotSize, minLotSizeAllowed, maxLotSizeAllowed);
        }

        /// <summary>
        /// Calculate the stop loss signalEntryPrice for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK signalEntryPrice</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="mtSpread">Spread value</param>
        /// <param name="mtDigits">Tick size</param>
        /// <param name="signalPrice">Signal ENTRY signalEntryPrice</param>
        /// <param name="signalSL">Signal SL signalEntryPrice</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Stop loss signalEntryPrice</returns>
        public double CalculateSLForLong(double mtPrice, double mtSpread, int mtDigits, double signalPrice, double signalSL)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            var atrMultiplier = 1.2;

            // Calculate SL signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var slPrice = mtPrice - ((signalPrice - signalSL) * atrMultiplier);

            // Round
            slPrice = Math.Round(slPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price minus spread
            return slPrice - mtSpread;
        }

        /// <summary>
        /// Calculate the stop loss signalEntryPrice for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK signalEntryPrice</param>
        /// <param name="mtATR">MetaTrader ATR5M</param>
        /// <param name="mtSpread">The spread value</param>
        /// <param name="signalPrice">Signal ENTRY signalEntryPrice</param>
        /// <param name="signalSL">Signal SL signalEntryPrice</param>
        /// <param name="signalATR">Signal ATR5M</param>
        /// <returns>Stop loss signalEntryPrice</returns>
        public double CalculateSLForShort(double mtPrice, double mtSpread, int mtDigits, double signalPrice, double signalSL)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            //var atrMultiplier = mtATR > 0 && signalATR > 0 && mtATR > signalATR ? 1.1 : 1.0;
            var atrMultiplier = 1.2;

            // Calculate SL signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var slPrice = mtPrice + ((signalSL - signalPrice) * atrMultiplier);

            // Round
            slPrice = Math.Round(slPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price plus spread
            return slPrice + mtSpread;
        }

        /// <summary>
        /// Calculate the take profit signalEntryPrice for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK signalEntryPrice</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="mtTickSize">Tick size</param>
        /// <param name="signalPrice">Signal ENTRY signalEntryPrice</param>
        /// <param name="signalTP">Signal TP signalEntryPrice</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Take profit signalEntryPrice</returns>
        public double CalculateTPForLong(double mtPrice, int mtDigits, double signalPrice, double signalTP)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            //var atrMultiplier = mtATR > 0 && signalATR > 0 && mtATR > signalATR ? 1.1 : 1.0;
            var atrMultiplier = 1.0;

            // Calculate TP signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var tpPrice = mtPrice + ((signalTP - signalPrice) * atrMultiplier);

            // Round
            tpPrice = Math.Round(tpPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price minus spread
            return tpPrice;
        }

        /// <summary>
        /// Calculate the take profit signalEntryPrice for long positions based on the specified parameters
        /// </summary>
        /// <param name="mtPrice">MetaTrader ASK signalEntryPrice</param>
        /// <param name="mtATR">MetaTrader ATR</param>
        /// <param name="signalPrice">Signal ENTRY signalEntryPrice</param>
        /// <param name="signalTP">Signal TP signalEntryPrice</param>
        /// <param name="signalATR">Signal ATR</param>
        /// <returns>Take profit signalEntryPrice</returns>
        public double CalculateTPForShort(double mtPrice, int mtDigits, double signalPrice, double signalTP)
        {
            // Calculate the ATR multiplier based on the difference between MetaTrader's ATR and TradingView's ATR
            //var atrMultiplier = mtATR > 0 && signalATR > 0 && mtATR > signalATR ? 1.1 : 1.0;
            var atrMultiplier = 1.0;

            // Calculate TP signalEntryPrice using MetaTrader signalEntryPrice minus risk to take
            var tpPrice = mtPrice - ((signalPrice - signalTP) * atrMultiplier);

            // Round
            tpPrice = Math.Round(tpPrice, mtDigits, MidpointRounding.AwayFromZero);

            // Return SL Price plus spread
            return tpPrice;
        }




        private void OnBarDataEvent(long clientId, string symbol, string timeFrame, DateTime time, double open, double high, double low, double close, int tickVolume)
        {
            // Check if app config is not null
            if (_appConfig != null && _apis != null)
            {
                // Start the system
                //Parallel.ForEach(_apis, api =>
                foreach (var api in _apis.Where(f => f.ClientId == clientId && f.OpenOrders != null && f.OpenOrders.Count > 0))
                {
                    // Clone the open order
                    foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol != null && f.Value.Symbol.Equals(symbol)).ToDictionary(entry => entry.Key, entry => entry.Value))
                    {
                        // Get the strategy number from the comment field
                        string[] components = order.Value.Comment != null ? order.Value.Comment.Split('/') : [];
                        var offset = 0.0;
                        var signalEntryPrice = 0.0M;
                        var signalStopLoss = 0.0M;
                        var spread = 0.0M;
                        StrategyType strategyType = StrategyType.None;
                        if (components != null && components.Length == 5)
                        {
                            _ = decimal.TryParse(components[1], out signalEntryPrice);
                            _ = decimal.TryParse(components[2], out signalStopLoss);
                            _ = decimal.TryParse(components[4], out spread);
                            _ = Enum.TryParse(components[3].Replace("[sl]", string.Empty).Replace("[tp]", string.Empty), out strategyType);

                            // LONG
                            // Signal Price : 1.2
                            // Open Price : 1.3
                            offset = decimal.ToDouble(signalEntryPrice) - order.Value.OpenPrice;
                        }

                        // Get the right pair back from the local database
                        var ticker = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInTradingView.Equals(order.Value.Symbol) && f.StrategyNr == strategyType && f.Timeframe.Equals(timeFrame));

                        // If this broker is listening to this signal and the account size is greater then zero
                        if (ticker != null && signalEntryPrice > 0 && api.MarketData != null && signalStopLoss > 0 && strategyType != StrategyType.None)
                        {
                            // When the risk setting is enabled
                            if (ticker.Risk > 0)
                            {
                                // Calculate the risk
                                var risk = Math.Abs(signalEntryPrice - signalStopLoss);

                                // Add to the trading journal
                                var marketdata = api.MarketData.FirstOrDefault(f => f.Key == order.Value.Symbol);

                                // Do null reference check
                                if (!string.IsNullOrEmpty(marketdata.Key))
                                {
                                    // If the order is a buy
                                    if (order.Value.Type?.ToUpper() == "BUY")
                                    {
                                        // If the current ASK signalEntryPrice is greater then x times the risk
                                        if (close >= order.Value.OpenPrice + (ticker.SLtoBEafterR * decimal.ToDouble(risk)))
                                        {
                                            // Set SL to BE
                                            var slPrice = order.Value.OpenPrice - decimal.ToDouble(spread) + offset;

                                            // Round
                                            slPrice = Math.Round(slPrice, marketdata.Value.Digits, MidpointRounding.AwayFromZero);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != slPrice)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("------!!! AUTO !!! ------- SEND MODIFY SL TO BE ORDER TO METATRADER ------!!! AUTO !!! -------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                Print("Ticker      : " + ticker.TickerInMetatrader);
                                                Print("Order       : MODIFY SL TO BE ORDER");
                                                Print("Lot Size    : " + order.Value.Lots);
                                                Print("Ask       : " + order.Value.OpenPrice);
                                                Print("Stop Loss   : " + slPrice);
                                                Print("Take Profit : " + order.Value.TakeProfit);
                                                Print("Magic       : " + order.Value.Magic);
                                                Print("Strategy    : " + ticker.StrategyNr);
                                                Print("Ticket id   : " + order.Key);
                                                Print("------!!! AUTO !!! -------!!! AUTO !!! -------!!! AUTO !!! --------!!! AUTO !!! -------");

                                                // Modify order
                                                api.ModifyOrder(order.Key, order.Value.Lots, 0, slPrice, order.Value.TakeProfit);
                                            }
                                        }
                                    }
                                    else if (order.Value.Type?.ToUpper() == "SELL")
                                    {
                                        // If the current BID signalEntryPrice is smaller then x times the risk
                                        if (close <= order.Value.OpenPrice - (ticker.SLtoBEafterR * decimal.ToDouble(risk)))
                                        {
                                            // Set SL to BE
                                            var slPrice = order.Value.OpenPrice + decimal.ToDouble(spread) + offset;

                                            // Round
                                            slPrice = Math.Round(slPrice, marketdata.Value.Digits, MidpointRounding.AwayFromZero);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != slPrice)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("------!!! AUTO !!! ------- SEND MODIFY SL TO BE ORDER TO METATRADER ------!!! AUTO !!! -------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                Print("Ticker      : " + ticker.TickerInMetatrader);
                                                Print("Order       : MODIFY SL TO BE ORDER");
                                                Print("Lot Size    : " + order.Value.Lots);
                                                Print("Ask       : " + order.Value.OpenPrice);
                                                Print("Stop Loss   : " + slPrice);
                                                Print("Take Profit : " + order.Value.TakeProfit);
                                                Print("Magic       : " + order.Value.Magic);
                                                Print("Strategy    : " + ticker.StrategyNr);
                                                Print("Ticket id   : " + order.Key);
                                                Print("------!!! AUTO !!! -------!!! AUTO !!! -------!!! AUTO !!! --------!!! AUTO !!! -------");

                                                // Modify order
                                                api.ModifyOrder(order.Key, order.Value.Lots, 0, slPrice, order.Value.TakeProfit);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnTickEvent(long clientId, string symbol, double bid, double ask, double tickValue)
        {

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

        public static double GetClosestRiskPercentage(double accountBalance, List<Risk>? risks = null)
        {
            if (risks == null || risks.Count == 0)
                return 1.0;

            // Find the closest risk level to the account balance
            var closestRisk = risks.OrderBy(risk => Math.Abs(accountBalance - risk.Procent)).First();

            // Adjust the risk percentage
            return AdjustRiskPercentage(closestRisk.Multiplier);
        }

        public static double AdjustRiskPercentage(double percent)
        {
            // Adjust the percentage as per the requirement
            return percent >= 0 ? 1 + percent / 100 : 1 - Math.Abs(percent) / 100;
        }
    }
}
