// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Samples.WebcamAndKinect
{ 
    using System;
    using System.ComponentModel;
    using System.Text;
    using System.Windows;
    using System.Windows.Media.Imaging;
    using Microsoft.Azure.Kinect.BodyTracking;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Media;
    using RemoteConnectors;

    /// <summary>
    /// Webcam with audio sample program.
    /// </summary>
    public partial class MainWindow
    {
        private Pipeline pipeline;
        private WriteableBitmap bitmap;
        private IntPtr bitmapPtr;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();
            this.Loaded += this.MainWindow_Loaded;
            this.Closing += this.MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Create the \psi pipeline
            this.pipeline = Pipeline.Create("WebcamAndKinect");

            // Create the store component
            var store = PsiStore.Open(pipeline, "WebcamAndKinect", "D:\\Stores");

            // Open images in the store at 'Image' track
            var webcamImage = store.OpenStream<Shared<EncodedImage>>("ImageW");
            // Open audio in the store at 'Audio' track
            var webcamAudio = store.OpenStream<AudioBuffer>("AudioW");
            // Write incoming images in the store at 'Image' track
            var kinectImage = store.OpenStream<Shared<EncodedImage>>("ImageK");
            // Write incoming audio in the store at 'Audio' track
            var kinectAudio = store.OpenStream<AudioBuffer>("AudioK");

            var acousticFeaturesWebcam = new AcousticFeaturesExtractor(this.pipeline);
            webcamAudio.PipeTo(acousticFeaturesWebcam);

            var acousticFeaturesKinect = new AcousticFeaturesExtractor(this.pipeline);
            kinectAudio.PipeTo(acousticFeaturesKinect);

            kinectImage.Decode(new ImageFromBitmapStreamDecoder()).Pair(acousticFeaturesKinect.LogEnergy).Join(webcamImage.Decode(new ImageFromBitmapStreamDecoder())).Pair(acousticFeaturesWebcam.LogEnergy).Do(message =>
            {
                var (kinect, audioKinect, webcam, audioWebcam) = message;

                if (audioKinect > audioWebcam)
                {
                    this.DrawFrame((kinect, audioKinect));
                }
                else
                {
                    this.DrawFrame((webcam, audioWebcam));
                }
            });

            // Start the pipeline running
            this.pipeline.RunAsync(ReplayDescriptor.ReplayAllRealTime);
        }

        private void DrawFrame((Shared<Image> Image, float AudioLevel) frame)
        {
            // copy the frame image to the display bitmap
            this.UpdateBitmap(frame.Image);

            // clamp level to between 0 and 20
            var audioLevel = frame.AudioLevel < 0 ? 0 : frame.AudioLevel > 20 ? 20 : frame.AudioLevel;

            // redraw on the UI thread
            this.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    this.UpdateDisplayImage();
                    this.displayLevel.Value = audioLevel;
                    this.displayText.Text = audioLevel.ToString("0.0");
                }));
        }

        private void UpdateBitmap(Shared<Image> image)
        {
            // create a new bitmap if necessary
            if (this.bitmap == null)
            {
                // WriteableBitmap must be created on the UI thread
                this.Dispatcher.Invoke(() =>
                {
                    this.bitmap = new WriteableBitmap(
                        image.Resource.Width,
                        image.Resource.Height,
                        300,
                        300,
                        image.Resource.PixelFormat.ToWindowsMediaPixelFormat(),
                        null);

                    this.image.Source = this.bitmap;
                    this.bitmapPtr = this.bitmap.BackBuffer;
                });
            }

            // update the display bitmap's back buffer
            image.Resource.CopyTo(this.bitmapPtr, image.Resource.Width, image.Resource.Height, image.Resource.Stride, image.Resource.PixelFormat);
        }

        private void UpdateDisplayImage()
        {
            // invalidate the entire area of the bitmap to cause the display image to be redrawn
            this.bitmap.Lock();
            this.bitmap.AddDirtyRect(new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight));
            this.bitmap.Unlock();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.pipeline.Dispose();
        }
    }
}
