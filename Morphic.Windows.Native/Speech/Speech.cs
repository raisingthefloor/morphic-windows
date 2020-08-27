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

namespace Morphic.Windows.Native
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Performs text to speech. 
    /// </summary>
    public class Speech
    {
        public static readonly Speech Default = new Speech();
        
        /// <summary>Used to tell the speech process to pause/resume.</summary>
        private EventWaitHandle toggleSpeech = new EventWaitHandle(false, EventResetMode.AutoReset, "morphic-speech");
        /// <summary>Cancellation for the speech process.</summary>
        private CancellationTokenSource? cancellation;

        /// <summary>true if currently speaking.</summary>
        public bool Active => this.cancellation != null;

        private Speech()
        {
        }

        /// <summary>
        /// Says the given text.
        /// </summary>
        /// <param name="text">The text to say.</param>
        /// <returns>Task, completing when the speech is done.</returns>
        public Task<bool> SpeakText(string text)
        {
            if (this.cancellation != null)
            {
                this.StopSpeaking();
            }
            
            // The speech API is only available for .NET framework, so access it via powershell.
            // This script invokes the speech synthesizer, waiting for a signal from this process to pause/resume
            // the speech.
            string script =             
                "Add-Type -AssemblyName System.speech;"
                + "$speech = New-Object System.Speech.Synthesis.SpeechSynthesizer;"
                + "$semaphore = [System.Threading.EventWaitHandle]::OpenExisting(\"morphic-speech\");"
                + "$speech.SetOutputToDefaultAudioDevice();"
                + "$speech.SpeakAsync($Env:MORPHIC_SPEECH);"
                + "while($speech.State -ne [System.Speech.Synthesis.SynthesizerState]::Ready)"
                + "{"
                + "    if ($semaphore.WaitOne(200)) {"
                + "        if ($speech.State -eq [System.Speech.Synthesis.SynthesizerState]::Paused) {"
                + "            $speech.Resume();"
                + "        } else {"
                + "            $speech.Pause();"
                + "        }"
                + "    }"
                + "};";

            Process process = Process.Start(new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                ArgumentList = {
                    "-ExecutionPolicy", "bypass",
                    "-NoProfile", "-NonInteractive", "-Command", script
                },
                Environment = { {"MORPHIC_SPEECH", text} },
                CreateNoWindow = true,
            })!;

            this.cancellation = new CancellationTokenSource();
            TaskCompletionSource<bool> task = new TaskCompletionSource<bool>();
            if (process == null)
            {
                task.SetCanceled();
            }
            else
            {
                // Kill the process when the task is cancelled.
                this.cancellation.Token.Register(process.Kill);
                
                // Complete the task when the process ends.
                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) =>
                {
                    task.SetResult(process.ExitCode == 0);
                    this.cancellation = null;
                    process.Dispose();
                };
            }

            return task.Task;
        }

        /// <summary>Stop talking.</summary>
        public void StopSpeaking()
        {
            if (this.cancellation != null)
            {
                this.cancellation.Cancel();
                this.cancellation.Dispose();
                this.cancellation = null;
            }
        }

        /// <summary>Pause or resume the speech.</summary>
        public void TogglePause()
        {
            this.toggleSpeech.Set();
        }
    }
}
