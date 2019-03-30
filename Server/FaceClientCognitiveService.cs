using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PiMonitR
{
    public class FaceClientCognitiveService
    {
        private readonly IFaceClient faceClient;
        private readonly float scoreLimit = 0.5f;
        private readonly ILogger<FaceClientCognitiveService> _logger;
        public FaceClientCognitiveService(IConfiguration config, ILogger<FaceClientCognitiveService> logger)
        {
            _logger = logger;
            faceClient = new FaceClient(new ApiKeyServiceClientCredentials(config["SubscriptionKey"]),
                new System.Net.Http.DelegatingHandler[] { });
            faceClient.Endpoint = config["FaceEndPointURL"];
        }

        public async Task<bool> IsCryingDetected(Stream stream)
        {
            IList<FaceAttributeType> faceAttributes = new FaceAttributeType[]
            {
                FaceAttributeType.Emotion
            };
            // Call the Face API.
            try
            {
                IList<DetectedFace> faceList = await faceClient.Face.DetectWithStreamAsync(stream, false, false, faceAttributes);
                if (faceList.Count > 0)
                {
                    var face = faceList[0];
                    if (face.FaceAttributes.Emotion.Sadness >= scoreLimit ||
                        face.FaceAttributes.Emotion.Anger >= scoreLimit ||
                        face.FaceAttributes.Emotion.Fear >= scoreLimit)
                    {
                        _logger.LogInformation($"Crying Detected with the score of {face.FaceAttributes.Emotion.Sadness}");
                        return true;
                    }
                    else
                    {
                        _logger.LogInformation($"Crying Not Detected with the score of {face.FaceAttributes.Emotion.Sadness}");
                    }
                }
                else
                {
                    _logger.LogInformation("No Face Detected");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }

            return false;
        }
    }
}
