using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Psi.Media;
using Microsoft.Psi;

namespace WebcamAndStore
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Create the \psi pipeline
             Pipeline pipeline = Pipeline.Create("WebcamAndStore");

            // Create the webcam component
            var webcam = new MediaCapture(pipeline, 640, 480, 30);

            // Create the store component
            var store = PsiStore.Create(pipeline, "Webcam1", "D:\\Stores");

            // Write incoming images in the store at 'Image' track
            store.Write(webcam.Out, "Image");

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
