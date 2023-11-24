using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace JustCallTheGuy.Strategy2
{
    public class Metatrader
    {
        private readonly ILogger _logger;

        public Metatrader(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Metatrader>();
        }

        [Function("Strategy2_Metatrader")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogDebug(requestBody);

            try
            {

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
            }


            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            //response.WriteString("NONE,tp=,sl=");
            response.WriteString("BUY,tp=1234.12,sl=1234.4");
            return response;
        }
    }
}
