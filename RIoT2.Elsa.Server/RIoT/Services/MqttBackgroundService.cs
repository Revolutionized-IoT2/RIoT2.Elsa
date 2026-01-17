using RIoT2.Elsa.Server.RIoT.Services.Interfaces;

namespace RIoT2.Elsa.Server.RIoT.Services
{
    internal class MqttBackgroundService : IHostedService
    {
        private readonly IWorkflowMqttService _mqttService;
  
        public MqttBackgroundService(IWorkflowMqttService mqttService)
        {
            _mqttService = mqttService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _mqttService.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _mqttService.Stop();
        }
    }
}