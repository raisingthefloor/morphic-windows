// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

namespace Morphic.Speech
{
    using System;
    using System.Speech.Synthesis;
    using System.Threading;

    /**
     * Helper program for Morphic.Client. This needs to be in a separate process, because System.Speech.Synthesis
     * is only available in .NET Framework 4.
     *
     * Start with the MORPHIC_SPEECH environment variable set to the text to speak. Speech can be controlled on stdin
     * using "pause", "resume", "toggle", or "stop".
     */
    internal class Program
    {
        public static void Main(string[] args)
        {
            string text = Environment.GetEnvironmentVariable("MORPHIC_SPEECH");
            if (string.IsNullOrEmpty(text))
            {
                Console.WriteLine("MORPHIC_SPEECH is not set");
                Environment.ExitCode = 1;
            }
            else
            {
                Speak(text);
            }
        }

        private static SpeechSynthesizer speech;


        /// <summary>
        /// Handles the control commands coming from stdin.
        /// </summary>
        private static void GetInput()
        {
            do
            {
                string command = Console.ReadLine();
                switch (command)
                {
                    case "resume" when speech.State == SynthesizerState.Paused:
                    case "toggle" when speech.State == SynthesizerState.Paused:
                        speech.Resume();
                        break;
                    case "pause" when speech.State == SynthesizerState.Speaking:
                    case "toggle" when speech.State == SynthesizerState.Speaking:
                        speech.Pause();
                        break;
                    case "stop":
                        speech.SpeakAsyncCancelAll();
                        return;
                }
            } while (speech.State != SynthesizerState.Ready);
        }

        /// <summary>
        /// Start speaking the given text.
        /// </summary>
        /// <param name="text">What to say.</param>
        private static void Speak(string text)
        {
            speech = new SpeechSynthesizer();

            speech.SetOutputToDefaultAudioDevice();
            speech.SpeakAsync(text);
            speech.SpeakCompleted += (sender, args) => { Environment.Exit(0); };

            Thread inputThread = new Thread(GetInput)
            {
                IsBackground = true
            };

            inputThread.Start();
            inputThread.Join();
        }

    }
}