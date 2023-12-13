namespace DotNetTwitchBot.CustomMiddleware
{
    public class ErrorHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlerMiddleware> _logger;

        public ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception)
            {
                //_logger.LogCritical(error, "Unhandled exception");
                //var response = context.Response;
                //response.ContentType = "application/json";
                //response.StatusCode = error switch
                //{
                //    AppException => (int)HttpStatusCode.BadRequest,// custom application error
                //    KeyNotFoundException => (int)HttpStatusCode.NotFound,// not found error
                //    _ => (int)HttpStatusCode.InternalServerError,// unhandled error
                //};
                //var result = JsonSerializer.Serialize(new { message = error?.Message });
                //await response.WriteAsync(result);
            }
        }
    }
}