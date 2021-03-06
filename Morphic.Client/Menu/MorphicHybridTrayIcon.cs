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

namespace Morphic.Client.Menu
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;
    using Bar.UI;

    /// <summary>
    /// Displays a system tray icon (NotifyIcon) in the notification area and/or an always-visible
    /// button (MorphicTrayButton) next to the notification area on the task bar.
    /// </summary>
    public class MorphicHybridTrayIcon : IDisposable
    {
        private Icon? _icon = null;
        private string? _text = null;
        private bool _visible = false;

        // <summary>Used if a tray icon is desired instead of a next-to-tray taskbar button</summary>
        private NotifyIcon? _notifyIcon = null;

        // <summary>Used if a next-to-tray button is desired instead of a tray icon</summary>
        private MorphicTrayButton? _trayButton = null;

        public enum TrayIconLocationOption
        {
            None,
            NotificationTray,
            NextToNotificationTray,
            NotificationTrayAndNextToNotificationTray
        }

        private TrayIconLocationOption _trayIconLocation = TrayIconLocationOption.None;

        /// <summary>Raised when the button is clicked.</summary>
        public event EventHandler<EventArgs>? Click;
        /// <summary>Raised when the button is right-clicked.</summary>
        public event EventHandler<EventArgs>? SecondaryClick;

        public MorphicHybridTrayIcon()
        {
        }

        public void Dispose()
        {
            _notifyIcon?.Dispose();
            _notifyIcon = null;

            _trayButton?.Dispose();
            _trayButton = null;
        }

        /// <summary>The icon for the tray icon</summary>
        public Icon? Icon
        {
            get
            {
                return _icon;
            }
            set
            {
                _icon = value;
                if (_notifyIcon != null)
                {
                    _notifyIcon.Icon = _icon;
                }
                if (_trayButton != null)
                {
                    _trayButton.Icon = _icon;
                }
            }
        }

        /// <summary>Tooltip for the tray icon.</summary>
        public string? Text
        {
            get
            {
                return _text;
            }
            set
            {
                _text = value;
                if (_notifyIcon != null)
                {
                    _notifyIcon.Text = _text;
                }
                if (_trayButton != null)
                {
                    _trayButton.Text = _text;
                }
            }
        }

        /// <summary>Show or hide the tray icon.</summary>
        public bool Visible
        {
            get
            {
                return _visible;
            }
            set
            {
                _visible = value;

                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = _visible;
                }
                if (_trayButton != null)
                {
                    _trayButton.Visible = _visible;
                }
            }
        }

        //

        private void InitializeTrayIcon()
        {
            if (_notifyIcon != null) {
                return;
            }

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = _text;
            _notifyIcon.Icon = _icon;
            //
            _notifyIcon.MouseUp += (sender, args) =>
            {
                if (args.Button == MouseButtons.Right)
                {
                    this.SecondaryClick?.Invoke(this, args);
                }
                else if (args.Button == MouseButtons.Left)
                {
                    this.Click?.Invoke(this, args);
                }
            };
            _notifyIcon.Visible = _visible;
        }

        private void InitializeTrayButton()
        {
            if (_trayButton != null)
            {
                return;
            }

            _trayButton = new MorphicTrayButton();
            _trayButton.Text = _text;
            _trayButton.Icon = _icon;
            //
            _trayButton.MouseUp += (sender, args) =>
            {
                if (args.Button == MouseButtons.Right)
                {
                    this.SecondaryClick?.Invoke(this, args);
                }
                else if (args.Button == MouseButtons.Left)
                {
                    this.Click?.Invoke(this, args);
                }
            };
            _trayButton.Visible = _visible;
        }

        //

        public TrayIconLocationOption TrayIconLocation
        {
            get
            {
                return _trayIconLocation;
            }
            set
            {
                _trayIconLocation = value;

                // create notify icon if requested
                switch (value)
                {
                    case TrayIconLocationOption.NotificationTray:
                    case TrayIconLocationOption.NotificationTrayAndNextToNotificationTray:
                        if (_notifyIcon == null)
                        {
                            this.InitializeTrayIcon();
                        }
                        break;
                }

                // create tray button if requested
                switch (value)
                {
                    case TrayIconLocationOption.NextToNotificationTray:
                    case TrayIconLocationOption.NotificationTrayAndNextToNotificationTray:
                        if (_trayButton == null)
                        {
                            this.InitializeTrayButton();
                        }
                        break;
                }

                // destroy notify icon if no longer wanted
                switch (value)
                {
                    case TrayIconLocationOption.None:
                    case TrayIconLocationOption.NextToNotificationTray:
                        if (_notifyIcon != null)
                        {
                            _notifyIcon.Dispose();
                            _notifyIcon = null;
                        }
                        break;
                }

                // destroy tray button if no longer wanted
                switch (value)
                {
                    case TrayIconLocationOption.None:
                    case TrayIconLocationOption.NotificationTray:
                        if (_trayButton != null)
                        {
                            _trayButton.Dispose();
                            _trayButton = null;
                        }
                        break;
                }
            }
        }
    }
}