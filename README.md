# PiMonitR
Real Time Baby Monitor Chrome Extension - Streaming from Raspberry PI using SignalR and Cognitive Vision Service

[![1553923113489](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553923113489.png)](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553923113489.png "1553923113489")

1553923113489

In this article, we will look at how to do baby monitor with Raspberry PI including camera module using SignalR streaming and Cognitive Vision Service APIs. Server side signalR streaming hub hosted in Raspberry PI along with dedicated .net core background worker service running in a separate thread to capture a picture of baby every 1 min and pass it to cognitive vision service to detect the emotion and if the emotion rate of crying is higher than defined limit, signalR Hub will notify the connected users in real time.

SignalR Client is a chrome extension developed in Javascript and it runs in chrome browser background all the time. When the user is notified about baby emotion from SignalR Hub, user will click the  **start streaming**  button from the chrome extension popup window to invoke signalR stream method and subscribe to it. Hub will start execute the streaming method to capture the photo every 100 milliseconds and streaming back to connected client. Client will receive it in complete method callback and show the stream content in the popup window until user clicks the stop streaming button to close the stream. During the streaming, background service will suspend the process of detecting the baby emotion using cognitive service and it resumes back when streaming is completed.

Before, we deep dive into article, I just want to add disclaimer to readers. I am a very big fan of signalR and always wanted to try different things with signalR. When i got this idea, i had very limited docs on signalr streaming and how to capture the photo / video from Raspberry PI. So, this is my experiment fun project with signalR streaming and i wrote this code with out thinking much about scaling and performance.

### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#Architecture "Architecture")Architecture

#### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#Diagram "Diagram")Diagram

[![1553933080399](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553933080399.png)](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553933080399.png "1553933080399")

1553933080399

### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#Demo "Demo")Demo

#### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#Prerequisites-and-Dependencies "Prerequisites and Dependencies")Prerequisites and Dependencies

-   Raspberry PI 3 with Camera Module (Any Version with cameral module port)
-   Azure Portal Account - Cognitive Vision Service (Free tier)
-   [MMALSharp - Unofficial C# API for the Raspberry Pi camera.](https://github.com/techyian/MMALSharp)
-   [Microsoft.Azure.CognitiveServices.Vision.Face Nuget Package.](https://www.nuget.org/packages/Microsoft.Azure.CognitiveServices.Vision.Face/2.4.0-preview)

### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#Steps "Steps")Steps

##### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#PiMonitR-SignalR-Hub "PiMonitR SignalR Hub")PiMonitR SignalR Hub

PiMonitRHub is streaming hub with startstream and stopstream methods. When the client invokes the startstream method, it calls the camera service to take the picture and converts to byte array and returns to client. It waits for 100 milliseconds and the do the same process again and stream back to client using ChannelReader.

``` csharp
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
```
##### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#PiMonitR-Background-Service "PiMonitR Background Service")PiMonitR Background Service

PiMonitR is a worker service inheriting from .Net Core background service. So, whenever, server is running in Raspberry PI, it will run in a separate thread for this worker service to run the logic inside the ExecuteAsync method.

``` csharp

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
``` 

Background service will run every 10 seconds for each iteration. It capture the photo using camera service and sends it to cognitive service API to detect the emotion. If the emotion rate is higher than defined limit, hub will broadcast the notification to all connected clients. If the stream is already running, _isStreamRunning property in hub will be set so that background service will not process anything further until streaming is closed.

##### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#Cognitive-Vision-Service "Cognitive Vision Service")Cognitive Vision Service

Microsoft Cognitive Service API provides the power of AI in few lines of code. There are various AI APIs are available with cognitive service. I am using Vision API to detect the face emotion to see if the baby is crying or not. In Vision Service, We have Face API that provides algorithms for detecting, recognizing, and analyzing human faces in images. I am using the Free tier from Azure Portal which allows 20 calls per minute.

[![1553926256620](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553926256620.png)](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553926256620.png "1553926256620")


After you register the cognitive service in Azure Portal, you can get API end point and the Keys from the portal.

[![1553927026058](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553927026058.png)](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553927026058.png "1553927026058")


You can store the Keys and EndPointURL in to UserSecrets / AppSettings / Azure Key Vault so that we can access it from configuration API.

``` csharp

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
``` 

Install the Microsoft.Azure.CognitiveServices.Vision.Face nuget package to install the FaceClient. Before, making the API call, set the face attributes to return only emotion attribute. Face API has got so many face attributes for the identified face. But, for our use case, we use the emotion attribute and it has the value of  **Sadness or Anger or Fear**  higher than 0.5, we are returning as baby is crying so that user can get the notification to view the stream. I just came up with this limit and attributes and you can change the value or attributes that works for you. I have tested with few crying images and my limit works fine for all those cases.

##### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#PiMonitR-Camera-Service "PiMonitR Camera Service")PiMonitR Camera Service

I am running my Rasperry PI with Raspian OS which is based on Linux ARM architecture. The camera module has built in command line tool called  `raspistill`  to take the picture. however, i was looking for some c# library to capture picture from PI and i found out the wonderful open source project called  [MMALSharp](https://github.com/techyian/MMALSharp)  which is Unofficial C# API for the Raspberry Pi camera and it supports Mono 4.x and .NET Standard 2.0.

I installed the nuget package of MMALSharp and initiated the singleton object in the constructor so that it can be reused while streaming the continuous shots of pictures. I have also set the resolution to 640 * 480 for the picture because the default resolution is very high and file size is huge as well.

``` csharp

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
``` 

### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#Publish-Server-App-to-Raspberry-PI "Publish Server App to Raspberry PI")Publish Server App to Raspberry PI

Now, that we have completed the server app, we can build the solution and publish the app into Raspberry PI. I used to self containment deploy so that all the dependencies are part of the deployment.

``` csharp
dotnet publish -r linux-arm  
``` 
This command will publish the output in linux-arm/publish folder under bin folder. I have file sharing with my raspberry PI so i just copied all the files into piMonitR folder in PI machine.

[![1553928540129](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553928540129.png)](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553928540129.png "1553928540129")


After all the files are copied, i connected my raspberry PI through remote connection and run the app with the following command in the terminal.

[![1553928844428](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553928844428.png)](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553928844428.png "1553928844428")

#### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#PiMonitR-Chrome-Extension-SignalR-Client "PiMonitR Chrome Extension SignalR Client")PiMonitR Chrome Extension SignalR Client

I choose chrome extension as my client because it supports real time notification and also it doesn’t need any server to host the client app. In this client app, i have background script which will initialize signalR connection with hub and runs in background to receive any notification from hub. Popup page will have start streaming and stop streaming button and image view holder to show the streaming output.

##### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#manifest-json "manifest.json")manifest.json

manifest.json will define the background scripts, icons and permissions that are needed for this extension.

``` json

{  
 "name": "Pi MonitR Client",  
 "version": "1.0",  
 "description": "Real time Streaming from Raspnerry PI using SignalR",  
 "browser_action": {  
 "default_popup": "popup.html",  
 "default_icon": {  
 "16": "images/16.png",  
 "32": "images/32.png",  
 "48": "images/48.png",  
 "128": "images/128.png"  
 }  
 },  
 "icons": {  
 "16": "images/16.png",  
 "32": "images/32.png",  
 "48": "images/48.png",  
 "128": "images/128.png"  
 },  
 "permissions": [  
 "tabs",  
 "notifications",  
 "http://*/*"  
 ],  
 "background": {  
 "persistent": true,  
 "scripts": [  
 "signalr.js","background.js"  
 ]  
 },  
 "manifest_version": 2,  
 "web_accessible_resources": [  
 "images/*.png"   
 ]  
}  
```

##### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#background-js "background.js")background.js

``` javascript

// The following sample code uses modern ECMAScript 6 features   
// that aren't supported in Internet Explorer 11.  
// To convert the sample for environments that do not support ECMAScript 6,   
// such as Internet Explorer 11, use a transpiler such as   
// Babel at http://babeljs.io/.  
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {  
 return new (P || (P = Promise))(function (resolve, reject) {  
 function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }  
 function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }  
 function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }  
 step((generator = generator.apply(thisArg, _arguments || [])).next());  
 });  
};  
  
const hubUrl = "http://pi:5000/hubs/piMonitR"  
  
var connection = new signalR.HubConnectionBuilder()  
 .withUrl(hubUrl, { logger: signalR.LogLevel.Information })  
 .build();  
  
// We need an async function in order to use await, but we want this code to run immediately,  
// so we use an "immediately-executed async function"  
(() => __awaiter(this, void 0, void 0, function* () {  
 try {  
 yield connection.start();  
 }  
 catch (e) {  
 console.error(e.toString());  
 }  
}))();  
  
connection.on("ReceiveNotification", (message) => {  
 new Notification(message, {  
 icon: '48.png',  
 body: message  
 });  
});  
  
chrome.runtime.onConnect.addListener(function (externalPort) {  
 externalPort.onDisconnect.addListener(function () {  
 connection.invoke("StopStream").catch(err => console.error(err.toString()));  
 });  
});  
``` 

background.js will initiate the signalR connection with hub with the URL defined. We also need signalr.js in the same folder. In order to get the signalr.js file, we need to install signalr npm package and copy the signalr.js from  _node_modules\@aspnet\signalr\dist\browser_  folder.

> npm install @aspnet/signalr

This background script will keep our signalR client active and when it receives the notification from hub, it will show as chrome notification.

[![1553929708010](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553929708010.png)](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553929708010.png "1553929708010")


##### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#popup-html "popup.html")popup.html

``` html

<!doctype html>  
<html>  
  
<head>  
 <title>Pi MonitR Dashboard</title>  
 <script src="popup.js" type="text/javascript"></script>  
</head>  
  
<body>  
 <h1>Pi MonitR - Stream Dashboard</h1>   
 <div>  
 <input type="button" id="streamStartButton" value="Start Streaming" />  
 <input type="button" id="streamStopButton" value="Stop Streaming" disabled />  
 </div>  
 <ul id="logContent"></ul>  
 <img id="streamContent" width="700" height="400" src="" />   
 </body>  
</html>  
``` 

popup html will show the stream content when the start streaming button is clicked. it will complete the stream when the stop streaming is clicked.

##### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#popup-js "popup.js")popup.js

``` javascript

var __awaiter = chrome.extension.getBackgroundPage().__awaiter;  
var connection = chrome.extension.getBackgroundPage().connection;  
  
document.addEventListener('DOMContentLoaded', function () {  
 const streamStartButton = document.getElementById('streamStartButton');  
 const streamStopButton = document.getElementById('streamStopButton');  
 const streamContent = document.getElementById('streamContent');  
 const logContent = document.getElementById('logContent');  
  
 streamStartButton.addEventListener("click", (event) => __awaiter(this, void 0, void 0, function* () {  
 streamStartButton.setAttribute("disabled", "disabled");  
 streamStopButton.removeAttribute("disabled");  
 try {  
 connection.stream("StartStream")  
 .subscribe({  
 next: (item) => {   
 streamContent.src = "data:image/jpg;base64," + item;   
 },  
 complete: () => {  
 var li = document.createElement("li");  
 li.textContent = "Stream completed";  
 logContent.appendChild(li);  
 },  
 error: (err) => {  
 var li = document.createElement("li");  
 li.textContent = err;  
 logContent.appendChild(li);  
 },  
 });  
 }  
 catch (e) {  
 console.error(e.toString());  
 }  
 event.preventDefault();  
 }));  
  
 streamStopButton.addEventListener("click", function () {  
 streamStopButton.setAttribute("disabled", "disabled");  
 streamStartButton.removeAttribute("disabled");  
 connection.invoke("StopStream").catch(err => console.error(err.toString()));  
 event.preventDefault();  
 });  
  
 connection.on("StopStream", () => {  
 var li = document.createElement("li");  
 li.textContent = "stream closed";  
 logContent.appendChild(li);   
 streamStopButton.setAttribute("disabled", "disabled");  
 streamStartButton.removeAttribute("disabled");  
 });  
});  
``` 

When the user clicks the start streaming button, it invoke the stream hub method (StartStream) and subscribe to it. When the hub sends the byte array content, it get the Next method callback with the item content and setting that value directly to streamContent Image src attribute.

`streamContent.src = "data:image/jpg;base64," + item;`

[![1553930343747](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553930343747.png)](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553930343747.png "1553930343747")

1553930343747

when the user clicks the stop streaming button, client invoke the StopStream hub method which will set the _isStreamRunning Property to false which will complete the stream and complete callback will be called.

[![1553930546244](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553930546244.png)](https://jeevasubburaj.com/images/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/1553930546244.png "1553930546244")


### [](https://jeevasubburaj.com/2019/03/30/real-time-baby-monitor-chrome-extension-streaming-from-raspberry-pi-using-signalr-and-cognitive-vision-service/#Conclusion "Conclusion")Conclusion

As i mentioned before, this is just a fun project to experiment signalR streaming and i am really happy with the end result. We also going to have lots of new stuffs coming in (IAsyncEnumerable) and its going to get even better for many real time scenario projects with signalR.

Happy Coding.
