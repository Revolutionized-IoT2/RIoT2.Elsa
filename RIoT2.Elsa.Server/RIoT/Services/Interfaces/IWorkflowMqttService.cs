using RIoT2.Core.Models;

namespace RIoT2.Elsa.Server.RIoT.Services.Interfaces
{
    public interface IWorkflowMqttService
    {
        Task Start();

        Task Stop();

        void Dispose();

    }
}
