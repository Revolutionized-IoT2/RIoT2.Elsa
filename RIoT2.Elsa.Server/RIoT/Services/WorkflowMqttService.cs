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


        private Task sendWorkflowOnlineCommand()
        {
            return _client.Publish(_nodeOnlineTopic, Json.SerializeIgnoreNulls(new NodeOnlineMessage()
            {
                IsOnline = true,
                Name = "Elsa3",
                NodeType = NodeType.Workflow,
                NodeBaseUrl = _configuration.WorkflowBaseUrl,
                GrpcBaseUrl = _configuration.WorkflowGrpcBaseUrl
            }));

        }

        public async Task Start()
        {
            try
            {
                _client.MessageReceived += _client_MessageReceived;
                _client.ConnectedAsync += sendWorkflowOnlineCommand;
                await _client.Start(_orchestratorOnlineTopic, _configureTopic);
            }
            catch (Exception x)
            {
                throw new Exception("Could not connect to MQTT Broker", x);
            }
        }

        public async Task Stop()
        {
            _client.MessageReceived -= _client_MessageReceived;
            _client.ConnectedAsync -= sendWorkflowOnlineCommand;
            await _client.Stop();
        }

        private void _client_MessageReceived(MqttEventArgs mqttEventArgs)
        {
            _ = HandleMessageReceivedAsync(mqttEventArgs);
        }

        private async Task HandleMessageReceivedAsync(MqttEventArgs mqttEventArgs)
        {
            try
            {
                if (MqttClient.IsMatch(mqttEventArgs.Topic, _orchestratorOnlineTopic) )
                {
                    await sendWorkflowOnlineCommand();
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