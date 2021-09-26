namespace IoDCLI.Workflows.MsiX
{
    public class MSIXErrorForm
    {
        public MSIXErrorType Type { get; set; }
        public MSIXErrorCode ErrorCode { get; set; }
        public string VerboseLog { get; set; }
    }
}
