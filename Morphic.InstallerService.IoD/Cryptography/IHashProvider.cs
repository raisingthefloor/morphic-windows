namespace IoDCLI.Cryptography
{
    public interface IHashProvider
    {
        string Hash(string filePath);
    }
}
