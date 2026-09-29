using System;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Server;

namespace iSent.Magician.ConsoleApp.Services
{
    public class MqttBrokerService : IDisposable
    {
        private MqttServer? _mqttServer;
        private readonly int _port;

        public MqttBrokerService(int port = 1883)
        {
            _port = port;
        }

        public async Task StartAsync()
        {
            try
            {
                var mqttFactory = new MqttServerFactory();
                var serverOptions = new MqttServerOptionsBuilder()
                    .WithDefaultEndpoint()
                    .WithDefaultEndpointPort(_port)
                    .Build();

                _mqttServer = mqttFactory.CreateMqttServer(serverOptions);

                // Optional event logging for client connections
                _mqttServer.ClientConnectedAsync += e =>
                {
                    Console.WriteLine($"[Broker Embedded] Client connected: {e.ClientId}");
                    return Task.CompletedTask;
                };

                await _mqttServer.StartAsync();
                Console.WriteLine($"[Broker Embedded] MQTT Broker running successfully on port {_port}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broker Embedded Error] Failed to start MQTT broker: {ex.Message}");
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (_mqttServer != null)
            {
                await _mqttServer.StopAsync();
                Console.WriteLine("[Broker Embedded] MQTT Broker stopped.");
            }
        }

        public void Dispose()
        {
            _mqttServer?.Dispose();
        }
    }
}