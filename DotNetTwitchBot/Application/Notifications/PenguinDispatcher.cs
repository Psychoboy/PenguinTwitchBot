namespace DotNetTwitchBot.Application.Notifications
{
    public class PenguinDispatcher(IServiceProvider serviceProvider) : IPenguinDispatcher
    {
        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        {
            // Use runtime type instead of compile-time generic type to support polymorphism
            var notificationType = notification.GetType();
            var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);
            var handlers = serviceProvider.GetServices(handlerType);

            var tasks = new List<Task>();
            foreach (var handler in handlers)
            {
                var handleMethod = handlerType.GetMethod(nameof(INotificationHandler<INotification>.Handle));
                if (handleMethod != null)
                {
                    try
                    {
                        var task = (Task?)handleMethod.Invoke(handler, [notification, cancellationToken]);
                        if (task != null)
                        {
                            tasks.Add(task);
                        }
                    }
                    catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
                    {
                        // Unwrap and rethrow the original exception
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            // Use runtime type instead of compile-time generic type to support polymorphism
            var requestType = request.GetType();
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));

            var handler = serviceProvider.GetService(handlerType);
            if (handler == null)
            {
                throw new InvalidOperationException($"No handler registered for request type {requestType.Name}");
            }

            var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.Handle));
            if (handleMethod == null)
            {
                throw new InvalidOperationException($"Handle method not found on handler for request type {requestType.Name}");
            }

            Task<TResponse>? task;
            try
            {
                task = (Task<TResponse>?)handleMethod.Invoke(handler, [request, cancellationToken]);
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                // Unwrap and rethrow the original exception
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // This line is unreachable but needed for compiler
            }

            if (task == null)
            {
                throw new InvalidOperationException($"Handle method returned null for request type {requestType.Name}");
            }

            return await task;
        }
    }
}
