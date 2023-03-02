// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace HelloWorld
{
    using System;
    using KeyboardReader;
    using Microsoft.Psi;
    using Microsoft.Psi.Components;

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

            timer.Out.Pair(reader.Out).Do(((TimeSpan, string) data, Envelope envelope) => { Console.WriteLine($"{data.Item1}: {data.Item2}"); });

            // Run the pipeline, but don't block here
            p.RunAsync();
        }
    }
}