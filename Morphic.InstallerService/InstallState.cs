using System;
using System.IO;
using System.Text.Json;

namespace Morphic.InstallerService
{
    public class InstallState
    {
        private static readonly string _location = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Morphic", "AToD");
        private static readonly string _installStateFilePath = Path.Combine(_location, "morphicatodinstallstate.tmp");

        private static readonly Lazy<InstallState> _instance = new(() =>
        {
            InstallState installState;

            if (!Directory.Exists(_location))
            {
                Directory.CreateDirectory(_location);
            }

            if (File.Exists(_installStateFilePath))
                installState = Load();
            else
                installState = new InstallState();

            return installState;
        });

        private long _sequenceNumber;
        private bool _isInstalling;
        private byte[] _certificatePassword;

        public long SequenceNumber
        {
            get { return _sequenceNumber; }
            set
            {
                _sequenceNumber = value;
                Save();
            }
        }

        public bool IsInstalling
        {
            get { return _isInstalling; }
            set
            {
                _isInstalling = value;
                Save();
            }
        }

        public byte[] CertificatePassword
        {
            get { return _certificatePassword; }
            set
            {
                _certificatePassword = value;
                Save();
            }
        }

        public static InstallState GetInstance()
        {
            return _instance.Value;
        }

        public void Save()
        {
            using var createStream = File.Create(_installStateFilePath);
            using Utf8JsonWriter writer = new(createStream);

            JsonSerializer.Serialize(writer, this);
        }

        private static InstallState Load()
        {
            var jsonReadOnlySpan = File.ReadAllBytes(_installStateFilePath);

            return JsonSerializer.Deserialize<InstallState>(jsonReadOnlySpan);
        }
    }
}
