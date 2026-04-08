namespace DotNetTwitchBot.Application.Notifications
{
    /// <summary>
    /// Penguin dispatcher that handles both requests and notifications
    /// </summary>
    public class NotificationPublisher(IServiceProvider serviceProvider) : INotificationPublisher, IPenguinDispatcher
    {
        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        {
            var handlerType = typeof(INotificationHandler<>).MakeGenericType(typeof(TNotification));
            var handlers = serviceProvider.GetServices(handlerType);

            var tasks = handlers
                .Cast<INotificationHandler<TNotification>>()
                .Select(handler => handler.Handle(notification, cancellationToken));

            await Task.WhenAll(tasks);
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var requestType = request.GetType();
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));

            var handler = serviceProvider.GetRequiredService(handlerType);
            var method = handlerType.GetMethod("Handle");

            if (method == null)
                throw new InvalidOperationException($"Handler for {requestType.Name} does not have a Handle method");

            try
            {
                var result = method.Invoke(handler, new object[] { request, cancellationToken });

                if (result is Task<TResponse> task)
                    return await task;

                throw new InvalidOperationException($"Handler for {requestType.Name} did not return Task<{typeof(TResponse).Name}>");
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        public async Task Send(IRequest request, CancellationToken cancellationToken = default)
        {
            var requestType = request.GetType();
            var handlerType = typeof(IRequestHandler<>).MakeGenericType(requestType);

            var handler = serviceProvider.GetRequiredService(handlerType);
            var method = handlerType.GetMethod("Handle");

            if (method == null)
                throw new InvalidOperationException($"Handler for {requestType.Name} does not have a Handle method");

            try
            {
                var result = method.Invoke(handler, new object[] { request, cancellationToken });

                if (result is Task task)
                    await task;
                else
                    throw new InvalidOperationException($"Handler for {requestType.Name} did not return a Task");
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }
    }
}
