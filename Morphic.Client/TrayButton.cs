// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

namespace Morphic.Client
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Windows.Controls;
    using System.Windows.Forms;
    using QuickStrip;

    /// <summary>
    /// Displays a button next to the notification area on the taskbar, which is always visible.
    /// This depends on tray-button.exe, from https://github.com/stegru/Morphic.TrayButton
    /// </summary>
    public class TrayButton : IDisposable
    {
        private readonly WindowMessageHook messageHook;

        /// <summary>Raised when the button is clicked.</summary>
        public event EventHandler<EventArgs>? Click;
        /// <summary>Raised when the button is double-clicked.</summary>
        public event EventHandler<EventArgs>? DoubleClick;
        /// <summary>Raised when the button is right-clicked.</summary>
        public event EventHandler<EventArgs>? SecondaryClick;

        /// <summary>Used if there was a problem starting the tray-button process.</summary>
        private NotifyIcon? notifyIcon;

        public bool UseNotificationIcon { get; set; }

        // The message sent from the tray button process;
        public const string ButtonMessageName = "GPII-TrayButton-Message";
        private readonly int buttonMessage;

        private enum TrayCommand
        {
            Icon = 1,
            IconHc = 2,
            Tooltip = 3,
            Destroy = 4,
            State = 5
        }

        private enum TrayNotification
        {
            Update = 0,
            Click = 1,
            ShowMenu = 2,
            MouseEnter = 3,
            MouseLeave = 4,
            Activate = 5,
        }

        /// <summary>
        /// Used by a second instance of this application to sends a message to the first instance telling
        /// it to activate the window.
        /// </summary>
        public static void SendActivate()
        {
            const int HWND_BROADCAST = 0xffff;
            int message = WindowMessageHook.RegisterMessage(ButtonMessageName);
            SendMessage((IntPtr)HWND_BROADCAST, message, (int)TrayNotification.Activate, IntPtr.Zero);
        }

        /// <summary>The icon on the button.</summary>
        public Icon? Icon
        {
            get => this.icon;
            set
            {
                this.icon = value;
                this.UpdateIcon();
            }
        }

        /// <summary>Tooltip for the button.</summary>
        public string Text
        {
            get => this.text ?? "";
            set
            {
                this.text = value;
                this.UpdateText();
            }
        }

        /// <summary>Show the button.</summary>
        public bool Visible
        {
            get => this.visible;
            set
            {
                this.visible = value; 

                if (this.visible)
                {
                    this.ShowIcon();
                }
                else
                {
                    this.HideIcon();
                }
            }
        }

        private Process? buttonProcess;
        private string? text;
        private string? iconFile;
        private Icon? icon;
        private bool visible;

        private IntPtr buttonWindow;

        public TrayButton(IMessageHook messageWindow) : this(messageWindow.Messages)
        {
        }

        public TrayButton(WindowMessageHook messageHook)
        {
            this.messageHook = messageHook;
            this.buttonMessage = this.messageHook.AddMessage(ButtonMessageName);
            this.messageHook.GotMessage += this.GotMessage;
        }

        /// <summary>
        /// Shows the button - starts the button process.
        /// </summary>
        private void ShowIcon()
        {
            bool success = false;

            //if (!this.UseNotificationIcon)
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo()
                    {
                        UseShellExecute = false,
                        // https://github.com/stegru/Morphic.TrayButton/releases/latest
                        FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                            "tray-button.exe")
                    };
                    startInfo.EnvironmentVariables.Add("GPII_HWND", this.messageHook.Handle.ToString());

                    Process? process = Process.Start(startInfo);

                    if (process != null)
                    {
                        success = true;
                        this.buttonProcess = process;
                        this.buttonProcess.EnableRaisingEvents = true;
                        this.buttonProcess.Exited += (sender, args) =>
                        {
                            if (this.Visible)
                            {
                                this.ShowIcon();
                            }
                        };
                    }
                }
                catch (Win32Exception e)
                {
                    this.UseNotificationIcon = true;
                }
            }

            if (this.UseNotificationIcon && this.notifyIcon == null)
            {
                this.notifyIcon = new NotifyIcon();
                this.notifyIcon.MouseUp += (sender, args) =>
                {
                    if (args.Button == MouseButtons.Right)
                    {
                        this.SecondaryClick?.Invoke(this, args);
                    }
                    else if (args.Button == MouseButtons.Left)
                    {
                        this.OnClick();
                    }
                };

                this.notifyIcon.DoubleClick += (sender, args) => this.OnClick(true);

            }

            this.Update();
        }

        /// <summary>
        /// A Window message was received.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GotMessage(object? sender, MessageEventArgs e)
        {
            if (e.Msg == this.buttonMessage)
            {
                switch ((TrayNotification)e.WParam)
                {
                    case TrayNotification.Update:
                        // Button wants to know the settings.
                        this.buttonWindow = e.LParam;
                        this.Update();
                        break;
                    case TrayNotification.Click:
                        // Left button click.
                        this.OnClick();
                        break;
                    case TrayNotification.ShowMenu:
                        // Right button click.
                        this.SecondaryClick?.Invoke(this, new EventArgs());
                        break;
                    case TrayNotification.MouseEnter:
                        break;
                    case TrayNotification.MouseLeave:
                        break;
                    case TrayNotification.Activate:
                        this.OnClick(true);
                        break;
                }
            }
        }

        private int lastClick;
        private void OnClick(bool doubleClick = false)
        {
            int span = Environment.TickCount - this.lastClick;
            if (doubleClick || span <= SystemInformation.DoubleClickTime)
            {
                this.DoubleClick?.Invoke(this, new EventArgs());
            }
            else
            {
                this.Click?.Invoke(this, new EventArgs());
            }

            this.lastClick = Environment.TickCount;
        }

        /// <summary>Hides the button, by terminating the process.</summary>
        private void HideIcon()
        {
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Visible = false;
            }
            
            if (this.buttonProcess != null)
            {
                this.SendCommand(TrayCommand.Destroy);
            }
        }


        /// <summary>
        /// Sends a command to the button process.
        /// </summary>
        /// <param name="command">The command name.</param>
        /// <param name="commandData">Command commandData.</param>
        private void SendCommand(TrayCommand command, string? commandData = null)
        {
            const int WM_COPYDATA = 0x4a;
            COPYDATASTRUCT data = new COPYDATASTRUCT((int)command, commandData ?? string.Empty);
            TrayButton.SendMessageCopyData(this.buttonWindow, WM_COPYDATA, (int)this.messageHook.Handle, ref data);
        }
        
        /// <summary>Sends the configuration to the button.</summary>
        private void Update()
        {
            this.UpdateText();
            this.UpdateIcon();
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Visible = this.Visible;
            }
        }

        /// <summary>
        /// Updates the tooltip text.
        /// </summary>
        private void UpdateText()
        {
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Text = this.text;
            }

            if (this.buttonProcess != null)
            {
                this.SendCommand(TrayCommand.Tooltip, this.text);
            }
        }

        /// <summary>
        /// Updates the icon on the button.
        /// </summary>
        private void UpdateIcon()
        {
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Icon = this.Icon;
            }

            if (this.buttonProcess != null) {
                // Store the icon to a file, and tell the tray-button to load it.
                this.iconFile ??= System.IO.Path.GetTempFileName();
                using (FileStream fs = new FileStream(this.iconFile, FileMode.Truncate))
                {
                    this.Icon?.Save(fs);
                }

                this.SendCommand(TrayCommand.Icon, this.iconFile);
            }
        }

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessageCopyData(IntPtr hWnd, int Msg, int wParam, ref COPYDATASTRUCT lParam);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        // ReSharper disable InconsistentNaming
        // ReSharper disable IdentifierTypo
        private struct COPYDATASTRUCT
        {
            public readonly IntPtr dwData;
            public readonly int cbData;
            [MarshalAs(UnmanagedType.LPWStr)]
            public readonly string lpData;

            public COPYDATASTRUCT(int dwData, string lpData)
                : this(new IntPtr(dwData), lpData)
            {

            }

            public COPYDATASTRUCT(IntPtr dwData, string lpData)
            {
                this.dwData = dwData;
                this.lpData = lpData + "\0";
                this.cbData = (lpData.Length + 1) * 2;
            }
        }
        // ReSharper restore IdentifierTypo
        // ReSharper restore InconsistentNaming


        public void Dispose()
        {
            this.HideIcon();
            this.buttonProcess?.Dispose();
            this.notifyIcon?.Dispose();
        }
    }
}