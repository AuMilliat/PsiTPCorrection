// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace AzureKinectSample
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using MathNet.Spatial.Euclidean;
    using Microsoft.Azure.Kinect.BodyTracking;
    using Microsoft.Azure.Kinect.Sensor;
    using Microsoft.Psi;
    using Microsoft.Psi.AzureKinect;
    using Microsoft.Psi.Calibration;
    using Microsoft.Psi.Imaging;

    /// <summary>
    /// Azure Kinect postures sample program.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static void Main()
        {
            using (var pipeline = Pipeline.Create("AzureKinectSample", DeliveryPolicy.LatestMessage))
            {
                // Open the store
                var store = PsiStore.Open(pipeline, "Azure", $"F:\\Stores");
                var bodies = store.OpenStream<List<AzureKinectBody>>("Bodies");
                var color = store.OpenStream<List<AzureKinectBody>>("Color");
                var depth = store.OpenStream<List<AzureKinectBody>>("Depth");

                // Create the componentq
                Bodies.HandsProximityDetectorConfiguration configHands = new Bodies.HandsProximityDetectorConfiguration();
                configHands.IsPairToCheckGiven = false;
                Bodies.HandsProximityDetector detector = new Bodies.HandsProximityDetector(pipeline, configHands);

                Bodies.BodyPosturesDetectorConfiguration configPostures = new Bodies.BodyPosturesDetectorConfiguration();
                Bodies.BodyPosturesDetector postures = new Bodies.BodyPosturesDetector(pipeline, configPostures);

                detector.Out.Do((m, e) =>
                {
                    foreach (var data in m)
                    {
                        foreach (var item in data.Value)
                            Console.WriteLine($"{data.Key} - {item}");
                    }
                });

                postures.Out.Do((m, e) =>
                {
                    foreach (var data in m)
                    {
                        foreach (var item in data.Value)
                            Console.WriteLine($"{data.Key} - {item}");
                    }
                });

                bodies.Out.PipeTo(detector.In);
                bodies.Out.PipeTo(postures.In);

                Console.Clear();
                pipeline.RunAsync();
                Console.ReadLine(); // press Enter to end
            }
        }
    }
}
