using System;
using System.Collections.Generic;
using System.Text;

namespace TerrariaLauncher.Commons.EventBus
{
    public abstract class EventData : IEventData
    {
        public EventData()
        {
            Id = Guid.NewGuid();
            EventTime = DateTimeOffset.UtcNow;
        }

        public EventData(Guid id, DateTimeOffset createDate)
        {
            Id = id;
            EventTime = createDate;
        }

        public Guid Id { get; set; }

        public DateTimeOffset EventTime { get; set; }
    }
}
