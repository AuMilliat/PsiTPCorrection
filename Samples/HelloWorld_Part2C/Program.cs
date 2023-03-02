// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace HelloWorld
{
    using System;
    using KeyboardReader;
    using Microsoft.Psi;
    using Microsoft.Psi.Components;

    public class ConsoleComponent : Subpipeline
    {
        protected Connector<string> InKeyboardConnector;
        public Receiver<string> InKeyboard => InKeyboardConnector.In;

        protected Connector<TimeSpan> InTickConnector;
        public Receiver<TimeSpan> InTick => InTickConnector.In;

        public ConsoleComponent(Pipeline parent, string name = "ConsoleComponent", DeliveryPolicy policy = null)
            : base(parent, name, policy)
        {
            InKeyboardConnector = parent.CreateConnector<string>(nameof(InKeyboardConnector));
            InTickConnector = parent.CreateConnector<TimeSpan>(nameof(InTickConnector));

            InTickConnector.Pair(InKeyboardConnector).Do(Process);
            //InKeyboardConnector.Pair(InTickConnector).Do(Process);
        }

        private void Process((TimeSpan, string) data, Envelope envelope)
        {
            Console.WriteLine($"{data.Item1}: {data.Item2}");
        }

        private void Process((string, TimeSpan) data, Envelope envelope)
        {
            Console.WriteLine($"{data.Item2}: {data.Item1}");
        }
    }

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

            ConsoleComponent console = new ConsoleComponent(p);
            KeyboardReader reader = new KeyboardReader(p);

            timer.Out.PipeTo(console.InTick);
            reader.Out.PipeTo(console.InKeyboard);

            // Run the pipeline, but don't block here
            p.RunAsync();
        }
    }
}