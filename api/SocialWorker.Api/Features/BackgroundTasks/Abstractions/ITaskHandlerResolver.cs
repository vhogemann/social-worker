using System;
using System.Collections.Generic;
using System.Linq;
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

    public TaskHandlerResolver(IEnumerable<IBackgroundTaskHandler> registeredHandlers)
    {
        foreach (var handler in registeredHandlers)
        {
            var type = handler.GetType();
            _handlerTypes[handler.TaskType] = type;
            _leaseTimeouts[handler.TaskType] = handler.DefaultLeaseTimeout;
        }
    }

    public IBackgroundTaskHandler? ResolveHandler(IServiceProvider serviceProvider, string taskType)
    {
        if (_handlerTypes.TryGetValue(taskType, out var handlerType))
        {
            return serviceProvider.GetService(handlerType) as IBackgroundTaskHandler;
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
