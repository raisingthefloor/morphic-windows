using Morphic.Client.About;
using System;
using System.Collections.Generic;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace Morphic.Client.Tests
{
    public class AboutWindowTest : IDisposable
    {
        AboutWindow window;

        public AboutWindowTest()
        {
            BuildInfo info = new BuildInfo();
            info.BuildTime = "whatsthetime";
            info.Commit = "alfalfa";
            window = new AboutWindow(info);
            window.Show();
        }

        private void GetAllChildren(AutomationPeer root, List<AutomationPeer> list)
        {
            var ichildren = root.GetChildren();
            if(ichildren != null)
            {
                foreach (var child in ichildren)
                {
                    list.Add(child);
                    GetAllChildren(child, list);
                }
            }
        }

        [StaFact]
        public void TestWindowDisplay()
        {
            var wpeer = new WindowAutomationPeer(window);
            var info = new BuildInfo();
            var kids = new List<AutomationPeer>();
            bool hasAppName = false;
            bool hasVersionLabel = false;
            bool hasBuildLabel = false;
            bool hasContactLink = false;
            GetAllChildren(wpeer, kids);
            foreach (var peer in kids)
            {
                if (peer.GetType() == typeof(LabelAutomationPeer))
                {
                    var labelPeer = (LabelAutomationPeer)peer;
                    Assert.IsType<Label>(labelPeer.Owner);
                    var label = (Label)labelPeer.Owner;
                    if (label.Name == "AppName")
                    {
                        hasAppName = true;
                        Assert.Equal("Morphic for Windows", label.Content);
                    }
                    else if (label.Name == "VersionLabel")
                    {
                        hasVersionLabel = true;
                        Assert.Equal(info.InformationalVersion, label.Content);
                    }
                    else if (label.Name == "BuildLabel")
                    {
                        hasBuildLabel = true;
                        Assert.Equal("(build alfalfa)", label.Content);
                    }
                }
                if(peer.GetType() == typeof(HyperlinkAutomationPeer))
                {
                    var linkPeer = (HyperlinkAutomationPeer)peer;
                    Assert.IsType<Hyperlink>(linkPeer.Owner);
                    var link = (Hyperlink)linkPeer.Owner;
                    hasContactLink = true;
                    Assert.Equal(new Uri("mailto: support@morphic.org"), link.NavigateUri);
                }
            }
            Assert.True(hasAppName);
            Assert.True(hasVersionLabel);
            Assert.True(hasBuildLabel);
            Assert.True(hasContactLink);
        }

        public void Dispose()
        {
            window.Close();
        }
    }
}
