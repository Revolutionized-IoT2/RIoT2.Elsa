using RIoT2.Core;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;

namespace RIoT2.Elsa.Server.RIoT.Services
{

    internal class WorkflowMqttService : IWorkflowMqttService, IDisposable
    {
        private MqttClient _client;
        private string _orchestratorOnlineTopic;
        private string _nodeOnlineTopic;
        private string _configureTopic;
        private readonly IRIoTConfigurationService _configuration;
        private readonly ILogger _logger;

        public WorkflowMqttService(IRIoTConfigurationService configuration, ILogger<WorkflowMqttService> logger)
        {
            _logger = logger;
            _configuration = configuration;
            _orchestratorOnlineTopic = Constants.Get("+", MqttTopic.OrchestratorOnline);
            _nodeOnlineTopic = Constants.Get(configuration.Id, MqttTopic.NodeOnline);
            _configureTopic = Constants.Get(configuration.Id, MqttTopic.Configuration);

            _client = new MqttClient(_configuration.Id,
                _configuration.MQTT_ServerUrl,
                _configuration.MQTT_Username,
                _configuration.MQTT_Password);
        }

        public void Dispose()
        {
            _client?.Dispose();
        }


        private void sendWorkflowOnlineCommand() 
        {
            _client.Publish(_nodeOnlineTopic, Json.SerializeIgnoreNulls(new NodeOnlineMessage()
            {
                IsOnline = true,
                Name = "Workflow"
            }));

        }

        public async Task Start()
        {
            try
            {
                await _client.Start(_orchestratorOnlineTopic, _configureTopic); //We're only interested in orchestrator online messages. Reports will be handled by the API Callbacks
                _client.MessageReceived += _client_MessageReceived;
            }
            catch (Exception x)
            {
                throw new Exception("Could not connect to MQTT Broker", x);
            }
        }

        public async Task Stop()
        {
            await _client.Stop();
        }

        private void _client_MessageReceived(MqttEventArgs mqttEventArgs)
        {
            try
            {
                if (MqttClient.IsMatch(mqttEventArgs.Topic, _orchestratorOnlineTopic) )
                {
                    sendWorkflowOnlineCommand();
                }
                if (MqttClient.IsMatch(mqttEventArgs.Topic, _configureTopic))
                {
                    var configurationCmd = Json.Deserialize<ConfigurationCommand>(mqttEventArgs.Message);
                    _configuration.OrchestratorBaseUrl = configurationCmd.ApiBaseUrl;
                }
            }
            catch (Exception x)
            {
                _logger.LogError(x, "Could not handle mqtt message {mqttEventArgs.Message}", mqttEventArgs.Message);
            }
        }
    }
}