namespace Morphic.InstallerService.Contracts
{
    public class Package 
    {
        public bool Enabled { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string CommandArguments { get; set; }
        public string UninstallCommand { get; set; }
        public string Url { get; set; }
        public string Hash { get; set; }
        public string HashType { get; set; }
    }
}
