// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace HelloWorld
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Numerics;
    using KeyboardReader;
    using Microsoft.Psi;
    using Microsoft.Psi.Interop.Rendezvous;
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

            string host = "127.0.0.1";
            var server = new RendezvousServer();
            var process = new Rendezvous.Process("Console");

            RemoteClockExporter exporter = new RemoteClockExporter(11511);
            process.AddEndpoint(exporter.ToRendezvousEndpoint(host));

            RemoteExporter remoteExporter = new RemoteExporter(p, 11412, TransportKind.Tcp);
            var timer = Timers.Timer(p, TimeSpan.FromSeconds(5));
            remoteExporter.Exporter.Write(timer.Out, "PingInter");
            process.AddEndpoint(remoteExporter.ToRendezvousEndpoint(host));

            server.Rendezvous.ProcessAdded += (_, process) =>
            {
                Console.WriteLine($"Process added: {process.Name}");
                if (process.Name.Contains("Console"))
                    return;
                Subpipeline subP = new Subpipeline(p, process.Name);
                var clone = process.Endpoints.DeepClone();
                foreach (var endpoint in clone)
                {
                    if (endpoint is Rendezvous.RemoteExporterEndpoint remoteEndpoint)
                    {
                        RemoteImporter remoteImporter = remoteEndpoint.ToRemoteImporter(subP);
                        if (remoteImporter.Connected.WaitOne() == false)
                            continue;
                        foreach (Rendezvous.Stream stream in remoteEndpoint.Streams)
                        {
                            Console.WriteLine($"Stream : {stream.StreamName}");
                            if (stream.StreamName is "PlayerPosition")
                            {
                                remoteImporter.Connected.WaitOne();
                                var pos = remoteImporter.Importer.OpenStream<Vector3>("PlayerPosition");
                                pos.Do((vec, env) => { Console.WriteLine("posImp : " + vec.ToString()); });
                            }
                            if (stream.StreamName is "Spawn")
                            {
                                remoteImporter.Connected.WaitOne();
                                var ids = remoteImporter.Importer.OpenStream<long>("Spawn");
                                ids.Do((id, env) => { Console.WriteLine("Spawn : " + id.ToString()); });
                            }
                        }
                    }
                }
                subP.RunAsync();
            };

            var res = server.Rendezvous.TryAddProcess(process);
            server.Start();

            // Waiting for an out key
            Console.WriteLine("Press any key to stop the application.");
            Console.ReadLine();
            // Stop correctly the pipeline.
            p.Dispose();
        }
    }
}