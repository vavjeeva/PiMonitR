using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace PiMonitR
{
    internal class PiMonitRWorker : BackgroundService
    {        
        private readonly IHubContext<PiMonitRHub> _piMonitRHub;
        private readonly PiCameraService _piCameraService;
        private readonly FaceClientCognitiveService _faceClientCognitiveService;
        public PiMonitRWorker(IHubContext<PiMonitRHub> piMonitRHub,
            PiCameraService piCameraService, FaceClientCognitiveService faceClientCognitiveService)
        {           
            _piMonitRHub = piMonitRHub;
            _piCameraService = piCameraService;
            _faceClientCognitiveService = faceClientCognitiveService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {               
                if (!PiMonitRHub._isStreamRunning)
                {
                    var stream = await _piCameraService.CapturePictureAsStream();
                    //Testing without rasperry PI with the below line of code
                    //var stream = new MemoryStream(System.IO.File.ReadAllBytes("cry.jpg"));                   
                    if (await _faceClientCognitiveService.IsCryingDetected(stream))
                    {
                        await _piMonitRHub.Clients.All.SendAsync("ReceiveNotification", "Baby Crying Detected! You want to start streaming?");
                    }
                }
                //Run the background service for every 10 seconds
                await Task.Delay(10000);
            }
        }
    }
}