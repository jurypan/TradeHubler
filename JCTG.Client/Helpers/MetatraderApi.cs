using System.Globalization;
using JCTG.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static JCTG.Client.Helpers;


namespace JCTG.Client
{
    public class MetatraderApi
    {
        private AutoResetEvent _resetHistoricBarDataEvent = new AutoResetEvent(false);

        private readonly string MetaTraderDirPath;
        private readonly int sleepDelay;
        private readonly int maxRetryCommandSeconds;
        private readonly bool loadDataFromFile;

        private readonly string pathOrders;
        private readonly string pathMessages;
        private readonly string pathMarketData;
        private readonly string pathCandleCloseData;
        private readonly string pathHistoricBarData;
        private readonly string pathDeals;
        private readonly string pathOrdersStored;
        private readonly string pathMessagesStored;
        private readonly string pathDealsStored;
        private readonly string pathCommandsPrefix;

        private readonly int maxCommandFiles = 20;
        private int commandID = 0;
        private long lastMessageId = 0;
        private string lastOpenOrdersStr = "";
        private string lastMessagesStr = "";
        private string lastMarketDataStr = "";
        private string lastCandleCloseStr = "";
        private string lastHistoricBarDataStr = "";
        private string lastDealsStr = "";


        public Dictionary<long, Order> OpenOrders { get; private set; } = [];
        public AccountInfo? AccountInfo { get; private set; }
        public Dictionary<string, MarketData> MarketData { get; private set; } = [];
        public Dictionary<string, BarData> LastBarData { get; private set; } = [];
        public Dictionary<string, HistoricBarData> HistoricBarData { get; private set; } = [];
        public Dictionary<long, Deal> Deals { get; private set; } = [];

        public long ClientId { get; private set; }

        public bool IsActive = true;
        private bool START = false;


        private Thread? openOrdersThread;
        private Thread? messageThread;
        private Thread? marketDataThread;
        private Thread? barDataThread;
        private Thread? historicBarDataThread;
        private Thread? dealsThread;

        // Define the delegate for the event
        public delegate void OnStartedEventHandler(long clientId);
        public event OnStartedEventHandler? OnStartedEvent;

        public delegate void OnOrderCreateEventHandler(long clientId, long ticketId, Order order);
        public event OnOrderCreateEventHandler? OnOrderCreateEvent;

        public delegate void OnOrderUpdateEventHandler(long clientId, long ticketId, Order order);
        public event OnOrderUpdateEventHandler? OnOrderUpdateEvent;

        public delegate void OnOrderCloseEventHandler(long clientId, long ticketId, Order order);
        public event OnOrderCloseEventHandler? OnOrderCloseEvent;

        public delegate void OnLogEventHandler(long clientId, long id, Log log);
        public event OnLogEventHandler? OnLogEvent;

        public delegate void OnTickEventHandler(long clientId, string symbol, decimal ask, decimal bid, decimal tickSize, int digits);
        public event OnTickEventHandler? OnTickEvent;

        public delegate void OnCandleCloseEventHandler(long clientId, string symbol, string timeFrame, DateTime time, decimal open, decimal high, decimal low, decimal close, int tickVolume);
        public event OnCandleCloseEventHandler? OnCandleCloseEvent;

        public delegate void OnHistoricBarDataEventHandler(long clientId, string symbol, string timeFrame);
        public event OnHistoricBarDataEventHandler? OnHistoricBarDataEvent;

        public delegate void OnDealCreatedEventHandler(long clientId, long dealId, Deal deal);
        public event OnDealCreatedEventHandler? OnDealCreatedEvent;

        public delegate void OnAccountInfoChangedHandler(long clientId, AccountInfo accountInfo);
        public event OnAccountInfoChangedHandler? OnAccountInfoChangedEvent;



        public MetatraderApi(string metaTraderDirPath, long clientId, int sleepDelay, int maxRetryCommandSeconds, bool loadDataFromFile)
        {
            this.MetaTraderDirPath = metaTraderDirPath;
            this.ClientId = clientId;
            this.sleepDelay = sleepDelay;
            this.maxRetryCommandSeconds = maxRetryCommandSeconds;
            this.loadDataFromFile = loadDataFromFile;

            if (!Directory.Exists(metaTraderDirPath))
            {
                Print("ERROR: metaTraderDirPath does not exist! metaTraderDirPath: " + metaTraderDirPath);
                Environment.Exit(1);
            }

            this.pathOrders = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Orders.json");
            this.pathMessages = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Messages.json");
            this.pathMarketData = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Market_Data.json");
            this.pathCandleCloseData = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Bar_Data.json");
            this.pathHistoricBarData = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Historic_BarData.json");
            this.pathDeals = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Historic_Trades.json");
            this.pathOrdersStored = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Orders_Stored.json");
            this.pathMessagesStored = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Messages_Stored.json");
            this.pathDealsStored = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Trades_Stored.json");
            this.pathCommandsPrefix = Path.Join(metaTraderDirPath, "JCTG", "JCTG_Commands_");
        }


        /// <summary>
        /// can be used to check if the _client has been initialized.  
        /// </summary>
        public async Task StartAsync()
        {
            IsActive = true;
            await LoadLogsAsync();

            if (loadDataFromFile)
                await LoadDataAsync();

            // StartCheckTimeAndExecuteOnceDaily the thread to run the asynchronous method
            this.openOrdersThread = new Thread(async () => await CheckOpenOrdersAsync());
            this.openOrdersThread?.Start();

            this.messageThread = new Thread(async () => await CheckMessagesAsync());
            this.messageThread?.Start();

            this.barDataThread = new Thread(async () => await CheckCandleCloseAsync());
            this.barDataThread?.Start();

            this.historicBarDataThread = new Thread(async () => await CheckHistoricBarDataAsync());
            this.historicBarDataThread?.Start();

            this.dealsThread = new Thread(async () => await CheckDealsAsync());
            this.dealsThread?.Start();

            this.marketDataThread = new Thread(async () => await CheckMarketDataAsync());
            this.marketDataThread?.Start();

            await ResetCommandIDsAsync();

            START = true;
        }

        /// <summary>
        /// can be used to check if the _client has been initialized.  
        /// </summary>
        public async Task StopAsync()
        {
            IsActive = false;
            START = false;
            openOrdersThread?.Join();
            messageThread?.Join();
            marketDataThread?.Join();
            barDataThread?.Join();
            historicBarDataThread?.Join();
            dealsThread?.Join();
            await Task.FromResult(0);
        }



        /// <summary>
        /// Regularly checks the file for open orders and triggers the eventHandler.OnOrderEvents functions.
        /// </summary>
        private async Task CheckOpenOrdersAsync()
        {
            while (IsActive)
            {

                Thread.Sleep(sleepDelay);

                if (!START)
                    continue;

                string text = await TryReadFileAsync(pathOrders);

                if (text.Length == 0 || text.Equals(lastOpenOrdersStr))
                    continue;

                lastOpenOrdersStr = text;

                JObject data;

                try
                {
                    data = JObject.Parse(text);
                }
                catch
                {
                    continue;
                }

                if (data == null)
                    continue;

                // Cast account info
                var accountInfo = data["account_info"]?.ToObject<AccountInfo>();

                // Check if the account info is changed
                if (accountInfo != null && AccountInfo != null && accountInfo.Balance != AccountInfo.Balance)
                {
                    // Invoke the event
                    OnAccountInfoChangedEvent?.Invoke(this.ClientId, accountInfo);
                }

                // Set the new account info
                AccountInfo = accountInfo;

                // Assuming 'dataOrders' is the JObject that contains your JSON dataOrders
                JObject ordersData = (JObject)data["orders"];

                // If market dataOrders is null -> create new instance
                if (OpenOrders == null)
                    OpenOrders = new Dictionary<long, Order>();

                // Iterate over each order in the JSON
                if (this.AccountInfo != null && ordersData != null)
                {
                    // Next, handle removal of orders not in ordersData
                    List<long> ordersToRemove = [];
                    foreach (var openOrder in OpenOrders)
                    {
                        long orderId = openOrder.Key;

                        // If orderId is not in ordersData, mark it for removal
                        if (!ordersData.ContainsKey(orderId.ToString()))
                        {
                            ordersToRemove.Add(orderId);
                        }
                    }

                    // Add or update
                    foreach (var orderEntry in ordersData)
                    {
                        long orderId = long.Parse(orderEntry.Key);

                        if (orderEntry.Value != null)
                        {
                            JObject value = (JObject)orderEntry.Value;

                            if (value != null)
                            {
                                var newOrder = new Order
                                {
                                    Symbol = value["symbol"].ToObject<string>(),
                                    Lots = value["lots"].ToObject<decimal>(),
                                    Type = value["type"].ToObject<string>(),
                                    OpenPrice = value["open_price"].ToObject<decimal>(),
                                    OpenTime = DateTime.SpecifyKind(DateTime.ParseExact(value["open_time"].ToString(), "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture).AddHours(-(this.AccountInfo == null ? 0.0 : this.AccountInfo.TimezoneOffset)), DateTimeKind.Utc),
                                    StopLoss = value["SL"].ToObject<decimal>(),
                                    TakeProfit = value["TP"].ToObject<decimal>(),
                                    Pnl = value["pnl"].ToObject<double>(),
                                    Commission = value["commission"] != null ? value["commission"].ToObject<double>() : 0.0,
                                    Swap = value["swap"].ToObject<double>(),
                                    Comment = value["comment"].ToObject<string>(),
                                    Magic = value["magic"].ToObject<int>(),
                                };

                                // Check if the order already exists
                                if (OpenOrders.TryGetValue(orderId, out var previousData))
                                {
                                    // Update the previous values
                                    OpenOrders[orderId] = newOrder;

                                    // Check if the values have changed
                                    if (newOrder.StopLoss != previousData.StopLoss || newOrder.TakeProfit != previousData.TakeProfit)
                                    {
                                        // Invoke the event
                                        OnOrderUpdateEvent?.Invoke(ClientId, orderId, newOrder);
                                    }
                                }
                                else
                                {
                                    // If it's a new order, add it to the dictionary and invoke the event
                                    OpenOrders.Add(orderId, newOrder);
                                    OnOrderCreateEvent?.Invoke(ClientId, orderId, newOrder);
                                }
                            }
                        }
                    }

                    // Remove the marked orders
                    foreach (var orderId in ordersToRemove)
                    {
                        // Set close date
                        OpenOrders[orderId].CloseTime = DateTime.UtcNow;

                        // Invoke the event
                        OnOrderCloseEvent?.Invoke(ClientId, orderId, OpenOrders[orderId]);

                        // Remove the order from the list
                        OpenOrders.Remove(orderId);
                    }

                }

                if (loadDataFromFile)
                    await TryWriteToFileAsync(pathOrdersStored, data.ToString());
            }
        }

        /// <summary>
        /// Regularly checks the file for deals and triggers the eventHandler.OnTradevent() function.
        /// </summary>
        private async Task CheckDealsAsync()
        {
            while (IsActive)
            {

                Thread.Sleep(sleepDelay);

                if (!START)
                    continue;

                // Read file
                string text = await TryReadFileAsync(pathDeals);

                // Make sure the import is new
                if (this.AccountInfo != null && text.Length > 0 && !text.Equals(lastDealsStr))
                {
                    // Set new ordersStored file to the variable
                    lastDealsStr = text;

                    // If null -> new instance
                    Deals ??= [];

                    // Parse it to objects
                    var deals = JsonConvert.DeserializeObject<Dictionary<long, Deal>>(text);

                    // Do null reference check
                    if (deals != null)
                    {
                        // Foreach deal
                        foreach (var deal in deals)
                        {
                            // Check if it's already in the current collection, if not -> throw event
                            if (!Deals.Any(f => f.Key == deal.Key))
                            {
                                OnDealCreatedEvent?.Invoke(this.ClientId, deal.Key, deal.Value);
                            }
                        }

                        // Update the object
                        Deals = deals;
                    }

                    if (loadDataFromFile)
                        await TryWriteToFileAsync(pathDealsStored, lastDealsStr);
                }
            }
        }

        /// <summary>
        /// Regularly checks the file for messages and triggers the eventHandler.OnMessage() function.
        /// </summary>
        private async Task CheckMessagesAsync()
        {
            while (IsActive)
            {

                Thread.Sleep(sleepDelay);

                if (!START)
                    continue;

                string text = await TryReadFileAsync(pathMessages);

                if (text.Length == 0 || text.Equals(lastMessagesStr))
                    continue;

                lastMessagesStr = text;

                JObject data;

                try
                {
                    data = JObject.Parse(text);
                }
                catch
                {
                    continue;
                }

                if (data == null)
                    continue;

                // Convert JObject to a list of HttpCallOnLogEvent objects
                var logs = JsonConvert.DeserializeObject<Dictionary<long, Log>>(text);

                // Sort the logs by Time
                var sortedLogs = logs?.OrderBy(f => f.Key).ToList();

                if (sortedLogs != null)
                {
                    foreach (var item in sortedLogs)
                    {
                        if (item.Key > lastMessageId)
                        {
                            lastMessageId = item.Key;
                            OnLogEvent?.Invoke(ClientId, item.Key, item.Value);
                        }
                    }
                }


                // Assuming TryWriteToFileAsync is a method that takes a path and a string
                await TryWriteToFileAsync(pathMessagesStored, JsonConvert.SerializeObject(data));

            }
        }

        /// <summary>
        /// Regularly checks the file for market dataOrders and triggers the eventHandler.OnTick() function.
        /// </summary>
        private async Task CheckMarketDataAsync()
        {
            var isStarted = false;

            while (IsActive)
            {
                // Sleep
                Thread.Sleep(sleepDelay);

                // If not started -> Skip
                if (!START)
                    continue;

                // Parse MT4 ordersStored file
                string text = await TryReadFileAsync(pathMarketData);

                // Check if the file is changed in regards to the previous version
                if (text.Length == 0 || text.Equals(lastMarketDataStr))
                    continue;

                // Save file
                lastMarketDataStr = text;


                var _marketData = JsonConvert.DeserializeObject<Dictionary<string, MarketData>>(lastMarketDataStr);

                // If market dataOrders is null -> create new instance
                MarketData ??= [];

                // Foreach property of the dataOrders
                if (_marketData != null)
                {
                    foreach (var property in _marketData)
                    {
                        if (property.Key != null && property.Value != null)
                        {
                            // Check if the ticker already has previous values
                            if (MarketData.TryGetValue(property.Key, out var previousData))
                            {
                                // Update the previous values
                                MarketData[property.Key] = property.Value;

                                // Check if the values have changed
                                if (property.Value.Bid != previousData.Bid || property.Value.Ask != previousData.Ask || property.Value.TickSize != previousData.TickSize)
                                {
                                    // Invoke the event
                                    OnTickEvent?.Invoke(ClientId, property.Key, property.Value.Ask, property.Value.Bid, property.Value.TickSize, property.Value.Digits);
                                }
                            }
                            else
                            {
                                // If it's a new ticker, add it to the dictionary and invoke the event
                                MarketData.Add(property.Key, property.Value);
                                OnTickEvent?.Invoke(ClientId, property.Key, property.Value.Ask, property.Value.Bid, property.Value.TickSize, property.Value.Digits);
                            }
                        }
                    }

                    // Throw event
                    if(isStarted == false)
                    {
                        // Reverse flag
                        isStarted = true;

                        // Throw event
                        OnStartedEvent?.Invoke(ClientId);
                    }
                }
            }
        }

        /// <summary>
        /// Regularly checks the file for bar dataOrders and triggers the eventHandler.OnBarData() function.
        /// </summary>
        private async Task CheckCandleCloseAsync()
        {
            while (IsActive)
            {
                Thread.Sleep(sleepDelay);

                if (!START)
                    continue;

                string text = await TryReadFileAsync(pathCandleCloseData);

                // Make sure the import is new
                if (this.AccountInfo != null && text.Length > 0 && !text.Equals(lastCandleCloseStr))
                {
                    lastCandleCloseStr = text;

                    JObject data;

                    try
                    {
                        data = JObject.Parse(text);
                    }
                    catch
                    {
                        continue;
                    }

                    if (data == null)
                        continue;

                    if (LastBarData == null)
                        LastBarData = [];

                    foreach (var property in data.Properties())
                    {
                        var value = property.Value as JObject;
                        if (value != null && value["time"] != null && value["open"] != null && value["high"] != null && value["low"] != null && value["close"] != null && value["tick_volume"] != null)
                        {
                            int lastIndex = property.Name.LastIndexOf('_');

                            if (lastIndex != -1)
                            {
                                string firstPart = property.Name.Substring(0, lastIndex);
                                string secondPart = property.Name.Substring(lastIndex + 1);

                                string[] stSplit = [firstPart, secondPart];
                                if (stSplit.Length != 2)
                                    continue;

                                var instrument = stSplit[0];

                                var newBarData = new BarData
                                {
                                    Timeframe = stSplit[1],
                                    Time = DateTime.SpecifyKind(value["time"].ToObject<DateTime>().AddHours(-(this.AccountInfo == null ? 0.0 : this.AccountInfo.TimezoneOffset)), DateTimeKind.Utc),
                                    Open = value["open"].ToObject<decimal>(),
                                    High = value["high"].ToObject<decimal>(),
                                    Low = value["low"].ToObject<decimal>(),
                                    Close = value["close"].ToObject<decimal>(),
                                    TickVolume = value["tick_volume"].ToObject<int>()
                                };

                                // Check if the ticker already has previous values
                                if (LastBarData.TryGetValue(property.Name, out var previousData))
                                {
                                    // Update the previous values
                                    LastBarData[property.Name] = new BarData
                                    {
                                        Timeframe = newBarData.Timeframe,
                                        Time = newBarData.Time,
                                        Open = newBarData.Open,
                                        High = newBarData.High,
                                        Low = newBarData.Low,
                                        Close = newBarData.Close,
                                        TickVolume = newBarData.TickVolume

                                    };

                                    // Invoke the event
                                    OnCandleCloseEvent?.Invoke(ClientId, property.Name, newBarData.Timeframe, newBarData.Time, newBarData.Open, newBarData.High, newBarData.Low, newBarData.Close, newBarData.TickVolume);
                                }
                                else
                                {
                                    // If it's a new ticker, add it to the dictionary and invoke the event
                                    LastBarData.Add(property.Name, new BarData
                                    {
                                        Timeframe = newBarData.Timeframe,
                                        Time = newBarData.Time,
                                        Open = newBarData.Open,
                                        High = newBarData.High,
                                        Low = newBarData.Low,
                                        Close = newBarData.Close,
                                        TickVolume = newBarData.TickVolume
                                    });

                                    // Invoke the event
                                    OnCandleCloseEvent?.Invoke(ClientId, property.Name, newBarData.Timeframe, newBarData.Time, newBarData.Open, newBarData.High, newBarData.Low, newBarData.Close, newBarData.TickVolume);
                                }

                                // Update the historic dataOrders
                                if (HistoricBarData == null)
                                    HistoricBarData = new Dictionary<string, HistoricBarData>();

                                // Update historic candles
                                if (HistoricBarData.ContainsKey(instrument))
                                {
                                    // Update the previous values
                                    if (!HistoricBarData[instrument].BarData.Any(f => f.Time == newBarData.Time && f.Timeframe == newBarData.Timeframe))
                                        HistoricBarData[instrument].BarData.Add(newBarData);
                                }
                                else
                                {
                                    var hbd = new HistoricBarData();
                                    hbd.BarData.Add(newBarData);
                                    HistoricBarData.Add(instrument, hbd);
                                }

                                foreach (var valueBD in HistoricBarData.Values)
                                {
                                    valueBD.BarData = valueBD.BarData.OrderBy(f => f.Time).ToList();
                                }
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Regularly checks the file for historic dataOrders and triggers the eventHandler.OnHistoricData() function.
        /// </summary>
        private async Task CheckHistoricBarDataAsync()
        {
            // Delete file
            await TryDeleteFileAsync(pathHistoricBarData);

            // Loop
            while (IsActive)
            {

                Thread.Sleep(sleepDelay);

                if (!START)
                    continue;

                // Read file
                string text = await TryReadFileAsync(pathHistoricBarData);

                // Make sure the import is new
                if (this.AccountInfo != null && text.Length > 0 && !text.Equals(lastHistoricBarDataStr))
                {
                    // Set new ordersStored file to the variable
                    lastHistoricBarDataStr = text;

                    JObject data;

                    try
                    {
                        data = JObject.Parse(text);
                    }
                    catch
                    {
                        _resetHistoricBarDataEvent.Set();
                        continue;
                    }

                    if (data == null)
                    {
                        _resetHistoricBarDataEvent.Set();
                        continue;
                    }

                    if (HistoricBarData == null)
                        HistoricBarData = new Dictionary<string, HistoricBarData>();

                    foreach (var property in data.Properties())
                    {
                        var items = property.Value as JArray;
                        if (items != null)
                        {
                            int lastIndex = property.Name.LastIndexOf('_');

                            if (lastIndex != -1)
                            {
                                string firstPart = property.Name.Substring(0, lastIndex);
                                string secondPart = property.Name.Substring(lastIndex + 1);

                                string[] stSplit = [firstPart, secondPart];
                                if (stSplit.Length != 2)
                                    continue;

                                var instrument = stSplit[0];
                                var timeframe = stSplit[1];

                                // Foreach bardata
                                foreach (var item in items)
                                {
                                    if (HistoricBarData.ContainsKey(instrument))
                                    {
                                        var obj = new BarData
                                        {
                                            Timeframe = timeframe,
                                            Time = DateTime.SpecifyKind(item["time"].ToObject<DateTime>().AddHours(-(this.AccountInfo == null ? 0.0 : this.AccountInfo.TimezoneOffset)), DateTimeKind.Utc),
                                            Open = item["open"].ToObject<decimal>(),
                                            High = item["high"].ToObject<decimal>(),
                                            Low = item["low"].ToObject<decimal>(),
                                            Close = item["close"].ToObject<decimal>(),
                                            TickVolume = item["tick_volume"].ToObject<int>()
                                        };
                                        // Update the previous values
                                        if (!HistoricBarData[instrument].BarData.Any(f => f.Time == obj.Time && f.Timeframe == obj.Timeframe))
                                        {
                                            HistoricBarData[instrument].BarData.Add(obj);
                                        }
                                    }
                                    else
                                    {
                                        var hbd = new HistoricBarData();
                                        hbd.BarData.Add(new BarData
                                        {
                                            Timeframe = timeframe,
                                            Time = DateTime.SpecifyKind(item["time"].ToObject<DateTime>().AddHours(-(this.AccountInfo == null ? 0.0 : this.AccountInfo.TimezoneOffset)), DateTimeKind.Utc),
                                            Open = item["open"].ToObject<decimal>(),
                                            High = item["high"].ToObject<decimal>(),
                                            Low = item["low"].ToObject<decimal>(),
                                            Close = item["close"].ToObject<decimal>(),
                                            TickVolume = item["tick_volume"].ToObject<int>()
                                        });
                                        HistoricBarData.Add(instrument, hbd);
                                    }
                                }

                                // Trigger event
                                OnHistoricBarDataEvent?.Invoke(ClientId, instrument, timeframe);
                            }
                        }
                    }

                    foreach (var valueBD in HistoricBarData.Values)
                    {
                        valueBD.BarData = [.. valueBD.BarData.OrderBy(f => f.Time)];
                    }

                    // Delete file
                    await TryDeleteFileAsync(pathHistoricBarData);

                    // Signal that the current dataOrders processing is complete
                    _resetHistoricBarDataEvent.Set();
                }
            }
        }



        /// <summary>
        /// Loads stored orders from file (in case of a restart). 
        /// </summary>
        private async Task LoadDataAsync()
        {

            string ordersStored = await TryReadFileAsync(pathOrdersStored);
            string tradesStored = await TryReadFileAsync(pathDealsStored);

            if (ordersStored.Length != 0)
            {
                JObject dataOrders;

                try { dataOrders = JObject.Parse(ordersStored); }
                catch { return; }
                if (dataOrders == null) { return; }

                // Set the orders stored as "last open order" to make sure the events are still working
                lastOpenOrdersStr = ordersStored;

                // Parse the open orders
                OpenOrders = dataOrders["orders"]?.ToObject<Dictionary<long, Order>>();

                // InitAndStart the open orders
                if (OpenOrders == null)
                    OpenOrders = new Dictionary<long, Order>();

                // Parse the account info
                AccountInfo = dataOrders["account_info"]?.ToObject<AccountInfo>();

                // Set the dates in UTC
                foreach (var order in OpenOrders)
                {
                    order.Value.OpenTime = DateTime.SpecifyKind(order.Value.OpenTime.AddHours(-(this.AccountInfo == null ? 0.0 : this.AccountInfo.TimezoneOffset)), DateTimeKind.Utc);
                }
            }

            if (tradesStored.Length != 0)
            {
                JObject dataTrades;

                try { dataTrades = JObject.Parse(tradesStored); }
                catch { return; }
                if (dataTrades == null) { return; }

                // Set the deal stored as "last deals" to make sure the events are still working
                lastDealsStr = tradesStored;

                // Parse it to objects
                var trades = JsonConvert.DeserializeObject<Dictionary<long, Deal>>(tradesStored);

                // Do null reference check
                if (trades != null)
                    Deals = trades;
                else
                    Deals = [];
            }
        }


        /// <summary>
        /// Loads stored messages from file (in case of a restart).
        /// </summary>
        private async Task LoadLogsAsync()
        {

            string text = await TryReadFileAsync(pathMessagesStored);

            if (text.Length == 0)
                return;

            JObject data;

            try
            {
                data = JObject.Parse(text);
            }
            catch (Exception e)
            {
                Print(e.ToString());
                return;
            }

            if (data == null)
                return;

            lastMessagesStr = text;

            //here we don't have to sort because we just need the latest millis items. 
            foreach (var x in data)
            {
                long millis = Int64.Parse(x.Key);
                if (millis > lastMessageId)
                    lastMessageId = millis;
            }
        }


        /// <summary>
        /// Sends a SUBSCRIBE_SYMBOLS command to subscribe to market (tick) dataOrders.
        /// </summary>
        /// <param name="symbols"> List of pairs to subscribe to.</param>
        public void SubscribeForTicks(List<string> symbols)
        {
            SendCommand("SUBSCRIBE_SYMBOLS", string.Join(",", symbols.Distinct().ToList()));
        }

        /// <summary>
        /// Sends a SUBSCRIBE_SYMBOLS_BAR_DATA command to subscribe to bar dataOrders.
        /// </summary>
        /// <param name="symbols"> List of lists containing symbol/time frame combinations to subscribe to.For example: { "EURUSD", "M1" }, { "USDJPY", "H1" } };</param>
        public void SubscribeForBarData(List<KeyValuePair<string, string>> symbols)
        {
            string content = "";
            for (int i = 0; i < symbols.Count; i++)
            {
                if (i != 0) content += ",";
                content += symbols[i].Key + "," + symbols[i].Value;
            }
            SendCommand("SUBSCRIBE_SYMBOLS_BAR_DATA", content);
        }

        /// <summary>
        /// Sends a GET_HISTORIC_DATA command to request historic dataOrders.
        /// </summary>
        /// <param name="pairs"> Symbol to get historic dataOrders</param>
        public void GetHistoricData(List<Pairs> pairs)
        {
            Thread thread = new(() =>
            {
                int maxRetries = 5; // Max retries to wait for AccountInfo
                int retryCount = 0;

                while (AccountInfo == null && retryCount < maxRetries)
                {
                    Thread.Sleep(2000); // Wait for 1 second before retrying
                    retryCount++;
                }

                foreach (var sym in pairs)
                {
                    // Determine the duration of a single bar in minutes
                    int durationInMinutes = sym.Timeframe switch
                    {
                        "M1" => 1,
                        "M2" => 2,
                        "M3" => 3,
                        "M4" => 4,
                        "M5" => 5,
                        "M6" => 6,
                        "M10" => 10,
                        "M12" => 12,
                        "M15" => 15,
                        "M20" => 20,
                        "M30" => 30,
                        "H1" => 60,
                        "H2" => 120,
                        "H3" => 180,
                        "H4" => 240,
                        "H6" => 360,
                        "H8" => 480,
                        "H12" => 720,
                        "D1" => 1440,
                        "W1" => 10080,
                        "MN1" => 43200, // Assuming 30 days in a month for simplicity
                        _ => throw new ArgumentException("Unknown timeframe", nameof(MetatraderApi)),
                    };

                    // Calculate total duration for all bars
                    int totalDurationInMinutes = sym.NumberOfHistoricalBarsRequested * durationInMinutes;

                    // Calculate and return the start date
                    var startDate = DateTime.UtcNow.AddMinutes(-totalDurationInMinutes);

                    // Send the command
                    GetHistoricData(sym.TickerInMetatrader, sym.Timeframe, startDate.AddHours(this.AccountInfo == null ? 0.0 : this.AccountInfo.TimezoneOffset).AddDays(-1), DateTimeOffset.UtcNow.AddHours(this.AccountInfo == null ? 0.0 : this.AccountInfo.TimezoneOffset));

                    // Wait for the event to be triggered
                    _resetHistoricBarDataEvent.WaitOne();

                    // Sleep 500ms
                    Thread.Sleep(500);
                }
            });
            thread.Start();
        }

        /// <summary>
        /// Sends a GET_HISTORIC_DATA command to request historic dataOrders.
        /// </summary>
        /// <param name="symbol"> Symbol to get historic dataOrders</param>
        /// <param name="timeFrame">Time frame for the requested dataOrders</param>
        /// <param name="start">StartCheckTimeAndExecuteOnceDaily timestamp (seconds since epoch) of the requested dataOrders</param>
        /// <param name="end">End timestamp of the requested dataOrders</param>
        public void GetHistoricData(string symbol, string timeFrame, DateTimeOffset start, DateTimeOffset end)
        {
            string content = symbol + "," + timeFrame + "," + start.ToUnixTimeSeconds() + "," + end.ToUnixTimeSeconds();
            SendCommand("GET_HISTORIC_DATA", content);
        }


        /// <summary>
        /// Sends an OPEN_ORDER command to open an order.
        /// </summary>
        /// <param name="symbol">Symbol for which an order should be opened.</param>
        /// <param name="orderType"> Order type. Can be one of: 'buy', 'sell', 'buylimit', 'selllimit', 'buystop', 'sellstop'</param>
        /// <param name="lots">Volume in lots</param>
        /// <param name="price">Ask of the (pending) order. Can be zero for market orders.</param>
        /// <param name="stopLoss">SL as absoute price. Can be zero if the order should not have an SL. </param>
        /// <param name="takeProfit"> TP as absoute price. Can be zero if the order should not have a TP.  </param>
        /// <param name="magic">TvMagic number</param>
        /// <param name="comment">Order comment</param>
        /// <param name="expiration"> Expiration time given as timestamp in seconds. Can be zero if the order should not have an expiration time.  </param>
        public void ExecuteOrder(string symbol, OrderType orderType, decimal lots, decimal price, decimal stopLoss, decimal takeProfit, int magic = 0, string? comment = null, long expiration = 0)
        {
            string orderT = GetDescription(orderType);

            string content = $"{symbol},{orderT},{Format(lots)},{Format(price)},{Format(stopLoss)},{Format(takeProfit)},{magic},{comment ?? string.Empty},{expiration}";
            SendCommand("OPEN_ORDER", content);
        }


        /// <summary>
        /// Sends a MODIFY_ORDER command to modify an order.
        /// </summary>
        /// <param name="ticket">Ticket of the order that should be modified</param>
        /// <param name="lots">Volume in lots</param>
        /// <param name="price">Ask of the (pending) order. Non-zero only works for pending orders</param>
        /// <param name="stopLoss">New stop loss price</param>
        /// <param name="takeProfit">New take profit price</param>
        /// <param name="magic">TvMagic number</param>
        /// <param name="expiration">New expiration time given as timestamp in seconds. Can be zero if the order should not have an expiration time</param>
        public void ModifyOrder(long ticket, decimal lots, decimal price, decimal stopLoss, decimal takeProfit, int magic = -1, long expiration = 0)
        {
            string content = $"{ticket},{Format(lots)},{Format(price)},{Format(stopLoss)},{Format(takeProfit)},{magic},{expiration}";
            SendCommand("MODIFY_ORDER", content);
        }


        /// <summary>
        /// Sends a CLOSE_ORDER command to close an order.
        /// </summary>
        /// <param name="ticket">Ticket of the order that should be closed.</param>
        /// <param name="lots"> Volume in lots. If lots=0 it will try to close the complete position</param>
        /// <param name="magic">TvMagic number</param>
        public void CloseOrder(long ticket, double lots = 0, int magic = -1)
        {
            string content = $"{ticket},{Format(lots)},{magic}";
            SendCommand("CLOSE_ORDER", content);
        }


        /// <summary>
        /// Sends a CLOSE_ALL_ORDERS command to close all orders with a given symbol.
        /// </summary>
        public void CloseAllOrders()
        {
            SendCommand("CLOSE_ALL_ORDERS", "");
        }


        /// <summary>
        /// Sends a CLOSE_ORDERS_BY_SYMBOL command to close all orders  with a given symbol.
        /// </summary>
        /// <param name="symbol"> Symbol for which all orders should be closed. </param>
        /// <param name="magic">TvMagic number</param>
        public void CloseOrdersBySymbol(string symbol, int magic = -1)
        {
            string content = $"{symbol},{magic}";
            SendCommand("CLOSE_ORDERS_BY_SYMBOL", content);
        }



        /// <summary>
        /// Sends a CLOSE_ORDERS_BY_MAGIC command to close all orders with a given magic number.
        /// </summary>
        /// <param name="magic"> TvMagic number for which all orders should be closed</param>
        public void CloseOrdersByMagic(int magic)
        {
            SendCommand("CLOSE_ORDERS_BY_MAGIC", magic.ToString());
        }

        /*Sends a RESET_COMMAND_IDS command to reset stored command IDs. 
        This should be used when restarting the java side without restarting 
        the mql side.
        */
        /// <summary>
        /// Sends a RESET_COMMAND_IDS command to reset stored command IDs. This should be used when restarting the java side without restarting  the mql side.
        /// </summary>
        public async Task ResetCommandIDsAsync()
        {
            commandID = 0;

            SendCommand("RESET_COMMAND_IDS", "");

            // sleep to make sure it is read before other commands.
            await Task.Delay(500);
        }


        /// <summary>
        /// Sends a command to the mql server by writing it to one of the command files. Multiple command files are used to allow for fast execution  of multiple commands in the correct chronological order.
        /// </summary>
        private void SendCommand(string command, string content)
        {
            // Need lock so that different threads do not use the same 
            // commandID or write at the same time.
            lock (this)
            {
                commandID = (commandID + 1) % 100000;

                string text = "<:" + commandID + "|" + command + "|" + content + ":>";

                DateTime now = DateTime.UtcNow;
                DateTime endTime = DateTime.UtcNow + new TimeSpan(0, 0, maxRetryCommandSeconds);

                // trying again for X seconds in case all files exist or are 
                // currently read from mql side. 
                while (now < endTime)
                {
                    // using 10 different files to increase the execution speed 
                    // for muliple commands. 
                    bool success = false;
                    for (int i = 0; i < maxCommandFiles; i++)
                    {
                        string filePath = pathCommandsPrefix + i + ".txt";
                        if (!File.Exists(filePath) && TryWriteToFile(filePath, text))
                        {
                            success = true;
                            break;
                        }
                    }
                    if (success) break;
                    Thread.Sleep(sleepDelay);
                    now = DateTime.UtcNow;
                }
            }
        }

    }
}
