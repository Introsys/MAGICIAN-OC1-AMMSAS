using HalconDotNet;
using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace iSent.Magician.ConsoleApp.Services.ImageSources
{

    public class HalconGigEVisionImageSource : IImageSourceService
    {
        public event Action<byte[]>? OnImageFrameReceived;

        private HTuple? _acqHandle;
        private bool _isAcquiring;
        public int CurrentWidth { get; private set; }
        public int CurrentHeight { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // open a GigEVision2 camera
                HOperatorSet.OpenFramegrabber(
                    "GigEVision2", 0, 0, 0, 0, 0, 0, "default", -1, "default",
                    -1, "false", "default", "default", 0, -1, out _acqHandle);

                _isAcquiring = true;

                // start the acquisition loop in a dedicated Task
                _ = Task.Run(() => AcquisitionLoop(cancellationToken), cancellationToken);

                Console.WriteLine("[GigE Source] Camera opened and acquisition loop started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GigE Source Error] Failed to open camera: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private void AcquisitionLoop(CancellationToken cancellationToken)
        {
            while (_isAcquiring && !cancellationToken.IsCancellationRequested)
            {
                HObject? image = null;
                try
                {
                    if (_acqHandle == null) break;

                    // sync grab
                    HOperatorSet.GrabImage(out image, _acqHandle);

                    if (image != null && image.IsInitialized())
                    {
                        // cast HObject to  HImage 
                        using HImage hImage = new HImage(image);

                        // dynamic conversion do byte[] acoridng to image size
                        byte[] imageBytes = ConvertHImageToBytesDynamic(hImage);

                        // trigger byte[] event 
                        OnImageFrameReceived?.Invoke(imageBytes);
                    }
                }
                catch (HalconException hex)
                {
                    Console.WriteLine($"[GigE Grab Error] HALCON Exception: {hex.Message}");
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GigE Grab Error] {ex.Message}");
                }
                finally
                {
                    image?.Dispose();
                }
            }

            Console.WriteLine("[GigE Source] Camera acquisition stopped.");
        }

        private byte[] ConvertHImageToBytesDynamic(HImage image)
        {
            HOperatorSet.GetImagePointer1(image, out HTuple pointer, out HTuple type, out HTuple width, out HTuple height);

            // update public cam properies
            CurrentWidth = width.I;
            CurrentHeight = height.I;

            int channels = image.CountChannels();
            int pixelSize = type.S == "byte" ? 1 : 2; // supports 8-bit and 16-bit
            int totalBytes = width.I * height.I * channels * pixelSize;

            byte[] rawData = new byte[totalBytes];
            System.Runtime.InteropServices.Marshal.Copy(pointer.IP, rawData, 0, totalBytes);

            return rawData;
        }

        public Task StopAsync()
        {
            _isAcquiring = false;
            if (_acqHandle != null)
            {
                try
                {
                    HOperatorSet.CloseFramegrabber(_acqHandle);
                    _acqHandle.Dispose();
                    _acqHandle = null;
                }
                catch { }
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            StopAsync().Wait();
        }
    }
}