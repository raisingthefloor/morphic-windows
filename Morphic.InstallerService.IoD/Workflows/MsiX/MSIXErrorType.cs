namespace IoDCLI.Workflows.MsiX
{
    public enum MSIXErrorType
    {
        BadParams,  //INTERNAL ERROR: the parameters given are faulty
        ManualHalt, //the user manually stopped the install
        PackageError,   //something is wrong with the package
        OSError,    //something is going on with the target system that prevents installation
        OSReset,    //something is going on, resetting the PC may fix it
        RetryPossible,  //an error was thrown that may resolve if we wait and retry
        OutOfSpace, //there's not enough space on the drive
        AlreadyExists, //there's a version of this program already installed (but not this version)
        MiscFailure
    }
}
