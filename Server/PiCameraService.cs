using MMALSharp;
using MMALSharp.Common.Utility;
using MMALSharp.Handlers;
using MMALSharp.Native;
using System.IO;
using System.Threading.Tasks;

namespace PiMonitR
{
    public class PiCameraService
    {
        public MMALCamera MMALCamera;
        private readonly string picStoragePath = "/home/pi/images/";
        private readonly string picExtension = "jpg";
        public PiCameraService()
        {
            MMALCamera = MMALCamera.Instance;
            //Setting the Average resolution for reducing the file size
            MMALCameraConfig.StillResolution = new Resolution(640, 480);
        }

        public async Task<byte[]> CapturePictureAsByteArray()
        {
            var fileName = await CapturePictureAndGetFileName();

            string filePath = Path.Join(picStoragePath, $"{fileName}.{picExtension}");
            byte[] resultData = await File.ReadAllBytesAsync(filePath);

            //Delete the captured picture from PI storage
            File.Delete(filePath);
            return resultData;
        }

        public async Task<Stream> CapturePictureAsStream()
        {
            return new MemoryStream(await CapturePictureAsByteArray());
        }

        private async Task<string> CapturePictureAndGetFileName()
        {
            string fileName = null;
            using (var imgCaptureHandler = new ImageStreamCaptureHandler(picStoragePath, picExtension))
            {
                await MMALCamera.TakePicture(imgCaptureHandler, MMALEncoding.JPEG, MMALEncoding.I420);
                fileName = imgCaptureHandler.GetFilename();
            }
            return fileName;
        }
    }
}
