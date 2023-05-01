// Copyright 2020-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Morphic.Controls.TrayButton;

public class TrayButton : IDisposable
{
     // NOTE: only one of the two tray button variants will be populated (i.e. based on the OS version)
     //       [we have chosen not to create a common interface between them, as the plan is to deprecate the Windows 10 variant once Windows 10 is no longer supported...and the Windows 11+ variant should be allowed to get a new API surface if/as needed]
     Morphic.Controls.TrayButton.Windows10.TrayButton? _legacyTrayButton;
     Morphic.Controls.TrayButton.Windows11.TrayButton? _trayButton;

     public event MouseEventHandler? MouseUp;

     public TrayButton()
     {
          if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
          {
               // Windows 11 and newer (i.e. modern tray button)
               _trayButton = new();
               _trayButton.MouseUp += (s, e) =>
               {
                    this.MouseUp?.Invoke(s, e);
               };
          } 
          else
          {
               // Windows 10 (i.e. legacy tray button)
               _legacyTrayButton = new();
               _legacyTrayButton.MouseUp += (s, e) =>
               {
                    this.MouseUp?.Invoke(s, e);
               };
          }
     }

     public void Dispose()
     {
          if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
          {
               _trayButton?.Dispose();
          } 
          else
          {
               _legacyTrayButton?.Dispose();
          }
     }

     public Bitmap? Bitmap
     {
          get
          {
               if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
               {
                    return _trayButton!.Bitmap;
               }
               else
               {
                    var icon = _legacyTrayButton!.Icon;
                    return (icon is not null) ? icon!.ToBitmap() : null;
               }
          }
          set
          {
               if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
               {
                    _trayButton!.Bitmap = value;
               }
               else
               {
                    if (value is not null)
                    {
                         var bitmapAsIconHandle = value.GetHicon();
                         try
                         {
                              _legacyTrayButton!.Icon = (Icon)Icon.FromHandle(bitmapAsIconHandle).Clone();
                         }
                         finally
                         {
                              PInvoke.User32.DestroyIcon(bitmapAsIconHandle);
                         }
                    }
                    else
                    {
                         _legacyTrayButton!.Icon = null;
                    }
               }
          }
     }

     public Icon? Icon
     {
          get
          {
               if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
               {
                    if (_trayButton!.Bitmap is not null)
                    {
                         var bitmapAsIconHandle = _trayButton!.Bitmap!.GetHicon();
                         try
                         {
                              return (Icon)Icon.FromHandle(bitmapAsIconHandle).Clone();
                         }
                         finally
                         {
                              PInvoke.User32.DestroyIcon(bitmapAsIconHandle);
                         }
                    }
                    else
                    {
                         return null;
                    }
               }
               else
               {
                    return _legacyTrayButton!.Icon;
               }
          }
          set
          {
               if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
               {
                    _trayButton!.Bitmap = (value is not null) ? value!.ToBitmap() : null;
               }
               else
               {
                    _legacyTrayButton!.Icon = value;
               }
          }
     }

     public string? Text
     {
          get
          {
               if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
               {
                    return _trayButton!.Text;
               }
               else
               {
                    return _legacyTrayButton!.Text;
               }
          }
          set
          {
               if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
               {
                    _trayButton!.Text = value;
               }
               else
               {
                    _legacyTrayButton!.Text = value;
               }
          }
     }

     public bool Visible
     {
          get
          {
               if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
               {
                    return _trayButton!.Visible;
               }
               else
               {
                    return _legacyTrayButton!.Visible;
               }
          }
          set
          {
               if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
               {
                    _trayButton!.Visible = value;
               }
               else
               {
                    _legacyTrayButton!.Visible = value;
               }
          }
     }
}
