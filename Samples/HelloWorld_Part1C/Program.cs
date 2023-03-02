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
        public class ConsoleComponent : Subpipeline
        {
            protected Connector<string> InKeyboardConnector;
            public Receiver<string> InKeyboard => InKeyboardConnector.In;

           public ConsoleComponent(Pipeline parent, string name = "ConsoleComponent", DeliveryPolicy policy = null)
                : base(parent, name, policy)
            {
                InKeyboardConnector = parent.CreateConnector<string>(nameof(InKeyboardConnector));

                InKeyboardConnector.Do(Process);
            }
            private void Process(string data, Envelope envelope)
            {
                Console.WriteLine($"Message @ {envelope.OriginatingTime}: {data}");
            }
        }

        /// <summary>
        /// Main entry point.
        /// </summary>
        public static void Main()
        {
            // Create a pipeline
            var p = Pipeline.Create();

            KeyboardReader keyboardReader = new KeyboardReader(p);
            ConsoleComponent consoleComponent = new ConsoleComponent(p);

            keyboardReader.Out.PipeTo(consoleComponent.InKeyboard);

            // Run the pipeline, but don't block here
            p.RunAsync();
        }
    }
}