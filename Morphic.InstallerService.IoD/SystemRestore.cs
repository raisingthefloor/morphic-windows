using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Morphic.InstallerService.IoD;
using System;
using System.Globalization;
using System.Management;

namespace IoDCLI
{
    public class SystemRestore : IDisposable
    {
        private ManagementClass? _wmiClass;
        private readonly ILogger _logger;

        public SystemRestore(ILogger logger)
        {
            _logger = logger;
        }

        private static string GetSystemDrive()
        {
            var text = Environment.ExpandEnvironmentVariables("%SystemDrive%");
            return string.Concat(new string[] { text, "\\" });
        }

        public void SetInfiniteCreationFrequency()
        {
            // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore
            // SystemRestorePointCreationFrequency DWORD
            // 0 - Infinite

            var systemRestoreSubKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", true);
            if (systemRestoreSubKey == null)
            {
                systemRestoreSubKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore");
            }

            systemRestoreSubKey.SetValue("SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);
        }

        public void Enable()
        {
            try
            {
                var managementScope = new ManagementScope("\\root\\default");
                managementScope.Connect();

                _wmiClass = new ManagementClass("SystemRestore")
                {
                    Scope = managementScope
                };

                var systemDrive = GetSystemDrive();

                var num = Convert.ToInt32(_wmiClass.InvokeMethod("Enable", new string[] { systemDrive }), CultureInfo.CurrentCulture);
            }
            finally
            {
                if (_wmiClass != null)
                {
                    _wmiClass.Dispose();
                }
            }
        }

        public void Disable()
        {
            try
            {
                var managementScope = new ManagementScope("\\root\\default");
                managementScope.Connect();

                _wmiClass = new ManagementClass("SystemRestore")
                {
                    Scope = managementScope
                };

                var systemDrive = GetSystemDrive();

                var num = Convert.ToInt32(_wmiClass.InvokeMethod("Disable", new string[] { systemDrive }), CultureInfo.CurrentCulture);
            }
            finally
            {
                if (_wmiClass != null)
                {
                    _wmiClass.Dispose();
                }
            }
        }

        public uint Cancel(long sequenceNumber)
        {
            var rpInfo = new RestorePointInfo();
            StateManagerStatus rpStatus = new StateManagerStatus();

            try
            {
                rpInfo.EventType = RestorePointEventType.EndSystemChange;
                rpInfo.RestorePointType = RestoreType.CancelledOperation;
                rpInfo.SequenceNumber = sequenceNumber;

                NativeMethods.SRSetRestorePointW(ref rpInfo, out rpStatus);
            }
            catch (DllNotFoundException)
            {
                return 99;
            }

            return rpStatus.Status;
        }

        public uint Start(string description, RestoreType restoreType, out long sequenceNumber)
        {
            var rpInfo = new RestorePointInfo();
            StateManagerStatus rpStatus;

            try
            {
                rpInfo.EventType = RestorePointEventType.BeginSystemChange;
                rpInfo.RestorePointType = restoreType;
                rpInfo.SequenceNumber = 0;
                rpInfo.Description = description;

                var result = NativeMethods.SRSetRestorePointW(ref rpInfo, out rpStatus);

                var s = result ? "Succeeded" : "Failed";
                _logger.LogInformation($"Restore point creation {s}");

                _logger.LogInformation($"SRSetRestorePointW status: {rpStatus.Status}");
                _logger.LogInformation($"SRSetRestorePointW rpStatus.llSequenceNumber: {rpStatus.SequenceNumber}");
                _logger.LogInformation($"SRSetRestorePointW rpInfo.llSequenceNumber: {rpInfo.SequenceNumber}");
            }
            catch (DllNotFoundException)
            {
                sequenceNumber = 0;
                return 99;
            }

            sequenceNumber = rpStatus.SequenceNumber;

            return rpStatus.Status;
        }

        public uint End(long sequenceNumber)
        {
            var rpInfo = new RestorePointInfo();
            StateManagerStatus rpStatus;

            try
            {
                rpInfo.EventType = RestorePointEventType.EndSystemChange;
                rpInfo.SequenceNumber = sequenceNumber;

                NativeMethods.SRSetRestorePointW(ref rpInfo, out rpStatus);
            }
            catch (DllNotFoundException)
            {
                return 99;
            }

            return rpStatus.Status;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposing && _wmiClass != null)
            {
                _wmiClass.Dispose();
            }
        }
    }
}
