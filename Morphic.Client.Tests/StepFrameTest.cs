using Moq;
using Morphic.Client.Elements;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;

namespace Morphic.Client.Tests
{
    public class StepFrameTest
    {
        [StaFact]
        public void TestPushPanelInstant()
        {
            var p1 = new Mock<Panel>();
            var p2 = new Mock<Panel>();
            var step = new StepFrame();
            Assert.Null(step.CurrentPanel);
            Assert.False(step.IsAncestorOf(p1.Object));
            Assert.False(step.IsAncestorOf(p2.Object));
            step.PushPanel(p1.Object, false);
            Assert.Equal(p1.Object, step.CurrentPanel);
            Assert.True(step.IsAncestorOf(p1.Object));
            Assert.False(step.IsAncestorOf(p2.Object));
            step.PushPanel(p2.Object, false);
            Assert.Equal(p2.Object, step.CurrentPanel);
            Assert.False(step.IsAncestorOf(p1.Object));
            Assert.True(step.IsAncestorOf(p2.Object));
        }

        [StaFact]
        public void TestPushPanelGradual()
        {
            var p1 = new Mock<Panel>();
            var p2 = new Mock<Panel>();
            var step = new StepFrame();
            step.PushAnimationDurationInSeconds = 1;
            Assert.Null(step.CurrentPanel);
            Assert.False(step.IsAncestorOf(p1.Object));
            Assert.False(step.IsAncestorOf(p2.Object));
            step.PushPanel(p1.Object, true);
            Assert.Equal(p1.Object, step.CurrentPanel);
            Assert.True(step.IsAncestorOf(p1.Object));
            Assert.False(step.IsAncestorOf(p2.Object));
            step.PushPanel(p2.Object, true);
            Assert.Equal(p2.Object, step.CurrentPanel);
            Assert.True(step.IsAncestorOf(p1.Object));
            Assert.True(step.IsAncestorOf(p2.Object));
            //cannot test eventual removal of p1 due to stafact limitations
        }
    }
}
