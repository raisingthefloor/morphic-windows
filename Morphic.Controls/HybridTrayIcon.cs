﻿// Copyright 2020-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/main/LICENSE.txt
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

using System;

namespace Morphic.Controls;

/// <summary>
/// Displays a system tray icon (NotifyIcon) in the notification area and/or an always-visible
/// button (Morphic.Controls.TrayButton.TrayButton) next to the notification area on the task bar.
/// </summary>
public class HybridTrayIcon : IDisposable
{
     private System.Drawing.Icon? _icon = null;
     private string? _text = null;
     private bool _visible = false;

     // <summary>Used if a tray icon is desired instead of a next-to-tray taskbar button</summary>
     private System.Windows.Forms.NotifyIcon? _notifyIcon = null;

     // <summary>Used if a next-to-tray button is desired instead of a tray icon</summary>
     private Morphic.Controls.TrayButton.TrayButton? _trayButton = null;

     public enum TrayIconLocationOption
     {
          None,
          NotificationTray,
          NextToNotificationTray,
          NotificationTrayAndNextToNotificationTray
     }
     //
     private TrayIconLocationOption _trayIconLocation = TrayIconLocationOption.None;

     /// <summary>Raised when the button is clicked.</summary>
     public event EventHandler<EventArgs>? Click;
     /// <summary>Raised when the button is right-clicked.</summary>
     public event EventHandler<EventArgs>? SecondaryClick;

     public HybridTrayIcon()
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
     public System.Drawing.Icon? Icon
     {
          get
          {
               return _icon;
          }
          set
          {
               _icon = value;
               if (_notifyIcon is not null)
               {
                    _notifyIcon.Icon = _icon;
               }
               if (_trayButton is not null)
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
               if (_notifyIcon is not null)
               {
                    _notifyIcon.Text = _text;
               }
               if (_trayButton is not null)
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

               if (_notifyIcon is not null)
               {
                    _notifyIcon.Visible = _visible;
               }
               if (_trayButton is not null)
               {
                    _trayButton.Visible = _visible;
               }
          }
     }

     //

     public void SuppressTaskbarButtonResurfaceChecks(bool suppress)
     {
          _trayButton?.SuppressTaskbarButtonResurfaceChecks(suppress);
     }

     //

     private void InitializeTrayIcon()
     {
          if (_notifyIcon is not null)
          {
               return;
          }

          _notifyIcon = new System.Windows.Forms.NotifyIcon();
          _notifyIcon.Text = _text;
          _notifyIcon.Icon = _icon;
          //
          _notifyIcon.MouseUp += (sender, args) =>
          {
               if (args.Button == System.Windows.Forms.MouseButtons.Right)
               {
                    this.SecondaryClick?.Invoke(this, args);
               }
               else if (args.Button == System.Windows.Forms.MouseButtons.Left)
               {
                    this.Click?.Invoke(this, args);
               }
          };
          _notifyIcon.Visible = _visible;
     }

     private void InitializeTrayButton()
     {
          if (_trayButton is not null)
          {
               return;
          }

          _trayButton = new Morphic.Controls.TrayButton.TrayButton();
          _trayButton.Text = _text;
          _trayButton.Icon = _icon;
          //
          _trayButton.MouseUp += (sender, args) =>
          {
               if (args.Button == System.Windows.Forms.MouseButtons.Right)
               {
                    this.SecondaryClick?.Invoke(this, args);
               }
               else if (args.Button == System.Windows.Forms.MouseButtons.Left)
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
                         if (_notifyIcon is null)
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
                         if (_trayButton is null)
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
                         if (_notifyIcon is not null)
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
                         if (_trayButton is not null)
                         {
                              _trayButton.Dispose();
                              _trayButton = null;
                         }
                         break;
               }
          }
     }
}