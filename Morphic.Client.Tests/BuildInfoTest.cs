using Morphic.Client.About;
using System;
using System.Globalization;
using System.Windows.Automation.Peers;
using Xunit;

namespace Morphic.Client.Tests
{
    public class BuildInfoTest : IDisposable
    {
        AboutWindow about;

        public BuildInfoTest()
        {
            BuildInfo info = new BuildInfo();
            info.BuildTime = "thebuildtime";
            info.Commit = "thecommit";
            about = new AboutWindow(info);
            about.Show();
        }

        [StaFact]
        public void InfoTest()
        {
            var peer = new WindowAutomationPeer(about);
            System.Threading.Thread.Sleep(5000);
        }

        public void Dispose()
        {
            about.Close();
        }
    }
}
