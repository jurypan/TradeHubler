using System.Collections;
using Newtonsoft.Json.Linq;
using static JCTG.Client.Helpers;


namespace JCTG.Client
{
    public class MetatraderApi
    {
        private string MetaTraderDirPath;  // { get; private set; }
        private int sleepDelay;
        private int maxRetryCommandSeconds;
        private bool loadOrdersFromFile;
        private bool verbose;

        private string pathOrders;
        private string pathMessages;
        private string pathMarketData;
        private string pathBarData;
        private string pathHistoricData;
        private string pathHistoricTrades;
        private string pathOrdersStored;
        private string pathMessagesStored;
        private string pathCommandsPrefix;

        private int maxCommandFiles = 20;
        private int commandID = 0;
        private long lastMessagesMillis = 0;
        private string lastOpenOrdersStr = "";
        private string lastMessagesStr = "";
        private string lastMarketDataStr = "";
        private string lastBarDataStr = "";
        private string lastHistoricDataStr = "";
        private string lastHistoricTradesStr = "";

        public JObject OpenOrders = new JObject();
        public JObject AccountInfo = new JObject();
        public JObject MarketData = new JObject();
        public JObject BarData = new JObject();
        public JObject HistoricData = new JObject();
        public JObject HistoricTrades = new JObject();
        public JObject lastBarData = new JObject();
        public JObject lastMarketData = new JObject();

        public bool ACTIVE = true;
        private bool START = false;

        private Thread? openOrdersThread;
        private Thread? messageThread;
        private Thread? marketDataThread;
        private Thread? barDataThread;
        private Thread? historicDataThread;
        private Thread? timerThread;

        // Define the delegate for the event
        public delegate void OnOrderEventHandler();
        public event OnOrderEventHandler? OnOrderEvent;

        public delegate void OnMessageEventHandler(JObject message);
        public event OnMessageEventHandler? OnMessageEvent;

        public delegate void OnTickEventHandler(string symbol, double bid, double ask);
        public event OnTickEventHandler? OnTickEvent;

        public delegate void OnBarDataEventHandler(string symbol, string timeFrame, string time, double open, double high, double low, double close, int tickVolume);
        public event OnBarDataEventHandler? OnBarDataEvent;

        public delegate void OnHistoricDataEventHandler(string symbol, string timeFrame, JObject data);
        public event OnHistoricDataEventHandler? OnHistoricDataEvent;

        public delegate void OnHistoricTradeEventHandler();
        public event OnHistoricTradeEventHandler? OnHistoricTradeEvent;

        public delegate void OnTimerEventHandler();
        public event OnTimerEventHandler? OnTimerEvent;

        public MetatraderApi(string MetaTraderDirPath, int sleepDelay, int maxRetryCommandSeconds, bool loadOrdersFromFile, bool verbose)
        {
            this.MetaTraderDirPath = MetaTraderDirPath;
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
            await LoadMessagesAsync();

            if (loadOrdersFromFile)
                await LoadOrdersAsync();

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

            this.timerThread = new Thread(() => Timer());
            this.timerThread?.Start();

            await ResetCommandIDsAsync();

            START = true;
        }


        /// <summary>
        /// Regularly checks the file for open orders and triggers the eventHandler.OnOrderEvent() function.
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
                
				JObject dataOrders = (JObject)data["orders"];
				
                bool newEvent = false;
                foreach (var x in OpenOrders)
                {
                    // JToken value = x.Value;
                    if (dataOrders[x.Key] == null)
                    {
                        newEvent = true;
                        if (verbose)
                            Print("Order removed: " + OpenOrders[x.Key].ToString());
                    }
                }
                foreach (var x in dataOrders)
                {
                    // JToken value = x.Value;
                    if (OpenOrders[x.Key] == null)
                    {
                        newEvent = true;
                        if (verbose)
                            Print("New order: " + dataOrders[x.Key].ToString());
                    }
                }
				
                OpenOrders = dataOrders;
				AccountInfo = (JObject)data["account_info"];

                if (loadOrdersFromFile)
                    await TryWriteToFileAsync(pathOrdersStored, data.ToString());

                // Check if there are any subscribers
                if (OnOrderEvent != null && newEvent)
                {
                    // Raise the event
                    OnOrderEvent();
                }
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

                // var sortedObj = new JObject(data.Properties().OrderByDescending(p => (int)p.Value));

                // make sure that the message are sorted so that we don't miss messages because of (millis > lastMessagesMillis).
                ArrayList millisList = new ArrayList();

                foreach (var x in data)
                {
                    if (data[x.Key] != null)
                    {
                        millisList.Add(x.Key);
                    }
                }
                millisList.Sort();
                foreach (string millisStr in millisList)
                {
                    if (data[millisStr] != null) 
                    {
                        long millis = Int64.Parse(millisStr);
                        if (millis > lastMessagesMillis)
                        {
                            lastMessagesMillis = millis;

                            // Check if there are any subscribers
                            // Raise the event
                            OnMessageEvent?.Invoke((JObject)data[millisStr]);
                        }
                    }
                }
                await TryWriteToFileAsync(pathMessagesStored, data.ToString());
            }
        }

        /// <summary>
        /// Regularly checks the file for market data and triggers the eventHandler.OnTick() function.
        /// </summary>
        private async Task CheckMarketDataAsync()
        {
            while (ACTIVE)
            {

                Thread.Sleep(sleepDelay);

                if (!START)
                    continue;

                string text = await TryReadFileAsync(pathMarketData);

                if (text.Length == 0 || text.Equals(lastMarketDataStr))
                    continue;

                lastMarketDataStr = text;

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

                MarketData = data;

                if (OnTickEvent != null)
                {
                    foreach (var x in MarketData)
                    {
                        string symbol = x.Key;
                        if (lastMarketData[symbol] == null || !MarketData[symbol].Equals(lastMarketData[symbol]))
                        {
                            OnTickEvent?.Invoke(symbol,  (double)MarketData[symbol]["bid"], (double)MarketData[symbol]["ask"]);
                        }
                    }
                }
                lastMarketData = data;
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

                BarData = data;

                if (OnBarDataEvent != null)
                {
                    foreach (var x in BarData)
                    {
                        string st = x.Key;
                        if (lastBarData[st] == null || !BarData[st].Equals(lastBarData[st]))
                        {
                            string[] stSplit = st.Split("_");
                            if (stSplit.Length != 2)
                                continue;
                            // JObject jo = (JObject)BarData[symbol];
                            OnBarDataEvent?.Invoke(stSplit[0], stSplit[1], 
                                                   (string)BarData[st]["time"], 
                                                   (double)BarData[st]["open"], 
                                                   (double)BarData[st]["high"], 
                                                   (double)BarData[st]["low"], 
                                                   (double)BarData[st]["close"], 
                                                   (int)BarData[st]["tick_volume"]);
                        }
                    }
                }
                lastBarData = data;
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

                        TryDeleteFile(pathHistoricData);

                        if (OnHistoricDataEvent != null)
                        {
                            foreach (var x in data)
                            {
                                string st = x.Key;
                                string[] stSplit = st.Split("_");
                                if (stSplit.Length != 2)
                                    continue;
                                // JObject jo = (JObject)BarData[symbol];
                                OnHistoricDataEvent?.Invoke(stSplit[0], stSplit[1], (JObject)data[x.Key]);
                            }
                        }
                    }

                    
                }

                // also check historic trades in the same thread. 
                text = await TryReadFileAsync(pathHistoricTrades);

                if (text.Length > 0 && !text.Equals(lastHistoricTradesStr))
                {
                    lastHistoricTradesStr = text;

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
                        HistoricTrades = data;

                        TryDeleteFile(pathHistoricTrades);

                        OnHistoricTradeEvent?.Invoke();
                    }
                }
            }
        }



        /// <summary>
        /// Regularly invokes this event
        /// </summary>
        private void Timer()
        {
            while (ACTIVE)
            {

                Thread.Sleep(sleepDelay);

                if (!START)
                    continue;

                OnTimerEvent?.Invoke();
            }
        }

        /// <summary>
        /// Loads stored orders from file (in case of a restart). 
        /// </summary>
        private async Task LoadOrdersAsync()
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
			OpenOrders = (JObject)data["orders"];
			AccountInfo = (JObject)data["account_info"];
        }


        /// <summary>
        /// Loads stored messages from file (in case of a restart).
        /// </summary>
        private async Task LoadMessagesAsync()
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

            // here we don't have to sort because we just need the latest millis value. 
            foreach (var x in data)
            {
                long millis = Int64.Parse(x.Key);
                if (millis > lastMessagesMillis)
                    lastMessagesMillis = millis;
            }
        }


        /// <summary>
        /// Sends a SUBSCRIBE_SYMBOLS command to subscribe to market (tick) data.
        /// </summary>
        /// <param name="symbols"> List of symbols to subscribe to.</param>
        public void SubscribeSymbols(string[] symbols)
        {
            SendCommand("SUBSCRIBE_SYMBOLS", string.Join(",", symbols));
        }

        /// <summary>
        /// Sends a SUBSCRIBE_SYMBOLS_BAR_DATA command to subscribe to bar data.
        /// </summary>
        /// <param name="symbols"> List of lists containing symbol/time frame combinations to subscribe to.For example: string[,] symbols = new string[,] { { "EURUSD", "M1" }, { "USDJPY", "H1" } };</param>
        public void SubscribeSymbolsBarData(string[,] symbols)
        {
            string content = "";
            for (int i = 0; i < symbols.GetLength(0); i++)
            {
                if (i != 0) content += ",";
                content += symbols[i, 0] + "," + symbols[i, 1];
            }
            SendCommand("SUBSCRIBE_SYMBOLS_BAR_DATA", content);
        }


        /// <summary>
        /// Sends a GET_HISTORIC_DATA command to request historic data.
        /// </summary>
        /// <param name="symbol"> Symbol to get historic data</param>
        /// <param name="timeFrame">Time frame for the requested data</param>
        /// <param name="start">StartAsync timestamp (seconds since epoch) of the requested data</param>
        /// <param name="end">End timestamp of the requested data</param>
        public void GetHistoricData(string symbol, string timeFrame, long start, long end)
		{
			string content = symbol + "," + timeFrame + "," + start + "," + end;
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
        /// <param name="price">Price of the (pending) order. Can be zero for market orders.</param>
        /// <param name="stopLoss">SL as absoute price. Can be zero if the order should not have an SL. </param>
        /// <param name="takeProfit"> TP as absoute price. Can be zero if the order should not have a TP.  </param>
        /// <param name="magic">Magic number</param>
        /// <param name="comment">Order comment</param>
        /// <param name="expiration"> Expiration time given as timestamp in seconds. Can be zero if the order should not have an expiration time.  </param>
        public void OpenOrder(string symbol, string orderType, double lots, double price, double stopLoss, double takeProfit, int magic, string comment, long expiration)
        {
            string content = symbol + "," + orderType + "," + Format(lots) + "," + Format(price) + "," + Format(stopLoss) + "," + Format(takeProfit) + "," + magic + "," + comment + "," + expiration;
            SendCommand("OPEN_ORDER", content);
        }


        /// <summary>
        /// Sends a MODIFY_ORDER command to modify an order.
        /// </summary>
        /// <param name="ticket">Ticket of the order that should be modified</param>
        /// <param name="lots">Volume in lots</param>
        /// <param name="price">Price of the (pending) order. Non-zero only works for pending orders</param>
        /// <param name="stopLoss">New stop loss price</param>
        /// <param name="takeProfit">New take profit price</param>
        /// <param name="expiration">New expiration time given as timestamp in seconds. Can be zero if the order should not have an expiration time</param>
        public void ModifyOrder(int ticket, double lots, double price, double stopLoss, double takeProfit, long expiration)
        {
            string content = ticket + "," + Format(lots) + "," + Format(price) + "," + Format(stopLoss) + "," + Format(takeProfit) + "," + expiration;
            SendCommand("MODIFY_ORDER", content);
        }


        /// <summary>
        /// Sends a CLOSE_ORDER command to close an order.
        /// </summary>
        /// <param name="ticket">Ticket of the order that should be closed.</param>
        /// <param name="lots"> Volume in lots. If lots=0 it will try to close the complete position</param>
        public void CloseOrder(int ticket, double lots=0)
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
