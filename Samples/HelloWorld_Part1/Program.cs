// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace HelloWorld
{
    using System;
    using KeyboardReader;
    using Microsoft.Psi;

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

            // Create a timer component that produces a message every second
            var timer = Timers.Timer(p, TimeSpan.FromSeconds(1));

            KeyboardReader reader = new KeyboardReader(p);
            reader.Out.Do((data, enveloppe) => { Console.WriteLine($"Message @ {enveloppe.OriginatingTime} : {data}"); });

            // Run the pipeline, but don't block here
            p.RunAsync();
        }
    }
}