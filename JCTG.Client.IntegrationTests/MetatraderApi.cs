namespace JCTG.Client.Tests
{
    public class MetatraderApiTests
    {
        private MetatraderApi api;

        [SetUp]
        public void Setup()
        {
            var metaTraderDirPath = "C:\\Users\\joeri.pansaerts\\AppData\\Roaming\\MetaQuotes\\Terminal\\3B534B10135CFEDF8CD1AAB8BD994B13\\MQL4\\Files\\";
            var clientId = 2089100683;
            var sleepDelay = 5000;
            var maxRetryCommands = 10;
            var loadOrdersFromFile = true;
            var verbose = true;

            api = new MetatraderApi(metaTraderDirPath, clientId, sleepDelay, maxRetryCommands, loadOrdersFromFile, verbose);
        }

        [Test]
        public void Test1()
        {
            api.ExecuteOrder("EURUSD", OrderType.Buy, 1.00, 0, 123.12, 126.12, 456);
        }
    }
}