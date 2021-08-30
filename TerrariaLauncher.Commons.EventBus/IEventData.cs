using System;

namespace TerrariaLauncher.Commons.EventBus
{
    public interface IEventData
    {
        Guid Id { get; set; }
        DateTimeOffset EventTime { get; set; }
    }
}
