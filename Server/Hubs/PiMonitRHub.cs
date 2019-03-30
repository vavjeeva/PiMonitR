using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace PiMonitR
{
    public class PiMonitRHub : Hub
    {
        internal static bool _isStreamRunning = false;
        private readonly PiCameraService _piCameraService;
        public PiMonitRHub(PiCameraService piCameraService)
        {
            _piCameraService = piCameraService;
        }

        public ChannelReader<object> StartStream(CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<object>();
            _isStreamRunning = true;
            _ = WriteItemsAsync(channel.Writer, cancellationToken);
            return channel.Reader;
        }

        private async Task WriteItemsAsync(ChannelWriter<object> writer, CancellationToken cancellationToken)
        {
            try
            {
                while (_isStreamRunning)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteAsync(await _piCameraService.CapturePictureAsByteArray());
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                writer.TryComplete(ex);
            }

            writer.TryComplete();
        }

        public void StopStream()
        {
            _isStreamRunning = false;
            Clients.All.SendAsync("StopStream");
        }
    }
}
