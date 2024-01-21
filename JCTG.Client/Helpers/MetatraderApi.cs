using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static JCTG.Client.Helpers;


namespace JCTG.Client
{
    public class MetatraderApi
    {
        private readonly string MetaTraderDirPath;
        private readonly int sleepDelay;
        private readonly int maxRetryCommandSeconds;
        private readonly bool loadOrdersFromFile;
        private readonly bool verbose;

        private readonly string pathOrders;
        private readonly string pathMessages;
        private readonly string pathMarketData;
        private readonly string pathBarData;
        private readonly string pathHistoricData;
        private readonly string pathHistoricTrades;
        private readonly string pathOrdersStored;
        private readonly string pathMessagesStored;
        private readonly string pathCommandsPrefix;

        private readonly int maxCommandFiles = 20;
        private int commandID = 0;
        private long lastMessageId = 0;
        private string lastOpenOrdersStr = "";
        private string lastMessagesStr = "";
        private string lastMarketDataStr = "";
        private string lastBarDataStr = "";
        private string lastHistoricDataStr = "";
        private string lastHistoricTradesStr = "";


        public Dictionary<long, Order> OpenOrders { get; set; }
        public AccountInfo? AccountInfo { get; set; }
        public Dictionary<string, MarketData> MarketData { get; set; }
        public Dictionary<string, BarData> BarData { get; set; }


        public JObject HistoricData = new JObject();
        public Dictionary<string, TradeData> Trades { get; set; }
        public long ClientId { get; set; }

        public bool ACTIVE = true;
        private bool START = false;

        private Thread? openOrdersThread;
        private Thread? messageThread;
        private Thread? marketDataThread;
        private Thread? barDataThread;
        private Thread? historicDataThread;

        // Define the delegate for the event
        public delegate void OnOrderCreateEventHandler(long clientId, long ticketId, Order order);
        public event OnOrderCreateEventHandler? OnOrderCreateEvent;

        public delegate void OnOrderUpdateEventHandler(long clientId, long ticketId, Order order);
        public event OnOrderUpdateEventHandler? OnOrderUpdateEvent;

        public delegate void OnOrderRemoveEventHandler(long clientId, long ticketId, Order order);
        public event OnOrderRemoveEventHandler? OnOrderRemoveEvent;

        public delegate void OnLogEventHandler(long clientId, long id, Log log);
        public event OnLogEventHandler? OnLogEvent;

        public delegate void OnTickEventHandler(long clientId, string symbol, decimal bid, decimal ask, decimal tickValue);
        public event OnTickEventHandler? OnTickEvent;

        public delegate void OnBarDataEventHandler(long clientId, string symbol, string timeFrame, DateTime time, decimal open, decimal high, decimal low, decimal close, int tickVolume);
        public event OnBarDataEventHandler? OnBarDataEvent;

        public delegate void OnHistoricDataEventHandler(long clientId, string symbol, string timeFrame, JObject data);
        public event OnHistoricDataEventHandler? OnTradeDataEvent;

        public delegate void OnTradeEventHandler(long clientId);
        public event OnTradeEventHandler? OnTradeEvent;


        public MetatraderApi(string MetaTraderDirPath, long clientId, int sleepDelay, int maxRetryCommandSeconds, bool loadOrdersFromFile, bool verbose)
        {
            this.MetaTraderDirPath = MetaTraderDirPath;
            this.ClientId = clientId;
            this.sleepDelay = sleepDelay;
            this.maxRetryCommandSeconds = maxRetryCommandSeconds;
            this.loadOrdersFromFile = loadOrdersFromFile;
            this.verbose = verbose;

            if (!Directory.Exists(MetaTraderDirPath))
            {
                Print("ERROR: MetaTraderDirPath does not exist! MetaTraderDirPath: " + MetaTraderDirPath);
                Environment.Exit(1);
            }

            this.pathOrders = Path.Join(MetaTraderDirPath, "DWX", "DWX_Orders.txt");
            this.pathMessages = Path.Join(MetaTraderDirPath, "DWX", "DWX_Messages.txt");
            this.pathMarketData = Path.Join(MetaTraderDirPath, "DWX", "DWX_Market_Data.txt");
            this.pathBarData = Path.Join(MetaTraderDirPath, "DWX", "DWX_Bar_Data.txt");
            this.pathHistoricData = Path.Join(MetaTraderDirPath, "DWX", "DWX_Historic_Data.txt");
            this.pathHistoricTrades = Path.Join(MetaTraderDirPath, "DWX", "DWX_Historic_Trades.txt");
            this.pathOrdersStored = Path.Join(MetaTraderDirPath, "DWX", "DWX_Orders_Stored.txt");
            this.pathMessagesStored = Path.Join(MetaTraderDirPath, "DWX", "DWX_Messages_Stored.txt");
            this.pathCommandsPrefix = Path.Join(MetaTraderDirPath, "DWX", "DWX_Commands_");
        }


        /// <summary>
        /// can be used to check if the client has been initialized.  
        /// </summary>
        public async Task StartAsync()
        {
            await LoadLogsAsync();

            if (loadOrdersFromFile)
                await LoadDataAsync();

            // Start the thread to run the asynchronous method
            this.openOrdersThread = new Thread(async () => await CheckOpenOrdersAsync());
            this.openOrdersThread?.Start();

            this.messageThread = new Thread(async () => await CheckMessagesAsync());
            this.messageThread?.Start();

            this.marketDataThread = new Thread(async () => await CheckMarketDataAsync());
            this.marketDataThread?.Start();

            this.barDataThread = new Thread(async () => await CheckBarDataAsync());
            this.barDataThread?.Start();

            this.historicDataThread = new Thread(async () => await CheckHistoricDataAsync());
            this.historicDataThread?.Start();

            await ResetCommandIDsAsync();

            START = true;
        }



        /// <summary>
        /// Regularly checks the file for open orders and triggers the eventHandler.OnOrderCreateEvent() function.
        /// </summary>
        private async Task CheckOpenOrdersAsync()
        {
            while (ACTIVE)
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


                // Assuming 'data' is the JObject that contains your JSON data
                JObject ordersData = (JObject)data["orders"];

                // If market data is null -> create new instance
                if (OpenOrders == null)
                    OpenOrders = new Dictionary<long, Order>();

                // Iterate over each order in the JSON
                if (ordersData != null)
                {
                    // Next, handle removal of orders not in ordersData
                    List<long> ordersToRemove = new List<long>();
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
                                    OpenTime = DateTime.ParseExact(value["open_time"].ToString(), "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture),
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
                        // Invoke the event
                        OnOrderRemoveEvent?.Invoke(ClientId, orderId, OpenOrders[orderId]);

                        // Remove the order from the list
                        OpenOrders.Remove(orderId);
                    }
                }


                // Cast account info
                AccountInfo = data["account_info"]?.ToObject<AccountInfo>();

                if (loadOrdersFromFile)
                    await TryWriteToFileAsync(pathOrdersStored, data.ToString());
            }
        }

        /// <summary>
        /// Regularly checks the file for messages and triggers the eventHandler.OnMessage() function.
        /// </summary>
        private async Task CheckMessagesAsync()
        {
            while (ACTIVE)
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

                // Convert JObject to a list of Log objects
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
        /// Regularly checks the file for market data and triggers the eventHandler.OnTick() function.
        /// </summary>
        private async Task CheckMarketDataAsync()
        {
            while (ACTIVE)
            {
                // Sleep
                Thread.Sleep(sleepDelay);

                // If not started -> Skip
                if (!START)
                    continue;

                // Parse MT4 text file
                string text = await TryReadFileAsync(pathMarketData);

                // Check if the file is changed in regards to the previous version
                if (text.Length == 0 || text.Equals(lastMarketDataStr))
                    continue;

                // Save file
                lastMarketDataStr = text;


                var _marketData = JsonConvert.DeserializeObject<Dictionary<string, MarketData>>(lastMarketDataStr);

                // If market data is null -> create new instance
                if (MarketData == null)
                    MarketData = new Dictionary<string, MarketData>();

                // Foreach property of the data
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
                                if (property.Value.Bid != previousData.Bid || property.Value.Ask != previousData.Ask || property.Value.TickValue != previousData.TickValue)
                                {
                                    // Invoke the event
                                    OnTickEvent?.Invoke(ClientId, property.Key, property.Value.Bid, property.Value.Ask, property.Value.TickValue);
                                }
                            }
                            else
                            {
                                // If it's a new ticker, add it to the dictionary and invoke the event
                                MarketData.Add(property.Key, property.Value);
                                OnTickEvent?.Invoke(ClientId, property.Key, property.Value.Bid, property.Value.Ask, property.Value.TickValue);
                            }
                        }
                    }
                }
                else
                {

                }
            }
        }

        /// <summary>
        /// Regularly checks the file for bar data and triggers the eventHandler.OnBarData() function.
        /// </summary>
        private async Task CheckBarDataAsync()
        {
            while (ACTIVE)
            {
                Thread.Sleep(sleepDelay);

                if (!START)
                    continue;

                string text = await TryReadFileAsync(pathBarData);

                if (text.Length == 0 || text.Equals(lastBarDataStr))
                    continue;

                lastBarDataStr = text;

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

                if (BarData == null)
                    BarData = new Dictionary<string, BarData>();

                foreach (var property in data.Properties())
                {
                    var value = property.Value as JObject;
                    if (value != null && value["time"] != null && value["open"] != null && value["high"] != null && value["low"] != null && value["close"] != null && value["tick_volume"] != null)
                    {
                        string[] stSplit = property.Name.Split("_");
                        if (stSplit.Length != 2)
                            continue;

                        var newBarData = new BarData
                        {
                            Timeframe = stSplit[1],
                            Time = value["time"].ToObject<DateTime>(),
                            Open = value["open"].ToObject<decimal>(),
                            High = value["high"].ToObject<decimal>(),
                            Low = value["low"].ToObject<decimal>(),
                            Close = value["close"].ToObject<decimal>(),
                            TickVolume = value["tick_volume"].ToObject<int>()
                        };

                        // Check if the ticker already has previous values
                        if (BarData.TryGetValue(property.Name, out var previousData))
                        {
                            // Update the previous values
                            BarData[property.Name] = new BarData
                            {
                                Timeframe = newBarData.Timeframe,
                                Time = newBarData.Time,
                                Open = newBarData.Open,
                                High = newBarData.High,
                                Low = newBarData.Low,
                                Close = newBarData.Close,
                                TickVolume = newBarData.TickVolume

                            };
                            // Check if the values have changed
                            if (newBarData.Timeframe != previousData.Timeframe || newBarData.Time != previousData.Time || newBarData.Open != previousData.Open || newBarData.High != previousData.High || newBarData.Low != previousData.Low || newBarData.Close != previousData.Close)
                            {
                                // Invoke the event
                                OnBarDataEvent?.Invoke(ClientId, property.Name, newBarData.Timeframe, newBarData.Time, newBarData.Open, newBarData.High, newBarData.Low, newBarData.Close, newBarData.TickVolume);
                            }
                        }
                        else
                        {
                            // If it's a new ticker, add it to the dictionary and invoke the event
                            BarData.Add(property.Name, new BarData
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
                            OnBarDataEvent?.Invoke(ClientId, property.Name, newBarData.Timeframe, newBarData.Time, newBarData.Open, newBarData.High, newBarData.Low, newBarData.Close, newBarData.TickVolume);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Regularly checks the file for historic data and triggers the eventHandler.OnHistoricData() function.
        /// </summary>
        private async Task CheckHistoricDataAsync()
        {
            while (ACTIVE)
            {

                Thread.Sleep(sleepDelay);

                if (!START)
                    continue;

                string text = await TryReadFileAsync(pathHistoricData);

                if (text.Length > 0 && !text.Equals(lastHistoricDataStr))
                {
                    lastHistoricDataStr = text;

                    JObject data;

                    try
                    {
                        data = JObject.Parse(text);
                    }
                    catch
                    {
                        data = null;
                    }

                    if (data != null)
                    {
                        foreach (var x in data)
                        {
                            HistoricData[x.Key] = data[x.Key];
                        }

                        //TryDeleteFile(pathHistoricData);

                        if (OnTradeDataEvent != null)
                        {
                            foreach (var x in data)
                            {
                                string st = x.Key;
                                string[] stSplit = st.Split("_");
                                if (stSplit.Length != 2)
                                    continue;
                                // JObject jo = (JObject)BarData[symbol];
                                OnTradeDataEvent?.Invoke(ClientId, stSplit[0], stSplit[1], (JObject)data[x.Key]);
                            }
                        }
                    }


                }

                // Read file
                text = await TryReadFileAsync(pathHistoricTrades);

                // Make sure the import is new
                if (text.Length > 0 && !text.Equals(lastHistoricTradesStr))
                {
                    // Set new text file to the variable
                    lastHistoricTradesStr = text;

                    // Deserialize
                    var data = JsonConvert.DeserializeObject<Dictionary<string, TradeData>>(text);

                    // Null reference check
                    if (data != null)
                    {
                        // Set the objects to the property
                        Trades = data;

                        // Delete the file on the disk
                        //TryDeleteFile(pathHistoricTrades);

                        // Invoke event
                        OnTradeEvent?.Invoke(ClientId);
                    }
                }
            }
        }



        /// <summary>
        /// Loads stored orders from file (in case of a restart). 
        /// </summary>
        private async Task LoadDataAsync()
        {

            string text = await TryReadFileAsync(pathOrdersStored);

            if (text.Length == 0)
                return;

            JObject data;

            try
            {
                data = JObject.Parse(text);
            }
            catch
            {
                return;
            }

            if (data == null)
                return;

            lastOpenOrdersStr = text;
            OpenOrders = data["orders"]?.ToObject<Dictionary<long, Order>>();
            if (OpenOrders == null)
                OpenOrders = new Dictionary<long, Order>();
            AccountInfo = data["account_info"]?.ToObject<AccountInfo>();
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

            //here we don't have to sort because we just need the latest millis value. 
            foreach (var x in data)
            {
                long millis = Int64.Parse(x.Key);
                if (millis > lastMessageId)
                    lastMessageId = millis;
            }
        }


        /// <summary>
        /// Sends a SUBSCRIBE_SYMBOLS command to subscribe to market (tick) data.
        /// </summary>
        /// <param name="symbols"> List of symbols to subscribe to.</param>
        public void SubscribeForTicks(List<string> symbols)
        {
            SendCommand("SUBSCRIBE_SYMBOLS", string.Join(",", symbols.Distinct().ToList()));
        }

        /// <summary>
        /// Sends a SUBSCRIBE_SYMBOLS_BAR_DATA command to subscribe to bar data.
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
        /// Sends a GET_HISTORIC_DATA command to request historic data.
        /// </summary>
        /// <param name="symbol"> Symbol to get historic data</param>
        /// <param name="timeFrame">Time frame for the requested data</param>
        /// <param name="start">Start timestamp (seconds since epoch) of the requested data</param>
        /// <param name="end">End timestamp of the requested data</param>
        public void GetHistoricData(string symbol, string timeFrame, DateTimeOffset start, DateTimeOffset end)
        {
            string content = symbol + "," + timeFrame + "," + start.ToUnixTimeSeconds() + "," + end.ToUnixTimeSeconds();
            SendCommand("GET_HISTORIC_DATA", content);
        }



        /// <summary>
        /// Sends a GET_HISTORIC_TRADES command to request historic trades. The data will be stored in HistoricTrades.  On receiving the data the eventHandler.OnHistoricTrades()  function will be triggered.
        /// </summary>
        /// <param name="lookbackDays"> lookbackDays (int): Days to look back into the trade history.  The history must also be visible in MT4. </param>
        public void GetHistoricTrades(int lookbackDays)
        {
            SendCommand("GET_HISTORIC_TRADES", lookbackDays.ToString());
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
        /// <param name="magic">Magic number</param>
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
        /// <param name="expiration">New expiration time given as timestamp in seconds. Can be zero if the order should not have an expiration time</param>
        public void ModifyOrder(long ticket, decimal lots, decimal price, decimal stopLoss, decimal takeProfit, long expiration = 0)
        {
            string content = $"{ticket},{Format(lots)},{Format(price)},{Format(stopLoss)},{Format(takeProfit)},{expiration}";
            SendCommand("MODIFY_ORDER", content);
        }


        /// <summary>
        /// Sends a CLOSE_ORDER command to close an order.
        /// </summary>
        /// <param name="ticket">Ticket of the order that should be closed.</param>
        /// <param name="lots"> Volume in lots. If lots=0 it will try to close the complete position</param>
        public void CloseOrder(long ticket, double lots = 0)
        {
            string content = ticket + "," + Format(lots);
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
        public void CloseOrdersBySymbol(string symbol)
        {
            SendCommand("CLOSE_ORDERS_BY_SYMBOL", symbol);
        }



        /// <summary>
        /// Sends a CLOSE_ORDERS_BY_MAGIC command to close all orders with a given magic number.
        /// </summary>
        /// <param name="magic"> Magic number for which all orders should be closed</param>
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
