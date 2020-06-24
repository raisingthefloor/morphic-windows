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

using System.IO;
using System.Reflection;

namespace Morphic.Windows.Native
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Performs text to speech. 
    /// </summary>
    public class Speech
    {
        public static readonly Speech Default = new Speech();
        
        /// <summary>Cancellation for the speech process.</summary>
        private CancellationTokenSource? cancellation;

        /// <summary>Speech process standard input</summary>
        private StreamWriter speechStream;

        /// <summary>true if currently speaking.</summary>
        public bool Active => this.cancellation != null;

        public event EventHandler<bool> StateChanged;
        
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

            // The speech synthesizer is only available in .NET Framework 4, so it is accessed in a separate process.
            string speechExe = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Morphic.Speech.exe");

            this.OnStateChanged(true);
            
            Process process = Process.Start(new ProcessStartInfo()
            {
                FileName = speechExe,
                Environment = { {"MORPHIC_SPEECH", text} },
                CreateNoWindow = true,
                RedirectStandardInput = true
            });

            this.speechStream = process.StandardInput;
            
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
                    this.OnStateChanged(false);
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
            this.speechStream.WriteLineAsync("toggle");
        }

        protected virtual void OnStateChanged(bool e)
        {
            this.StateChanged?.Invoke(this, e);
        }
    }
}
