namespace JCTG.WebApp.Backend.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Log the exception
                // Respond with a generic error message if necessary
                Serilog.Log.ForContext<ExceptionHandlingMiddleware>().Error($"Exception: {ex.Message}\nInner exception message: {ex.InnerException?.Message}\n", ex);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("An unexpected error has occurred.");
            }
        }
    }
}
