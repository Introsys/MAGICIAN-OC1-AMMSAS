using iSent.Magician.ConsoleApp.Services;
using iSent.Magician.ConsoleApp.Services.ImageSources;
using iSent.Magician.Engine;
using iSent.Magician.Engine.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace iSent.Magician.ConsoleApp
{
    internal class Program
    {
        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== iSent Magician Unified Engine (Embedded Broker + HALCON) ===");

            // Set up graceful shutdown handler for Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cts.Cancel();
            };

            // Monitor the 'Q' key in the background
            _ = Task.Run(CancelKeyMonitor);

            // Start embedded MQTT Broker
            using var brokerService = new MqttBrokerService(port: 1883);
            await brokerService.StartAsync();

            await Task.Delay(200);

            string mqttConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Services", "mqtt_config.yaml");

            using var mqttService = new MqttService(mqttConfigPath);
            await mqttService.StartAsync();

            var engineConfig = new ImageProcessingConfig
            {
                PathToPreprocessParamFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "model_optimized_dl_preprocess_params.hdict"),
                PathToTrainedModelFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "model_optimized.hdl"),
                ImageWidth = 3000,
                ImageHeight = 4096,
                MinScoreClass = 0.40,
                BatchSizeInference = 50
            };

            using IImageSourceService imageSource = ResolveImageSource(args, mqttService);

            using var inspectionEngine = new InspectionEngine(engineConfig);
            Console.WriteLine("[Engine] HALCON Inspection Engine initialized.");

            // Loop for FOLDER Mode (Batch/Files)
            _ = Task.Run(async () =>
            {
                Console.WriteLine("[Worker] Continuous inspection worker loop started.");
                string debugImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_inspected_frame.jpg");

                while (!_cts.Token.IsCancellationRequested)
                {
                    if (mqttService.IsSensingActive)
                    {
                        try
                        {
                            if (imageSource is FileFolderImageSource fileSource)
                            {
                                string? filePath = fileSource.GetNextFilePath();
                                if (!string.IsNullOrEmpty(filePath))
                                {
                                    File.Copy(filePath, debugImagePath, overwrite: true);

                                    InspectionResultMessage resultMessage = inspectionEngine.ProcessImage(filePath);
                                    await mqttService.PublishInspectionResultAsync(resultMessage);

                                    Console.WriteLine($"[Inspection MAX FPS] Processed: {Path.GetFileName(filePath)}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Engine Error] Inspection loop error: {ex.Message}");
                        }

                        await Task.Yield();
                    }
                    else
                    {
                        await Task.Delay(50, _cts.Token);
                    }
                }
            }, _cts.Token);

            // Event for CAMERA Mode (GigE / ROS)
            if (imageSource is HalconGigEVisionImageSource gigeSource)
            {
                imageSource.OnImageFrameReceived += async (imageBytes) =>
                {
                    if (mqttService.IsSensingActive)
                    {
                        try
                        {
                            InspectionResultMessage resultMessage = inspectionEngine.ProcessImage(imageBytes, gigeSource.CurrentWidth, gigeSource.CurrentHeight);
                            await mqttService.PublishInspectionResultAsync(resultMessage);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Engine Error] Processing failed: {ex.Message}");
                        }
                    }
                };
            }

            await imageSource.StartAsync(_cts.Token);
            Console.WriteLine("\n[App] Unified system running. Press 'Q' or CTRL+C to stop application.");

            // Wait for the cancellation signal (Q or Ctrl+C) without blocking the console with ReadLine
            try
            {
                await Task.Delay(-1, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                // Expected on exit
            }

            // Shutdown sequence
            Console.WriteLine("\n[App] Shutting down services...");
            await imageSource.StopAsync();
            await mqttService.StopAsync();
            await brokerService.StopAsync();

            Console.WriteLine("[App] System shutdown completed successfully.");
        }

        private static async Task CancelKeyMonitor()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key is ConsoleKey.Q)
                    {
                        _cts.Cancel();
                        break;
                    }
                }
                await Task.Delay(100);
            }
        }

        private static IImageSourceService ResolveImageSource(string[] args, MqttService mqttService)
        {
            string sourceMode = "folder";

            string defaultResourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");

            if (!Directory.Exists(defaultResourcesPath))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
                defaultResourcesPath = Path.Combine(projectRoot, "Resources");
            }

            string folderPath = defaultResourcesPath;

            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i].Equals("--source", StringComparison.OrdinalIgnoreCase) || args[i].Equals("--mode", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    sourceMode = args[i + 1].ToLower();
                }
                else if (args[i].Equals("--path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    folderPath = args[i + 1];
                }
            }

            Console.WriteLine($"[App] Selected Image Source Mode: '{sourceMode.ToUpper()}'");
            Console.WriteLine($"[App] Target Image Path: '{folderPath}'");

            return sourceMode switch
            {
                "gige" => new HalconGigEVisionImageSource(),
                "ros" => new RosBridgeMqttImageSource(mqttService),
                "folder" or _ => new FileFolderImageSource(folderPath)
            };
        }
    }
}