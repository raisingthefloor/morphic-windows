using System;

namespace IoDCLI.Workflows
{
    public class ProgressEventArgs : EventArgs
    {
        public double Value { get; set; }

        public ProgressEventArgs(double progress) 
        { 
            Value = progress;
        }
    }
}
