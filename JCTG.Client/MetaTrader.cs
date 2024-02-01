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
                        _api.OnCandleCloseEvent += OnCandleCloseEvent;
                        _api.OnTickEvent += OnTickEvent;

                        // Start the API
                        await _api.StartAsync();

                        // Subscribe foreach pair
                        _api.SubscribeForTicks(broker.Pairs.Select(f => f.TickerInMetatrader).ToList());
                        _api.SubscribeForBarData(broker.Pairs.Select(p => new KeyValuePair<string, string>(p.TickerInMetatrader, p.Timeframe)).ToList());
                        _api.GetHistoricData(broker.Pairs.Where(f => f.OrderExecType == OrderExecType.Passive).Select(p => new KeyValuePair<string, string>(p.TickerInMetatrader, p.Timeframe)).ToList());
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
                    _ = client.MessageReceived.Subscribe(async msg =>
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

                                    // Start balance
                                    var startbalance = _appConfig.Brokers.First(f => f.ClientId == api.ClientId).StartBalance;

                                    // Get dynamic risk
                                    var dynRisk = new List<Risk>(_appConfig.Brokers
                                                                              .Where(f => f.ClientId == api.ClientId)
                                                                              .SelectMany(f => f.Risk ?? []));

                                    // If this broker is listening to this signal and the account size is greater then zero
                                    if (pair != null && api.AccountInfo != null && api.AccountInfo.Balance > 0 && api.MarketData != null)
                                    {
                                        // Get the metadata tick
                                        var metadataTick = api.MarketData.FirstOrDefault(f => f.Key == pair.TickerInMetatrader).Value;

                                        if (metadataTick != null && metadataTick.Ask > 0 && metadataTick.Bid > 0 && metadataTick.Digits > 0)
                                        {
                                            // Calculate spread
                                            var spread = Math.Round(Math.Abs(metadataTick.Ask - metadataTick.Bid), metadataTick.Digits, MidpointRounding.AwayFromZero);

                                            // BUY
                                            if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                        && response.OrderType == "BUY"
                                                        && response.Price.HasValue
                                                        && response.StopLoss.HasValue
                                                        && response.TakeProfit.HasValue
                                            )
                                            {
                                                // If Order execution is active
                                                if (pair.OrderExecType == OrderExecType.Active)
                                                {
                                                    // Calculate SL Price
                                                    var slPrice = RiskCalculator.SLForLong(
                                                            mtPrice: metadataTick.Ask,
                                                            mtSpread: spread,
                                                            mtDigits: metadataTick.Digits,
                                                            signalPrice: response.Price.Value,
                                                            signalSL: response.StopLoss.Value,
                                                            pairSlMultiplier: pair.SLMultiplier
                                                            );

                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, metadataTick.Ask, slPrice, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk);

                                                    // do 0.0 check
                                                    if (lotSize > 0.0M && slPrice > 0.0M && (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize))
                                                    {
                                                        // Calculate TP Price
                                                        var tpPrice = RiskCalculator.TPForLong(
                                                                    mtPrice: metadataTick.Ask,
                                                                    mtDigits: metadataTick.Digits,
                                                                    signalPrice: response.Price.Value,
                                                                    signalTP: response.TakeProfit.Value
                                                                    );

                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Date        : " + DateTime.UtcNow);
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
                                                        var comment = string.Format($"{response.SignalID}/{Math.Round(response.Price.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(response.StopLoss.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");
                                                        api.ExecuteOrder(pair.TickerInMetatrader, OrderType.Buy, lotSize, 0, slPrice, tpPrice, (int)response.Magic, comment);
                                                    }
                                                }

                                                // If order execution is passive
                                                else if (pair.OrderExecType == OrderExecType.Passive)
                                                {
                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, response.Price.Value, response.StopLoss.Value, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk); ;

                                                    // do 0.0 check
                                                    if (lotSize > 0.0M && (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize))
                                                    {
                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Date        : " + DateTime.UtcNow);
                                                        Print("Ticker      : " + pair.TickerInMetatrader);
                                                        Print("Order       : BUY LIMIT ORDER");
                                                        Print("Lot Size    : " + lotSize);
                                                        Print("Ask         : " + metadataTick.Ask);
                                                        Print("Bid         : " + metadataTick.Bid);
                                                        Print("Digits      : " + metadataTick.Digits);
                                                        Print("Stop Loss   : " + response.StopLoss.Value);
                                                        Print("Take Profit : " + response.TakeProfit.Value);
                                                        Print("Magic       : " + response.Magic);
                                                        Print("Strategy    : " + pair.StrategyNr);
                                                        Print("SignalPrice : " + response.Price);
                                                        Print("SignalSL    : " + response.StopLoss);
                                                        Print("SignalTP    : " + response.TakeProfit);
                                                        Print("------------------------------------------------");

                                                        // Open order
                                                        var comment = string.Format($"{response.SignalID}/{Math.Round(response.Price.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(response.StopLoss.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");
                                                        var orderType = OrderType.BuyLimit;
                                                        if (metadataTick.Ask < response.Price.Value)
                                                            orderType = OrderType.BuyStop;
                                                        api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, response.Price.Value, response.StopLoss.Value, response.TakeProfit.Value, (int)response.Magic, comment, 900);
                                                    }
                                                }
                                            }

                                            // BUY STOP
                                            else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                        && response.OrderType == "BUYSTOP"
                                                        && response.EntryExpression != null
                                                        && response.Risk.HasValue
                                                        && response.RiskRewardRatio.HasValue
                                            )
                                            {
                                                // If Order execution is passive (only passive can be set, for this kind of orders)
                                                if (pair.OrderExecType == OrderExecType.Passive)
                                                {
                                                    // Get the entry price
                                                    var price = await DynamicEvaluator.EvaluateExpressionAsync(response.EntryExpression, api.HistoricData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList());

                                                    // Get the Stop Loss price
                                                    var sl = price - response.Risk.Value - spread;

                                                    // Get the Take Profit Price
                                                    var tp = price + (response.Risk.Value * response.RiskRewardRatio.Value);

                                                    // Round
                                                    price = Math.Round(price, metadataTick.Digits, MidpointRounding.AwayFromZero);
                                                    sl = Math.Round(sl , metadataTick.Digits, MidpointRounding.AwayFromZero);
                                                    tp = Math.Round(tp, metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, price, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk); ;

                                                    // do 0.0 check
                                                    if (lotSize > 0.0M && price > 0.0M && sl > 0.0M && tp > 0.0M && (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize))
                                                    {
                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Date        : " + DateTime.UtcNow);
                                                        Print("Ticker      : " + pair.TickerInMetatrader);
                                                        if (metadataTick.Ask > price)
                                                            Print("Order       : BUY LIMIT ORDER");
                                                        else
                                                            Print("Order       : BUY STOP ORDER");
                                                        Print("Lot Size    : " + lotSize);
                                                        Print("Ask         : " + metadataTick.Ask);
                                                        Print("Bid         : " + metadataTick.Bid);
                                                        Print("Digits      : " + metadataTick.Digits);
                                                        Print("Entry price : " + price);
                                                        Print("Stop Loss   : " + sl);
                                                        Print("Take Profit : " + tp);
                                                        Print("Magic       : " + response.Magic);
                                                        Print("Strategy    : " + pair.StrategyNr);
                                                        Print("------------------------------------------------");

                                                        // Check if there is already a limit or stop order -> cancel

                                                        // Open order
                                                        var comment = string.Format($"{response.SignalID}/{price}/{sl}/{(int)pair.StrategyNr}/{spread}");
                                                        var orderType = OrderType.BuyStop;
                                                        if (metadataTick.Ask > price)
                                                            orderType = OrderType.BuyLimit;
                                                        api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, price, sl, tp, (int)response.Magic, comment);
                                                    }
                                                }
                                            }

                                            // SELL
                                            else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                                && response.OrderType == "SELL"
                                                                && response.Price.HasValue
                                                                && response.StopLoss.HasValue
                                                                && response.TakeProfit.HasValue
                                                                )
                                            {
                                                // If Order execution is active
                                                if (pair.OrderExecType == OrderExecType.Active)
                                                {
                                                    // Calculate SL Price
                                                    var slPrice = RiskCalculator.SLForShort(
                                                            mtPrice: metadataTick.Ask,
                                                            mtSpread: spread,
                                                            mtDigits: metadataTick.Digits,
                                                            signalPrice: response.Price.Value,
                                                            signalSL: response.StopLoss.Value,
                                                            pairSlMultiplier: pair.SLMultiplier
                                                            );

                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, metadataTick.Ask, slPrice, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk);

                                                    // do 0.0 check
                                                    if (lotSize > 0.0M && slPrice > 0.0M && (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize))
                                                    {
                                                        // Calculate TP Price
                                                        var tpPrice = RiskCalculator.TPForShort(
                                                                    mtPrice: metadataTick.Ask,
                                                                    mtDigits: metadataTick.Digits,
                                                                    signalPrice: response.Price.Value,
                                                                    signalTP: response.TakeProfit.Value
                                                                    );

                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Date        : " + DateTime.UtcNow);
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
                                                        var comment = string.Format($"{response.SignalID}/{Math.Round(response.Price.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(response.StopLoss.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");
                                                        api.ExecuteOrder(pair.TickerInMetatrader, OrderType.Sell, lotSize, 0, slPrice, tpPrice, (int)response.Magic, comment);
                                                    }
                                                }

                                                // If order execution is passive
                                                else if (pair.OrderExecType == OrderExecType.Passive)
                                                {
                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, response.Price.Value, response.StopLoss.Value, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk); ;

                                                    // do 0.0 check
                                                    if (lotSize > 0.0M && (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize))
                                                    {
                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Date        : " + DateTime.UtcNow);
                                                        Print("Ticker      : " + pair.TickerInMetatrader);
                                                        Print("Order       : SELL LIMIT ORDER");
                                                        Print("Lot Size    : " + lotSize);
                                                        Print("Ask         : " + metadataTick.Ask);
                                                        Print("Bid         : " + metadataTick.Bid);
                                                        Print("Digits      : " + metadataTick.Digits);
                                                        Print("Stop Loss   : " + response.StopLoss.Value);
                                                        Print("Take Profit : " + response.TakeProfit.Value);
                                                        Print("Magic       : " + response.Magic);
                                                        Print("Strategy    : " + pair.StrategyNr);
                                                        Print("SignalPrice : " + response.Price);
                                                        Print("SignalSL    : " + response.StopLoss);
                                                        Print("SignalTP    : " + response.TakeProfit);
                                                        Print("------------------------------------------------");

                                                        // Open order
                                                        var comment = string.Format($"{response.SignalID}/{Math.Round(response.Price.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{Math.Round(response.StopLoss.Value, metadataTick.Digits, MidpointRounding.AwayFromZero)}/{(int)pair.StrategyNr}/{spread}");
                                                        var orderType = OrderType.SellLimit;
                                                        if (metadataTick.Ask > response.Price.Value)
                                                            orderType = OrderType.SellStop;
                                                        api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, response.Price.Value, response.StopLoss.Value, response.TakeProfit.Value, (int)response.Magic, comment, 900);
                                                    }
                                                }
                                            }

                                            else if ((pair.MaxSpread == 0 || (pair.MaxSpread > 0 && spread < pair.MaxSpread))
                                                        && response.OrderType == "SELLSTOP"
                                                        && response.EntryExpression != null
                                                        && response.Risk.HasValue
                                                        && response.RiskRewardRatio.HasValue
                                            )
                                            {
                                                // If Order execution is passive (only passive can be set, for this kind of orders)
                                                if (pair.OrderExecType == OrderExecType.Passive)
                                                {
                                                    // Get the entry price
                                                    var price = await DynamicEvaluator.EvaluateExpressionAsync(response.EntryExpression, api.HistoricData.Where(f => f.Key == pair.TickerInMetatrader).SelectMany(f => f.Value.BarData).ToList());

                                                    // Get the Stop Loss price
                                                    var sl = price + response.Risk.Value + spread;

                                                    // Get the Take Profit Price
                                                    var tp = price - (response.Risk.Value * response.RiskRewardRatio.Value);

                                                    // Round
                                                    price = Math.Round(price, metadataTick.Digits, MidpointRounding.AwayFromZero);
                                                    sl = Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero);
                                                    tp = Math.Round(tp, metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                    // Calculate the lot size
                                                    var lotSize = RiskCalculator.LotSize(startbalance, api.AccountInfo.Balance, pair.Risk, price, sl, metadataTick.TickValue, metadataTick.TickSize, metadataTick.LotStep, metadataTick.MinLotSize, metadataTick.MaxLotSize, dynRisk); ;

                                                    // do 0.0 check
                                                    if (lotSize > 0.0M && price > 0.0M && sl > 0.0M && tp > 0.0M && (pair.MaxLotSize == 0 || pair.MaxLotSize > 0 && lotSize <= pair.MaxLotSize))
                                                    {
                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- SEND NEW ORDER TO METATRADER ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Date        : " + DateTime.UtcNow);
                                                        Print("Ticker      : " + pair.TickerInMetatrader);
                                                        if (metadataTick.Ask < price)
                                                            Print("Order       : SELL LIMIT ORDER");
                                                        else
                                                            Print("Order       : SELL STOP ORDER");
                                                        Print("Lot Size    : " + lotSize);
                                                        Print("Ask         : " + metadataTick.Ask);
                                                        Print("Bid         : " + metadataTick.Bid);
                                                        Print("Digits      : " + metadataTick.Digits);
                                                        Print("Entry price : " + price);
                                                        Print("Stop Loss   : " + sl);
                                                        Print("Take Profit : " + tp);
                                                        Print("Magic       : " + response.Magic);
                                                        Print("Strategy    : " + pair.StrategyNr);
                                                        Print("------------------------------------------------");

                                                        // Open order
                                                        var comment = string.Format($"{response.SignalID}/{price}/{sl}/{(int)pair.StrategyNr}/{spread}");
                                                        var orderType = OrderType.SellStop;
                                                        if (metadataTick.Ask < price)
                                                            orderType = OrderType.SellLimit;
                                                        api.ExecuteOrder(pair.TickerInMetatrader, orderType, lotSize, price, sl, tp, (int)response.Magic, comment);
                                                    }
                                                }
                                            }

                                            // MODIFYSLTOBE
                                            else if (response.OrderType == "MODIFYSLTOBE" && response.Magic > 0)
                                            {
                                                // Check if the ticket still exist as open order
                                                var ticketId = api.OpenOrders.FirstOrDefault(f => f.Value.Magic == response.Magic);

                                                // Null reference check
                                                if (ticketId.Key > 0 && ticketId.Value.Type != null)
                                                {
                                                    // If Order execution is active
                                                    if (pair.OrderExecType == OrderExecType.Active)
                                                    {
                                                        // Get the strategy number from the comment field
                                                        string[] components = ticketId.Value.Comment != null ? ticketId.Value.Comment.Split('/') : [];
                                                        var offset = 0.0M;
                                                        if (components != null && components.Length == 4)
                                                        {
                                                            _ = decimal.TryParse(components[1], out decimal signalEntryPrice);

                                                            // LONG
                                                            // Signal Price : 1.2
                                                            // Open Price : 1.3
                                                            offset = signalEntryPrice - ticketId.Value.OpenPrice;
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
                                                        Print("Ask         : " + ticketId.Value.OpenPrice);
                                                        Print("Stop Loss   : " + sl);
                                                        Print("Take Profit : " + ticketId.Value.TakeProfit);
                                                        Print("Magic       : " + ticketId.Value.Magic);
                                                        Print("Strategy    : " + pair.StrategyNr);
                                                        Print("Ticket id   : " + ticketId.Key);
                                                        Print("------------------------------------------------");

                                                        // Modify order
                                                        api.ModifyOrder(ticketId.Key, ticketId.Value.Lots, 0, sl, ticketId.Value.TakeProfit);
                                                    }

                                                    // If Order execution is passive
                                                    else if (pair.OrderExecType == OrderExecType.Passive)
                                                    {
                                                        // the SL should be set as BE minus Spread
                                                        var sl = ticketId.Value.OpenPrice - spread;
                                                        if (ticketId.Value.Type.Equals("SELL", StringComparison.CurrentCultureIgnoreCase))
                                                            sl = ticketId.Value.OpenPrice + spread;

                                                        // Round
                                                        sl = Math.Round(sl, metadataTick.Digits, MidpointRounding.AwayFromZero);

                                                        // Print on the screen
                                                        Print(Environment.NewLine);
                                                        Print("--------- SEND MODIFY SL TO BE ORDER TO METATRADER ---------");
                                                        Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                        Print("Ticker      : " + pair.TickerInMetatrader);
                                                        Print("Order       : MODIFY SL TO BE ORDER");
                                                        Print("Lot Size    : " + ticketId.Value.Lots);
                                                        Print("Ask         : " + ticketId.Value.OpenPrice);
                                                        Print("Stop Loss   : " + sl);
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

                                            if (pair.MaxSpread > 0 && spread > pair.MaxSpread)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("--------- !!!! SPREAD TOO HIGH !!!! ---------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                Print("Ticker      : " + pair.TickerInMetatrader);
                                                Print("------------------------------------------------");
                                            }
                                        }
                                        else
                                        {
                                            // Print on the screen
                                            Print(Environment.NewLine);
                                            Print("---------  ERROR NO MARKET DATA ---------");
                                            Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                            Print("Ticker      : " + pair.TickerInMetatrader);
                                            Print("Ask         : " + metadataTick?.Ask);
                                            Print("Bid         : " + metadataTick?.Bid);
                                            Print("Digits      : " + metadataTick?.Digits);
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



        private void OnCandleCloseEvent(long clientId, string symbol, string timeFrame, DateTime time, decimal open, decimal high, decimal low, decimal close, int tickVolume)
        {
            // Make sure we have the instrument name right
            var instrument = symbol.Replace("_" + timeFrame.ToUpper(), string.Empty);

            // Check if app config is not null
            if (_appConfig != null && _apis != null)
            {
                // Start the system
                foreach (var api in _apis.Where(f => f.ClientId == clientId && f.OpenOrders != null && f.OpenOrders.Count > 0))
                {
                    // Clone the open order
                    foreach (var order in api.OpenOrders.Where(f => f.Value.Symbol != null && f.Value.Symbol.Equals(instrument)).ToDictionary(entry => entry.Key, entry => entry.Value))
                    {

                        // Get the strategy number from the comment field
                        string[] components = order.Value.Comment != null ? order.Value.Comment.Split('/') : [];
                        var offset = 0.0M;
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
                            offset = signalEntryPrice - order.Value.OpenPrice;
                        }

                        // Get the right pair back from the local database
                        var pair = new List<Pairs>(_appConfig.Brokers.Where(f => f.ClientId == api.ClientId).SelectMany(f => f.Pairs)).FirstOrDefault(f => f.TickerInMetatrader.Equals(order.Value.Symbol) && f.StrategyNr == strategyType && f.Timeframe.Equals(timeFrame));

                        // If this broker is listening to this signal and the account size is greater then zero
                        if (pair != null && signalEntryPrice > 0 && api.MarketData != null && signalStopLoss > 0 && strategyType != StrategyType.None)
                        {
                            // When the risk setting is enabled
                            if (pair.Risk > 0)
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
                                        if (close >= (order.Value.OpenPrice + (Convert.ToDecimal(pair.SLtoBEafterR) * risk) + offset))
                                        {
                                            // Set SL to BE
                                            var slPrice = order.Value.OpenPrice - spread + offset;

                                            // Round
                                            slPrice = Math.Round(slPrice, marketdata.Value.Digits, MidpointRounding.AwayFromZero);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != slPrice)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("------!!! AUTO !!! ------- SEND MODIFY SL TO BE ORDER TO METATRADER ------!!! AUTO !!! -------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                Print("Ticker      : " + pair.TickerInMetatrader);
                                                Print("Order       : MODIFY SL TO BE ORDER");
                                                Print("Lot Size    : " + order.Value.Lots);
                                                Print("Ask         : " + order.Value.OpenPrice);
                                                Print("Stop Loss   : " + slPrice);
                                                Print("Take Profit : " + order.Value.TakeProfit);
                                                Print("Magic       : " + order.Value.Magic);
                                                Print("Strategy    : " + pair.StrategyNr);
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
                                        if (close <= (order.Value.OpenPrice - (Convert.ToDecimal(pair.SLtoBEafterR) * risk)) + offset)
                                        {
                                            // Set SL to BE
                                            var slPrice = order.Value.OpenPrice + spread + offset;

                                            // Round
                                            slPrice = Math.Round(slPrice, marketdata.Value.Digits, MidpointRounding.AwayFromZero);

                                            // Check if SL is already set to BE
                                            if (order.Value.StopLoss != slPrice)
                                            {
                                                // Print on the screen
                                                Print(Environment.NewLine);
                                                Print("------!!! AUTO !!! ------- SEND MODIFY SL TO BE ORDER TO METATRADER ------!!! AUTO !!! -------");
                                                Print("Broker      : " + _appConfig.Brokers.First(f => f.ClientId == api.ClientId).Name);
                                                Print("Ticker      : " + pair.TickerInMetatrader);
                                                Print("Order       : MODIFY SL TO BE ORDER");
                                                Print("Lot Size    : " + order.Value.Lots);
                                                Print("Ask         : " + order.Value.OpenPrice);
                                                Print("Stop Loss   : " + slPrice);
                                                Print("Take Profit : " + order.Value.TakeProfit);
                                                Print("Magic       : " + order.Value.Magic);
                                                Print("Strategy    : " + pair.StrategyNr);
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

        private void OnTickEvent(long clientId, string symbol, decimal bid, decimal ask, decimal tickValue)
        {
            foreach (var api in _apis)
            {
            }
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


    }
}
