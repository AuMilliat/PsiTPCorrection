// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace HelloWorld
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using KeyboardReader;
    using Microsoft.Psi;
    using Microsoft.Psi.Components;
    using Microsoft.Psi.Remoting;

    /// <summary>
    /// Hello world sample.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static void Main()
        {
            // Create a pipeline
            var p = Pipeline.Create();

            RemoteImporter importer = new RemoteImporter(p, "localhost");
            RemoteExporter exporter = new RemoteExporter(p, 11412, TransportKind.Tcp);
            KeyboardReader reader = new KeyboardReader(p);

            if (!importer.Connected.WaitOne(-1))
            {
                throw new Exception("could not connect to server");
            }

            exporter.Exporter.Write(reader.Out, "Topic");
            importer.Importer.OpenStream<System.Numerics.Vector3>("Position").Do((System.Numerics.Vector3 data, Envelope envelope) => { Console.WriteLine($"{envelope.OriginatingTime} : {data}"); });

            // Run the pipeline, but don't block here
            p.RunAsync();
        }
    }
}