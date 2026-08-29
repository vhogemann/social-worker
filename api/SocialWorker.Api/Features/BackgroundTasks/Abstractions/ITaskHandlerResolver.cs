using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace SocialWorker.Api.Features.BackgroundTasks.Abstractions;

public interface ITaskHandlerResolver
{
    IBackgroundTaskHandler? ResolveHandler(IServiceProvider serviceProvider, string taskType);
    TimeSpan GetDefaultLeaseTimeout(string taskType);
}

public class TaskHandlerResolver : ITaskHandlerResolver
{
    private readonly Dictionary<string, Type> _handlerTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TimeSpan> _leaseTimeouts = new(StringComparer.OrdinalIgnoreCase);

    public TaskHandlerResolver()
    {
    }

    public TaskHandlerResolver(IEnumerable<IBackgroundTaskHandler> registeredHandlers)
    {
        foreach (var handler in registeredHandlers)
        {
            var type = handler.GetType();
            _handlerTypes[handler.TaskType] = type;
            _leaseTimeouts[handler.TaskType] = handler.DefaultLeaseTimeout;
        }
    }

    public TaskHandlerResolver Register<THandler>(string taskType, TimeSpan? defaultLease = null) where THandler : IBackgroundTaskHandler
    {
        _handlerTypes[taskType] = typeof(THandler);
        _leaseTimeouts[taskType] = defaultLease ?? TimeSpan.FromMinutes(5);
        return this;
    }

    public IBackgroundTaskHandler? ResolveHandler(IServiceProvider serviceProvider, string taskType)
    {
        if (_handlerTypes.TryGetValue(taskType, out var handlerType))
        {
            return (serviceProvider.GetService(handlerType) as IBackgroundTaskHandler)
                ?? (ActivatorUtilities.CreateInstance(serviceProvider, handlerType) as IBackgroundTaskHandler);
        }
        return null;
    }

    public TimeSpan GetDefaultLeaseTimeout(string taskType)
    {
        if (_leaseTimeouts.TryGetValue(taskType, out var timeout))
        {
            return timeout;
        }
        return TimeSpan.FromMinutes(5);
    }
}
