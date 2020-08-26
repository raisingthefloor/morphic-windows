namespace Morphic.Bar
{
    using System;
    using CommandLine;

    public class Options
    {
        public static Options Current { get; set; } = GetOptions();

        private static Options GetOptions()
        {
            Options? result = null;

            Options.Current = new Options();
            Parser.Default
                .ParseArguments<Options>(Environment.GetCommandLineArgs())
                .WithParsed(o => result = o);

            return result ?? new Options();
        }

        [Option("bar")]
        public string? BarFile { get; set; }
    }
}
