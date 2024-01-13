using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace JCTG.AzureFunction.Functions
{
    public class Log(ILoggerFactory loggerFactory, JCTGDbContext dbContext)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<Log>();
        private readonly JCTGDbContext _dbContext = dbContext;

        [Function("Log")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            // Check for the custom query parameter
            var queryParameters = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var source = queryParameters["source"];

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            if (source == "timer")
            {
                _logger.LogDebug("Call received from Timer Triggered function");
            }
            else
            {

                // Read body from request
                string jsonString = await new StreamReader(req.Body).ReadToEndAsync();

                // TradeJournal item
                _logger.LogDebug($"Request body : {jsonString}");

                try
                {
                    // Parse object
                    var logItem = JsonConvert.DeserializeObject<LogRequest>(jsonString);

                    // Do null reference check
                    if (logItem != null)
                    {
                        // TradeJournal item
                        _logger.LogInformation($"Parsed Log object : AccountID={logItem.AccountID}, ClientID={logItem.ClientID}, Type={logItem.Type}, ErrorType={logItem.ErrorType}", jsonString);

                        // Add item to the database
                        var log = new JCTG.Log
                        {
                            AccountID = logItem.AccountID,
                            ClientID = logItem.ClientID,
                            DateCreated = DateTime.UtcNow,
                            ErrorType = logItem.ErrorType,
                            Message = logItem.Message,
                            Type = logItem.Type,
                        };

                        // Add to database
                        await _dbContext.Log.AddAsync(log);

                        // Update database
                        await _dbContext.SaveChangesAsync();
                    }

                }
                catch (Exception ex)
                {
                    // TradeJournal item
                    _logger.LogError($"Object: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
                }
            }

            return response;
        }
    }
}
