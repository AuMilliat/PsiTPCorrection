using System;
using Microsoft.Psi;
using WebRTC;

namespace WebcamAndStore
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Create the \psi pipeline
             Pipeline pipeline = Pipeline.Create("WebcamAndStore");

            WebRTCVideoStreamConfiguration config = new WebRTCVideoStreamConfiguration();
            config.WebsocketAddress = System.Net.IPAddress.Loopback;
            config.WebsocketPort = 8888;
            WebRTCVideoStream stream = new WebRTCVideoStream(pipeline, config);
            var store = PsiStore.Create(pipeline, "WebRTC", "D:\\Stores");

            store.Write(stream.OutImage, "Image");

            // Start the pipeline running
            pipeline.RunAsync();

            // Wainting for an out key
            Console.WriteLine("Press any key to stop the application.");
            Console.ReadLine();
            // Stop correctly the pipeline.
            pipeline.Dispose();
        }
    }
}
