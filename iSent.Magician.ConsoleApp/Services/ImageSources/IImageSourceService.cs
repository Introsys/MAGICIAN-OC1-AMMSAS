using System;
using System.Threading;
using System.Threading.Tasks;

namespace iSent.Magician.ConsoleApp.Services.ImageSources
{
    public interface IImageSourceService : IDisposable
    {
        // Event triggered whenever a new image frame is acquired/received
        event Action<byte[]>? OnImageFrameReceived;

        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync();
    }
}