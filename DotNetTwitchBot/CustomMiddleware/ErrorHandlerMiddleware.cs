namespace DotNetTwitchBot.CustomMiddleware
{
    public class ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (Exception)
            {
                logger.LogDebug("Exception happened in ErrorHandlerMiddleware. This is expected");
            }
        }
    }
}