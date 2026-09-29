using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace iSent.Magician.ConsoleApp.Services.ImageSources
{
    public class RosImagePayload
    {
        public string Data { get; set; } = string.Empty; // Base64 encoded image string from ROS bridge
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class RosBridgeMqttImageSource : IImageSourceService
    {
        public event Action<byte[]>? OnImageFrameReceived;
        private readonly MqttService _mqttService;
        private readonly string _rosImageTopic;

        public RosBridgeMqttImageSource(MqttService mqttService, string rosImageTopic = "ros/camera/image_raw")
        {
            _mqttService = mqttService;
            _rosImageTopic = rosImageTopic;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Note: Subscribe to the ROS Image Topic inside MqttService or directly here
            Console.WriteLine($"[ROS Source] Listening for ROS image messages on topic '{_rosImageTopic}'...");
            return Task.CompletedTask;
        }

        // Call this method whenever MqttService receives a message on the ROS Image Topic
        public void ProcessRosMqttPayload(string payload)
        {
            try
            {
                // ROS Bridge usually sends JSON with Base64 image payload or raw byte payload
                if (payload.TrimStart().StartsWith("{"))
                {
                    var rosMsg = JsonSerializer.Deserialize<RosImagePayload>(payload);
                    if (rosMsg != null && !string.IsNullOrEmpty(rosMsg.Data))
                    {
                        byte[] imageBytes = Convert.FromBase64String(rosMsg.Data);
                        OnImageFrameReceived?.Invoke(imageBytes);
                    }
                }
                else
                {
                    // Direct RAW byte payload
                    byte[] imageBytes = Encoding.UTF8.GetBytes(payload);
                    OnImageFrameReceived?.Invoke(imageBytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ROS Source Error] Failed to parse ROS image payload: {ex.Message}");
            }
        }

        public Task StopAsync()
        {
            Console.WriteLine("[ROS Source] Stopped ROS image listener.");
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
}