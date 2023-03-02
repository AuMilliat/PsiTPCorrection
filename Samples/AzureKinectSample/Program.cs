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
    using RemoteConnectors;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Media;

    /// <summary>
    /// Azure Kinect sample program.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static void Main()
        {
            // camera resolution settings
            const int widthSource = 1280;
            const int heightSource = 720;

            // down sampled resolution
            const int widthOutput = 80;
            const int heightOutput = 45;
            const double scaleFactorWidth = (double)widthOutput / widthSource;
            const double scaleFactorHeight = (double)heightOutput / heightSource;

            // background subtraction beyond this depth
            const double maxDepth = 1.0; // meters

            const SensorOrientation initialOrientation = SensorOrientation.Default;

            using (var pipeline = Pipeline.Create("AzureKinectSample", DeliveryPolicy.LatestMessage))
            {
                KinectAzureRemoteConnectorConfiguration kinectAzureRemoteConnectorConfiguration = new KinectAzureRemoteConnectorConfiguration();
                kinectAzureRemoteConnectorConfiguration.Address = "localhost";
                kinectAzureRemoteConnectorConfiguration.StartPort = 22822;
                kinectAzureRemoteConnectorConfiguration.ActiveStreamNumber = 5;
                KinectAzureRemoteConnector kinectAzureRemoteConnector = new KinectAzureRemoteConnector(pipeline, kinectAzureRemoteConnectorConfiguration);

                StringBuilder sb = new StringBuilder();
                SensorOrientation lastOrientation = (SensorOrientation)(-1); // detect orientation changes

                kinectAzureRemoteConnector.OutColorImage.Decode(new ImageFromBitmapStreamDecoder()).Join(kinectAzureRemoteConnector.OutDepthImage.Decode()).Join(kinectAzureRemoteConnector.OutIMU, TimeSpan.FromMilliseconds(10)).Pair(kinectAzureRemoteConnector.OutBodies).Pair(kinectAzureRemoteConnector.OutDepthDeviceCalibrationInfo).Do(message =>
                {
                    var (color, depth, imu, bodies, calib) = message;

                    // determine camera orientation from IMU
                    static SensorOrientation ImuOrientation(ImuSample imu)
                    {
                        const double halfGravity = 9.8 / 2;
                        return
                            (imu.AccelerometerSample.Z > halfGravity) ? SensorOrientation.Flip180 :
                            (imu.AccelerometerSample.Y > halfGravity) ? SensorOrientation.Clockwise90 :
                            (imu.AccelerometerSample.Y < -halfGravity) ? SensorOrientation.CounterClockwise90 :
                            SensorOrientation.Default; // upright
                    }

                    // enumerate image coordinates while correcting for orientation
                    static (IEnumerable<int>, IEnumerable<int>, bool) EnumerateCoordinates(SensorOrientation orientation)
                    {
                        var w = Enumerable.Range(0, widthOutput);
                        var h = Enumerable.Range(0, heightOutput);
                        return orientation switch
                        {
                            SensorOrientation.Clockwise90 => (h.Reverse(), w, true),
                            SensorOrientation.Flip180 => (w.Reverse(), h.Reverse(), false),
                            SensorOrientation.CounterClockwise90 => (h, w.Reverse(), true),
                            _ => (w, h, false), // normal
                        };
                    }

                    // render color frame as "ASCII art"
                    sb.Clear();
                    var bitmap = color.Resource.ToBitmap();
                    var orientation = ImuOrientation(imu);
                    var (horizontal, vertical, swap) = EnumerateCoordinates(orientation);
                    foreach (var j in vertical.Where(n => n % 2 == 0))
                    {
                        foreach (var i in horizontal)
                        {
                            var (x, y) = swap ? (j, i) : (i, j);

                            // subtract background beyond max depth
                            var d = CalibrationExtensions.ProjectToCameraSpace(calib, new Point2D(x / scaleFactorWidth, y / scaleFactorHeight), depth);
                            if (!d.HasValue || d.Value.Z < maxDepth)
                            {
                                var p = bitmap.GetPixel(x, y);
                                sb.Append(" .:-=+*#%@"[(int)((p.R + p.G + p.B) / 76.5)]);
                            }
                            else
                            {
                                sb.Append(' '); // subtract background
                            }
                        }

                        sb.Append(Environment.NewLine);
                    }

                    // clear console when orientation changes
                    if (orientation != lastOrientation)
                    {
                        Console.Clear();
                        lastOrientation = orientation;
                    }

                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine(sb.ToString());

                    // overlay head tracking
                    if (orientation == initialOrientation)
                    {
                        // body tracking works only in initially configured orientation
                        Console.BackgroundColor = ConsoleColor.Red;
                        foreach (var body in bodies)
                        {
                            if (calib.TryGetPixelPosition(body.Joints[JointId.Head].Pose.Origin, out var p))
                            {
                                var x = (int)(p.X * scaleFactorWidth);
                                var y = (int)(p.Y * scaleFactorHeight / 2);
                                if (x > 0 && x < widthOutput && y > 0 && y < heightOutput)
                                {
                                    Console.SetCursorPosition(x, y / 2);
                                    Console.Write(' ');
                                }
                            }
                        }

                        Console.BackgroundColor = ConsoleColor.Black;
                    }
                });
                // Create the webcam component
                var webcam = new MediaCapture(pipeline, 640, 480, 30, true);

                // Create the store component
                var store = PsiStore.Create(pipeline, "AzureKinectSample", "D:\\Stores");

                // Write incoming images in the store at 'Image' track
                store.Write(webcam.Out, "Image");
                // Write incoming audio in the store at 'Audio' track
                store.Write(webcam.Out, "Audio");
                // Write incoming images in the store at 'KinectImage' track
                store.Write(kinectAzureRemoteConnector.OutColorImage, "KinectImage");
                // Write incoming bodies in the store at 'KinectBodies' track
                store.Write(kinectAzureRemoteConnector.OutBodies, "KinectBodies");
                // Write incoming bodies in the store at 'KinectDepth' track
                store.Write(kinectAzureRemoteConnector.OutDepthImage, "KinectDepth");

                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Clear();
                pipeline.RunAsync();
                Console.ReadLine(); // press Enter to end
            }
        }
    }
}
