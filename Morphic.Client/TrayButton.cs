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
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Forms;
    using System.Windows.Interop;
    using Path = System.IO.Path;

    /// <summary>
    /// Displays a button next to the notification area on the taskbar, which is always visible.
    /// This depends on tray-button.exe, from https://github.com/stegru/Morphic.TrayButton
    /// </summary>
    public class TrayButton : IDisposable
    {
        /// <summary>Raised when the button is clicked.</summary>
        public event EventHandler<EventArgs> Click = null!;
        
        /// <summary>A menu to show when the button is right-clicked.</summary>
        public ContextMenu? ContextMenu { get; set; }

        /// <summary>Used if there was a problem starting the tray-button process.</summary>
        private NotifyIcon? fallbackIcon;
        
        /// <summary>The icon on the button.</summary>
        public Icon Icon
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
        private Icon icon;
        private bool visible;

        /// <summary>
        /// Shows the button - starts the button process.
        /// </summary>
        private void ShowIcon()
        {
            try
            {
                this.buttonProcess = Process.Start(new ProcessStartInfo()
                {
                    // https://github.com/stegru/Morphic.TrayButton/releases/latest
                    FileName = 
                        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                            "tray-button.exe"),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    StandardInputEncoding = Encoding.Unicode
                });

                this.buttonProcess.EnableRaisingEvents = true;
                this.buttonProcess.Exited += (sender, args) =>
                {
                    if (this.Visible)
                    {
                        this.ShowIcon();
                    }
                };
                
                this.buttonProcess.StandardInput.AutoFlush = true;
                this.GetInput();
            }
            catch (Win32Exception e)
            {
                this.buttonProcess = null;
                this.fallbackIcon = new NotifyIcon();
                this.Update();
                this.fallbackIcon.Click += this.Click.Invoke;
            }

        }

        /// <summary>
        /// Reads input from the menu button process.
        /// </summary>
        private async void GetInput()
        {
            string? line;
            while ((line = await this.buttonProcess?.StandardOutput.ReadLineAsync()!) != null)
            {
                string[] parts = line.Split(' ', 3);
                string command = parts[0];
                switch (command)
                {
                    case "UPDATE":
                        // Button wants to know the settings.
                        this.Update();
                        break;
                    
                    case "CLICK":
                        // Left button click.
                        this.Click.Invoke(this, new EventArgs());
                        break;
                    
                    case "SHOWMENU":
                        // Right button click.
                        if (this.ContextMenu != null)
                        {
                            this.ContextMenu.IsOpen = true;
                        }

                        break;
                    
                    case "MOUSEENTER":
                    case "MOUSELEAVE":
                        break;
                    case "POSITION":
                        break;
                }
            };
        }

        /// <summary>Hides the button, by terminating the process.</summary>
        private void HideIcon()
        {
            if (this.fallbackIcon != null)
            {
                this.fallbackIcon.Visible = true;
            }
            
            if (this.buttonProcess != null)
            {
                this.SendCommand("DESTROY");
            }
        }

        /// <summary>
        /// Sends a command to the button process.
        /// </summary>
        /// <param name="command">The command name.</param>
        /// <param name="data">Command data.</param>
        private void SendCommand(string command, string? data = null)
        {
            if (string.IsNullOrEmpty(data))
            {
                this.buttonProcess?.StandardInput.WriteLine(command);
            }
            else
            {
                this.buttonProcess?.StandardInput.WriteLine("{0} {1}", command, data);
            }
        }
        
        /// <summary>Sends the configuration to the button.</summary>
        private void Update()
        {
            this.UpdateWindow();
            this.UpdateText();
            this.UpdateIcon();
            if (this.fallbackIcon != null)
            {
                this.fallbackIcon.Visible = this.Visible;
            }
        }

        /// <summary>
        /// Provides the button process with a window that can be activated when the button is clicked.
        /// This is needed so the popup menu can capture the focus.
        /// </summary>
        private async void UpdateWindow()
        {
            while (App.Current.Windows.Count == 0)
            {
                await Task.Delay(1000);
            }

            Window window = App.Current.Windows[0];
            WindowInteropHelper nativeWindow = new WindowInteropHelper(window);
            this.SendCommand("HWND", nativeWindow.Handle.ToString());
        }
        
        /// <summary>
        /// Updates the tooltip text.
        /// </summary>
        private void UpdateText()
        {
            if (this.fallbackIcon == null)
            {
                this.SendCommand("TOOLTIP", this.text);
            }
            else
            {
                this.fallbackIcon.Text = this.text;
            }
        }

        /// <summary>
        /// Updates the icon on the button.
        /// </summary>
        private void UpdateIcon()
        {
            if (this.fallbackIcon == null)
            {
                // Store the icon to a file, and tell the tray-button to load it.
                this.iconFile ??= System.IO.Path.GetTempFileName();
                using (FileStream fs = new FileStream(this.iconFile, FileMode.Truncate))
                {
                    this.Icon.Save(fs);
                }

                this.SendCommand("ICON", this.iconFile);
            }
            else
            {
                this.fallbackIcon.Icon = this.Icon;
            }
        }

        public void Dispose()
        {
            this.HideIcon();
            this.buttonProcess?.Dispose();
            this.fallbackIcon?.Dispose();
        }
    }
}