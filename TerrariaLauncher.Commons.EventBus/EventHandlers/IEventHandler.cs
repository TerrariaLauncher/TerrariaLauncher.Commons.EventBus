using System.Threading.Tasks;

namespace TerrariaLauncher.Commons.EventBus
{
    public interface IEventHandler
    {

    }

    public interface IEventHandler<in TEventData> : IEventHandler
        where TEventData : IEventData
    {
        Task Handle(TEventData @event);
    }
}
