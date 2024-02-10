namespace JCTG.WebApp.Helpers
{
    public class WebsocketService(AzurePubSubClient client, JCTGDbContext dbContext)
    {
        public void Run()
        {
            client.OnOrderCreateEvent += (onOrderCreate) =>
            {
                if (onOrderCreate != null)
                {

                }
            };

            client.OnOrderUpdateEvent += (onOrderUpdate) =>
            {
                if (onOrderUpdate != null)
                {

                }
            };

            client.OnOrderCloseEvent += (onOrderClose) =>
            {
                if (onOrderClose != null)
                {

                }
            };

            client.OnLogEvent += (onLog) =>
            {
                if (onLog != null)
                {

                }
            };

            client.ListeningToServer();
        }
    }
}
