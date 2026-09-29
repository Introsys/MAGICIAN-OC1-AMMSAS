using iSent.Magician.Engine.Models;
using MQTTnet;
using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace iSent.Magician.ConsoleApp.Services
{
    public class MqttConfiguration
    {
        [YamlMember(Alias = "host")]
        public string Host { get; set; } = "127.0.0.1";

        [YamlMember(Alias = "port")]
        public int Port { get; set; } = 1883;

        [YamlMember(Alias = "robot_topics_mqtt")]
        public List<string> SubscribedTopics { get; set; } = new();

        [YamlMember(Alias = "inspection_mqtt_topic")]
        public string PublishTopic { get; set; } = "inspection/result";
    }

    public class SensingStatus
    {
        public bool Active { get; set; }
    }

    public class MqttService : IDisposable
    {
        private IMqttClient _mqttClient;
        private MqttClientOptions _mqttOptions;
        private MqttConfiguration _config;

        private Position _lastKnownRobotPosition = new Position();
        private bool _isSensingActive = false;

        public Position LastKnownRobotPosition => _lastKnownRobotPosition;
        public bool IsSensingActive => _isSensingActive;

        public event Action<bool>? OnSensingStateChanged;

        public MqttService(string configFilePath)
        {
            LoadConfiguration(configFilePath);
            InitializeMqttClient();
        }

        private void LoadConfiguration(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var yamlContent = File.ReadAllText(filePath);
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .Build();
                    _config = deserializer.Deserialize<MqttConfiguration>(yamlContent);
                    Console.WriteLine($"[MQTT] Configuration loaded successfully from '{filePath}'.");
                }
                else
                {
                    Console.WriteLine($"[MQTT] Config file '{filePath}' not found. Using default settings.");
                    _config = new MqttConfiguration();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Error loading YAML config: {ex.Message}. Falling back to defaults.");
                _config = new MqttConfiguration();
            }
        }

        private void InitializeMqttClient()
        {
            var factory = new MqttClientFactory();
            _mqttClient = factory.CreateMqttClient();

            _mqttOptions = new MqttClientOptionsBuilder()
                .WithClientId($"iSent_Engine_Console_{Guid.NewGuid():N}")
                .WithTcpServer(_config.Host, _config.Port)
                .WithCleanSession()
                .Build();

            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            _mqttClient.DisconnectedAsync += async e =>
            {
                Console.WriteLine("[MQTT] Disconnected from broker. Attempting reconnection in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    if (!_mqttClient.IsConnected)
                    {
                        await _mqttClient.ConnectAsync(_mqttOptions);
                        await SubscribeToTopics();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MQTT] Reconnection failed: {ex.Message}");
                }
            };
        }

        public async Task StartAsync()
        {
            try
            {
                Console.WriteLine($"[MQTT] Connecting to broker at {_config.Host}:{_config.Port}...");
                var connectResult = await _mqttClient.ConnectAsync(_mqttOptions);

                if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
                {
                    Console.WriteLine("[MQTT] Connected successfully.");
                    await SubscribeToTopics();
                }
                else
                {
                    Console.WriteLine($"[MQTT] Connection failed with code: {connectResult.ResultCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Error connecting to broker: {ex.Message}");
            }
        }

        private async Task SubscribeToTopics()
        {
            foreach (var topic in _config.SubscribedTopics)
            {
                await _mqttClient.SubscribeAsync(topic);
                Console.WriteLine($"[MQTT] Subscribed to topic: {topic}");
            }
        }

        private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            try
            {
                // Process robot pose payload
                if (topic.Contains("robot/position"))
                {
                    var pose = JsonSerializer.Deserialize<Pose>(payload);
                    if (pose != null)
                    {
                        _lastKnownRobotPosition = pose.Position;
                    }
                }
                // Process sensing active state payload
                else if (topic.Contains("robot/sensing/active"))
                {
                    var status = JsonSerializer.Deserialize<SensingStatus>(payload);
                    if (status != null)
                    {
                        _isSensingActive = status.Active;
                        Console.WriteLine($"[MQTT] Sensing active state changed to: {_isSensingActive}");
                        OnSensingStateChanged?.Invoke(_isSensingActive);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Payload parsing error on topic '{topic}': {ex.Message}");
            }

            return Task.CompletedTask;
        }

        public async Task PublishInspectionResultAsync(InspectionResultMessage message)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected)
            {
                Console.WriteLine("[MQTT] Warning: Cannot publish, MQTT client is disconnected.");
                return;
            }

            // Inject the latest known robot position into the inspection pose
            message.InspectionPose = new Pose
            {
                Position = _lastKnownRobotPosition,
                Orientation = new Orientation { w = 1.0 }
            };

            string jsonPayload = JsonSerializer.Serialize(message);

            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(_config.PublishTopic)
                .WithPayload(jsonPayload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(mqttMessage);
            Console.WriteLine($"[MQTT] Published inspection result to '{_config.PublishTopic}' with robot position ({_lastKnownRobotPosition.x}, {_lastKnownRobotPosition.y}, {_lastKnownRobotPosition.z}).");
        }

        public async Task StopAsync()
        {
            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
                Console.WriteLine("[MQTT] Disconnected from broker.");
            }
        }

        public void Dispose()
        {
            _mqttClient?.Dispose();
        }
    }
}