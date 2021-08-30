using System.Text.Json;
using System.Threading.Tasks;

namespace TerrariaLauncher.Commons.EventBus
{
    public interface IJsonEventHandler : IEventHandler
    {
        Task Handle(JsonDocument eventData);
    }
}
