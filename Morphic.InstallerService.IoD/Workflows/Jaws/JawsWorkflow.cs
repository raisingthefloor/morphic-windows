using IoDCLI.Workflows.Exe;
using IoDCLI.Workflows.Msi;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Morphic.Core;
using Morphic.InstallerService.Contracts;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace IoDCLI.Workflows.Jaws
{
    public class JawsWorkflow : WorkflowBase<JawsError>
    {
        private static readonly Package[] _prereqs =
        {
            new Package { Url = @"..\vcredist2010_x64.exe", Hash = "C6CD2D3F0B11DC2A604FFDC4DD97861A83B77E21709BA71B962A47759C93F4C8", HashType = "SHA256", CommandArguments = "/q", UninstallCommand = "/q /uninstall" },
            new Package { Url = @"..\vcredist2013_x64.exe", Hash = "E554425243E3E8CA1CD5FE550DB41E6FA58A007C74FAD400274B128452F38FB8", HashType = "SHA256", CommandArguments = "/q", UninstallCommand = "/q /uninstall" },
            new Package { Url = @"..\vcredist2019_x64.exe", Hash = "52B196BBE9016488C735E7B41805B651261FFA5D7AA86EB6A1D0095BE83687B2", HashType = "SHA256", CommandArguments = "/q", UninstallCommand = "/q /uninstall" },
        };

        private static readonly Package[] _msiToInstall =
        {
            new Package { Url = @"x86\Eloquence.msi", Hash = "D104F234D6637B8FD39333EE67F2785DDFF4F08320BDB84FFA7C7CC68F4403A2", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\VideoAccessibility.msi", Hash = "2E190EE7B9A69153FD9861DF95DB7C5D7D1036CBE76D57E050F8FCDD0DE9166E", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\Utilities.msi", Hash = "271960BDE7DAF83E9D4FC3544E799DB2EF5A267091DC5EDA338444E4B721C751", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\FSElevation.msi", Hash = "0694DA3C1CEB194437DA5E4AD8E414B90D0E153A8F8AB51081FB814F518A20F0", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\FsSynth.msi", Hash = "3EABE7345AB11EFA0E92BC5A81F800BE309C1B05B18D27E68145A33B567A1C76", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\UIAHooks.msi", Hash = "FFB7641D94A205D2608A546C709414B8C8E424455AD2734FAD8B43CDB27D9CF2", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\HookManager.msi", Hash = "21BA31EF3AFD2A5FE8ACDBFFCD1B74D5A06E77EC8DA03208C262C74681630617", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\AccEventCache.msi", Hash = "A86A56182C2FD837ADDF615EC049E6A8014B67EC9BA5478DF25E4C6A7AF53934", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\fsWow64Proxy.msi", Hash = "F11C5B918D7B8B15CA468F71D060C414A056852C45B96E860B898938556559C8", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\FSReader.msi", Hash = "458A705DF7A9F54A673B27695A1716FA47C75C7AD0854EA5C39BE5F06507745F", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\TableOfContents.msi", Hash = "656C59289CF75F6A0CC16F50C0301E5D9C614AA735FB6BA6D45E32A80C2D5A9F", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x86\FSOmnipage.msi", Hash = "F85242B5B6115F92A22097EE188345A81E56632FE8AAB72237629503F9951DFE", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x86\FSOcr.msi", Hash = "52538A0829EF65CB7DE63CDAADC48F0EBE7563BDA0057FE8CC8332B1D2B988D2", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\FSOcr.msi", Hash = "93A3118B67428DE6AB75D321D2F075061618A84D393B527A037D0E548E395A2B", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x86\FSOcrTombstone.msi", Hash = "A96A4ED0D0C99A0955CF4D8EAB2C4791D839690422205B1EBC530741B8E3CAC1", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\FSSupportTool.msi", Hash = "AE6A548F31692573D276D51E957A688F0CD8A4DBF7D352D42BAEC61F72905CA0", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\ErrorReporting.msi", Hash = "8B622489583269CBE0EB82F34858BFB398EF9552903798AD0D9E519670E5691E", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\TouchServer.msi", Hash = "0C259135694EF3C15E4225076563FDA01B6AB45257CC0EABD2FEE2336850CF8B", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\FusionInterface.msi", Hash = "5FE0A671965A7AAD2785DA4785F83D9B60A43CAD6B6963452BF7259F491E0AE3", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\Authorization.msi", Hash = "0EB293FE9ABBAE52B215C9AA4E17C322C43277E6CCA0B5C39BF7351519E69AEE", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x86\KeyboardManager.msi", Hash = "1DA9A48C61DE27D2493984A96A777C0D8BC3626DBD3ECEBA122D932967C397D8", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\FSCam.msi", Hash = "6C3E5F06994CACCB9007D78E1776430FEC27D3071FB9A928CC7DFBD98F4F7CCF", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\RdpSupport.msi", Hash = "B7FFBCB5A5C8FCA5C51EF50F0BA39CBF6A67319D8398FBB2B5691C43E6287CCE", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\Telemetry.msi", Hash = "EA272C80B0223792A4C670EA7E6488FCA87F4312519B3E26A8707587D8A89278", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\VoiceAssistant.msi", Hash = "7773A9C393CBE724C9836C120337A61C676EA4A40ABFAEEC6A1086F1F11559F0", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\JAWSBase.msi", Hash = "F2FDBE1B3AF3C65986FC3F79C24C5D7BF8E474E63137FDB926B358DB8EF973C7", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1 TANDEM=1 REMOTE_ONLY=0" },
            new Package { Url = @"x64\JAWSLanguage.msi", Hash = "432AB1D02EABDAEC22FB2BE40F4B94C14FAD3F284246D40CEF569DB4565050A5", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1 PRIMARY_LANGUAGE=1 TANDEM=1" },
            new Package { Url = @"x64\Xerces.msi", Hash = "7D1633530971FA62615B61A7BDF12D20342FD5AC515F5EB29BB8C3A22ECF36E2", HashType = "SHA256", CommandArguments = "REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
            new Package { Url = @"x64\JAWSStart.msi", Hash = "ABCF5DA4A16FE5705642A9FE5C6A6E9453A085ABBA4818329EDC4F00315009D4", HashType = "SHA256", CommandArguments = "ALLUSERS=1 REBOOT=ReallySuppress ARPSYSTEMCOMPONENT=1" },
        };

        public string SourcePath { get; set; }

        public JawsWorkflow(Package package, EventHandler<ProgressEventArgs> progressHandler, ILogger logger) : base(package, progressHandler, logger)
        {
            SourcePath = @"C:\FS setup\OfflineSetup\enu";
        }

        public override string GetFileExtension()
        {
            return "exe";
        }

        public override async Task<IMorphicResult<bool, JawsError>> Install()
        {
            try
            {

                foreach(var prereq in _prereqs)
                {
                    prereq.Url = Path.Combine(SourcePath, prereq.Url);

                    var workflow = new ExeWorkflow(prereq, (sender, args) => HandleProgress(args), Logger);

                    await workflow.Install();
                }

                //Download();

                //if (Validate())
                CreateRegistryKeys();

                foreach (var msiToInstall in _msiToInstall)
                {
                    msiToInstall.Url = Path.Combine(SourcePath, msiToInstall.Url);

                    var workflow = new MsiWorkflow(msiToInstall, true, (sender, args) => HandleProgress(args), Logger);

                    await workflow.Install();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error has occured.");
            }

            return IMorphicResult<bool, JawsError>.SuccessResult(true);
        }

        private void CreateRegistryKey(RegistryKey root, string path)
        {
            var reg = root.OpenSubKey(path, true);
            if (reg == null)
            {
                reg = root.CreateSubKey(path);
            }
        }

        public override async Task<IMorphicResult<bool, JawsError>> Uninstall()
        {
            try
            {
                //Download();

                //if (Validate())
                await UninstallJaws();

                CleanupDirectories();
                DeleteRegistryKeys();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error has occured.");
            }

            return IMorphicResult<bool, JawsError>.SuccessResult(true);
        }

        private void HandleProgress(ProgressEventArgs progressEventArgs)
        {
            Logger.LogInformation($"Progress: {progressEventArgs.Value}%");
        }

        private async Task UninstallJaws()
        {
            foreach (var msiToInstall in _msiToInstall)
            {
                msiToInstall.Url = Path.Combine(SourcePath, msiToInstall.Url);

                var workflow = new MsiWorkflow(msiToInstall, true, (sender, args) => HandleProgress(args), Logger);

                var result = await workflow.Uninstall();

                if(result.IsError)
                {
                    Logger.LogError($"An error has occured while attempting to uninstall jaws. '{result.Error}'.");
                }
            }
        }

        private void CreateRegistryKeys()
        {
            CreateRegistryKey(Registry.CurrentUser, @"Software\Freedom Scientific");
            CreateRegistryKey(Registry.LocalMachine, @"Software\Freedom Scientific");
        }

        private void DeleteRegistryKeys()
        {
            Registry.CurrentUser.OpenSubKey("Software", true).DeleteSubKeyTree("Freedom Scientific");
            Registry.LocalMachine.OpenSubKey("Software", true).DeleteSubKeyTree("Freedom Scientific");
        }

        private void CleanupDirectories()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            var directoriesToDelete = new[] { "Freedom Scientific", "Freedom Scientific Installation Information" };

            foreach (var directory in directoriesToDelete)
            {
                try
                {
                    RecursiveDelete(new DirectoryInfo(Path.Combine(programFiles, directory)));
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"An error has occured while attempting to delete '{directory}'.");
                }
            }
        }

        private void RecursiveDelete(DirectoryInfo baseDir)
        {
            if (!baseDir.Exists)
                return;

            foreach (var dir in baseDir.EnumerateDirectories())
            {
                RecursiveDelete(dir);
            }

            baseDir.Delete(true);
        }
    }
}
