using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Morphic.Client.Login;
using Morphic.Core;
using Morphic.Service;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace Morphic.Client.Tests
{
    public class LoginWindowTest : IDisposable
    {
        LoginWindow window;
        Mock<Session> mockSession;

        public LoginWindowTest()
        {
            var options = new SessionOptions();
            options.Endpoint = "https://www.morphic.world";
            options.FrontEnd = "https://www.morphic.world";
            var logger = new NullLogger<LoginWindow>();
            mockSession = new Mock<Session>(options, null, null, null, null, null);
            //mockSession.Setup(x => x.ApplyAllPreferences());
            //mockSession.Setup(x => x.Authenticate(It.IsAny<Core.UsernameCredentials>())).ReturnsAsync((Core.UsernameCredentials credentials) => { return (credentials.Username == "correct" && credentials.Password == "correct"); });
            window = new LoginWindow(mockSession.Object, logger, options);
            window.Show();
        }

        private void GetAllChildren(AutomationPeer root, List<AutomationPeer> list)
        {
            var ichildren = root.GetChildren();
            if (ichildren != null)
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
            var kids = new List<AutomationPeer>();
            var hasEmailFieldLabel = false;
            var hasPasswordFieldLabel = false;
            var hasUsernameField = false;
            var hasPasswordField = false;
            var hasErrorLabel = false;
            var hasLoginButton = false;
            var hasForgotPassword = false;
            GetAllChildren(wpeer, kids);
            foreach (var peer in kids)
            {
                if (peer is LabelAutomationPeer)
                {
                    var labelPeer = (LabelAutomationPeer)peer;
                    Assert.IsType<Label>(labelPeer.Owner);
                    var label = (Label)labelPeer.Owner;
                    if (label.Name == "EmailFieldLabel")
                    {
                        hasEmailFieldLabel = true;
                        Assert.Equal("Email Address", label.Content);
                    }
                    else if (label.Name == "PasswordFieldLabel")
                    {
                        hasPasswordFieldLabel = true;
                        Assert.Equal("Password", label.Content);
                    }
                    else if (label.Name == "ErrorLabel")
                    {
                        hasErrorLabel = true;
                        Assert.Equal("We did not recognize your login. Please try again.", label.Content);
                        Assert.Equal(System.Windows.Visibility.Hidden, label.Visibility);
                    }
                }
                else if (peer is HyperlinkAutomationPeer)
                {
                    var linkPeer = (HyperlinkAutomationPeer)peer;
                    Assert.IsType<Hyperlink>(linkPeer.Owner);
                    var link = (Hyperlink)linkPeer.Owner;
                    if (new Uri("https://www.morphic.world/password/reset") == link.NavigateUri)
                    {
                        hasForgotPassword = true;
                    }
                }
                else if (peer is TextBoxAutomationPeer)
                {
                    var tboxPeer = (TextBoxAutomationPeer)peer;
                    Assert.IsType<TextBox>(tboxPeer.Owner);
                    var tbox = (TextBox)tboxPeer.Owner;
                    if (tbox.Name == "UsernameField")
                    {
                        hasUsernameField = true;
                        Assert.Equal("", tbox.Text);
                    }
                }
                else if (peer is PasswordBoxAutomationPeer)
                {
                    var pboxPeer = (PasswordBoxAutomationPeer)peer;
                    Assert.IsType<PasswordBox>(pboxPeer.Owner);
                    var pbox = (PasswordBox)pboxPeer.Owner;
                    if (pbox.Name == "PasswordField")
                    {
                        hasPasswordField = true;
                        Assert.Equal("", pbox.Password);
                    }
                }
                else if (peer is ButtonAutomationPeer)
                {
                    var buttonPeer = (ButtonAutomationPeer)peer;
                    Assert.IsType<Button>(buttonPeer.Owner);
                    var button = (Button)buttonPeer.Owner;
                    if(button.Name == "LoginButton")
                    {
                        hasLoginButton = true;
                        Assert.Equal("Apply My Settings", button.Content);
                    }
                }
            }
            Assert.True(hasEmailFieldLabel);
            Assert.True(hasPasswordFieldLabel);
            Assert.True(hasUsernameField);
            Assert.True(hasPasswordField);
            Assert.True(hasErrorLabel);
            Assert.True(hasLoginButton);
            Assert.True(hasForgotPassword);
        }

        [StaFact]
        public void TestLoginButtonFirstTry()
        {
            Assert.Equal("Needs an override", "Session");
            var wpeer = new WindowAutomationPeer(window);
            var kids = new List<AutomationPeer>();
            LabelAutomationPeer errorLabel = null;
            TextBoxAutomationPeer usernameBox = null;
            PasswordBoxAutomationPeer passwordBox = null;
            ButtonAutomationPeer loginb = null;
            GetAllChildren(wpeer, kids);
            foreach (var peer in kids)
            {
                if (peer is LabelAutomationPeer)
                {
                    var labelPeer = (LabelAutomationPeer)peer;
                    Assert.IsType<Label>(labelPeer.Owner);
                    var label = (Label)labelPeer.Owner;
                    if (label.Name == "ErrorLabel")
                    {
                        errorLabel = labelPeer;
                    }
                }
                else if (peer is TextBoxAutomationPeer)
                {
                    var tboxPeer = (TextBoxAutomationPeer)peer;
                    Assert.IsType<TextBox>(tboxPeer.Owner);
                    var tbox = (TextBox)tboxPeer.Owner;
                    if (tbox.Name == "UsernameField")
                    {
                        usernameBox = tboxPeer;
                    }
                }
                else if (peer is PasswordBoxAutomationPeer)
                {
                    var pboxPeer = (PasswordBoxAutomationPeer)peer;
                    Assert.IsType<PasswordBox>(pboxPeer.Owner);
                    var pbox = (PasswordBox)pboxPeer.Owner;
                    if (pbox.Name == "PasswordField")
                    {
                        passwordBox = pboxPeer;
                    }
                }
                else if (peer is ButtonAutomationPeer)
                {
                    var buttonPeer = (ButtonAutomationPeer)peer;
                    Assert.IsType<Button>(buttonPeer.Owner);
                    var button = (Button)buttonPeer.Owner;
                    if (button.Name == "LoginButton")
                    {
                        loginb = buttonPeer;
                    }
                }
            }

            var password = (PasswordBox)passwordBox.Owner;
            password.Password = "correct";
            var username = (TextBox)usernameBox.Owner;
            username.Text = "correct";

            var p2 = (PasswordBox)passwordBox.Owner;
            Assert.Equal("correct", p2.Password);

            Thread.Sleep(100);
            loginb.Owner.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            mockSession.Verify(p => p.Authenticate(It.IsAny<UsernameCredentials>()), Times.Exactly(1));
            mockSession.Verify(p => p.ApplyAllPreferences(), Times.Exactly(1));
            Assert.False(errorLabel.Owner.IsVisible);
            //Thread.Sleep(2000);
            //Assert.False(window.IsLoaded);    //behavior inconsistent
        }

        [StaFact]
        public void TestLoginButtonManyTries()
        {
            Assert.Equal("Needs an override", "Session");
            var wpeer = new WindowAutomationPeer(window);
            var kids = new List<AutomationPeer>();
            LabelAutomationPeer errorLabel = null;
            TextBoxAutomationPeer usernameBox = null;
            PasswordBoxAutomationPeer passwordBox = null;
            ButtonAutomationPeer loginButton = null;
            GetAllChildren(wpeer, kids);
            foreach (var peer in kids)
            {
                if (peer is LabelAutomationPeer)
                {
                    var labelPeer = (LabelAutomationPeer)peer;
                    Assert.IsType<Label>(labelPeer.Owner);
                    var label = (Label)labelPeer.Owner;
                    if (label.Name == "ErrorLabel")
                    {
                        errorLabel = labelPeer;
                    }
                }
                else if (peer is TextBoxAutomationPeer)
                {
                    var tboxPeer = (TextBoxAutomationPeer)peer;
                    Assert.IsType<TextBox>(tboxPeer.Owner);
                    var tbox = (TextBox)tboxPeer.Owner;
                    if (tbox.Name == "UsernameField")
                    {
                        usernameBox = tboxPeer;
                    }
                }
                else if (peer is PasswordBoxAutomationPeer)
                {
                    var pboxPeer = (PasswordBoxAutomationPeer)peer;
                    Assert.IsType<PasswordBox>(pboxPeer.Owner);
                    var pbox = (PasswordBox)pboxPeer.Owner;
                    if (pbox.Name == "PasswordField")
                    {
                        passwordBox = pboxPeer;
                    }
                }
                else if (peer is ButtonAutomationPeer)
                {
                    var buttonPeer = (ButtonAutomationPeer)peer;
                    Assert.IsType<Button>(buttonPeer.Owner);
                    var button = (Button)buttonPeer.Owner;
                    if (button.Name == "LoginButton")
                    {
                        loginButton = buttonPeer;
                    }
                }
            }

            var username = (TextBox)usernameBox.Owner;
            username.Text = "incorrect";
            var password = (PasswordBox)passwordBox.Owner;
            password.Password = "incorrect";

            var u2 = (TextBox)usernameBox.Owner;
            Assert.Equal("incorrect", u2.Text);
            var p2 = (PasswordBox)passwordBox.Owner;
            Assert.Equal("incorrect", p2.Password);

            Thread.Sleep(100);
            loginButton.Owner.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            mockSession.Verify(p => p.Authenticate(It.IsAny<UsernameCredentials>()), Times.Exactly(1));
            mockSession.Verify(p => p.ApplyAllPreferences(), Times.Exactly(0));
            Assert.True(errorLabel.Owner.IsVisible);
            Thread.Sleep(2000);

            username = (TextBox)usernameBox.Owner;
            username.Text = "correct";
            password = (PasswordBox)passwordBox.Owner;
            password.Password = "incorrect";

            u2 = (TextBox)usernameBox.Owner;
            Assert.Equal("correct", u2.Text);
            p2 = (PasswordBox)passwordBox.Owner;
            Assert.Equal("incorrect", p2.Password);

            Thread.Sleep(100);
            loginButton.Owner.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            mockSession.Verify(p => p.Authenticate(It.IsAny<UsernameCredentials>()), Times.Exactly(2));
            mockSession.Verify(p => p.ApplyAllPreferences(), Times.Exactly(0));
            Assert.True(errorLabel.Owner.IsVisible);
            Thread.Sleep(2000);

            username = (TextBox)usernameBox.Owner;
            username.Text = "incorrect";
            password = (PasswordBox)passwordBox.Owner;
            password.Password = "correct";

            u2 = (TextBox)usernameBox.Owner;
            Assert.Equal("incorrect", u2.Text);
            p2 = (PasswordBox)passwordBox.Owner;
            Assert.Equal("correct", p2.Password);

            Thread.Sleep(100);
            loginButton.Owner.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            mockSession.Verify(p => p.Authenticate(It.IsAny<UsernameCredentials>()), Times.Exactly(3));
            mockSession.Verify(p => p.ApplyAllPreferences(), Times.Exactly(0));
            Assert.True(errorLabel.Owner.IsVisible);
            Thread.Sleep(2000);

            username = (TextBox)usernameBox.Owner;
            username.Text = "correct";
            password = (PasswordBox)passwordBox.Owner;
            password.Password = "correct";

            u2 = (TextBox)usernameBox.Owner;
            Assert.Equal("correct", u2.Text);
            p2 = (PasswordBox)passwordBox.Owner;
            Assert.Equal("correct", p2.Password);

            Thread.Sleep(100);
            loginButton.Owner.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            mockSession.Verify(p => p.Authenticate(It.IsAny<UsernameCredentials>()), Times.Exactly(4));
            mockSession.Verify(p => p.ApplyAllPreferences(), Times.Exactly(1));
            Assert.False(errorLabel.Owner.IsVisible);
            Thread.Sleep(2000);
        }

        public void Dispose()
        {
            window.Close();
        }
    }
}
