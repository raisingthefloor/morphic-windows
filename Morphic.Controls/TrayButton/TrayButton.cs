// Copyright 2020-2025 Raising the Floor - US, Inc.
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

namespace Morphic.Controls.TrayButton;

public class TrayButton : IDisposable
{
    private bool disposedValue;

    // NOTE: only one of the two tray button variants will be populated (i.e. based on the OS version)
    //       [we have chosen not to create a common interface between them, as the plan is to deprecate the Windows 10 variant once Windows 10 is no longer supported...and the Windows 11+ variant should be allowed to get a new API surface if/as needed]
    Morphic.Controls.TrayButton.Windows10.TrayButton? _legacyTrayButton;
    Morphic.Controls.TrayButton.Windows11.TrayButton? _trayButton;

    public event System.Windows.Forms.MouseEventHandler? MouseUp;

    public System.Drawing.Rectangle? PositionAndSize
    {
        get
        {
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
                // Windows 11 and newer (i.e. modern tray button)
                return _trayButton?.PositionAndSize;
            }
            else
            {
                // Windows 10 (i.e. legacy tray button)
                return _legacyTrayButton?.PositionAndSize;
            }
        }
    }

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

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                //
                if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
                {
                    // Windows 11 and newer (i.e. modern tray button)
                    _trayButton?.Dispose();
                } 
                else
                {
                    // Windows 10 (i.e. legacy tray button)
                    _legacyTrayButton?.Dispose();
                }
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // [none]

            // set large fields to null
            // [none]

            disposedValue = true;
        }
    }

    // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~TrayButton()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    //

    public System.Drawing.Bitmap? Bitmap
    {
        get
        {
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
                return _trayButton!.Bitmap;
            }
            else //if (.IsWindows10() == true)
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
            else //if (.IsWindows10() == true)
            {
                if (value is not null)
                {
                    var bitmapAsIconHandlePointer = value.GetHicon();
                    try
                    {
                        _legacyTrayButton!.Icon = (System.Drawing.Icon)(System.Drawing.Icon.FromHandle(bitmapAsIconHandlePointer).Clone());
                    }
                    finally
                    {
                        _ = Windows.Win32.PInvoke.DestroyIcon((Windows.Win32.UI.WindowsAndMessaging.HICON)bitmapAsIconHandlePointer);
                    }
                }
                else
                {
                    _legacyTrayButton!.Icon = null;
                }
            }
        }
    }

    public System.Drawing.Icon? Icon
    {
        get
        {
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
                if (_trayButton!.Bitmap is not null)
                {
                    var bitmapAsIconHandlePointer = _trayButton!.Bitmap!.GetHicon();
                    try
                    {
                        return (System.Drawing.Icon)(System.Drawing.Icon.FromHandle(bitmapAsIconHandlePointer).Clone());
                    }
                    finally
                    {
                        Windows.Win32.PInvoke.DestroyIcon((Windows.Win32.UI.WindowsAndMessaging.HICON)bitmapAsIconHandlePointer);
                    }
                }
                else
                {
                    return null;
                }
            }
            else //if (.IsWindows10() == true)
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
            else //if (.IsWindows10() == true)
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
            else //if (.IsWindows10() == true)
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
            else //if (.IsWindows10() == true)
            {
                _legacyTrayButton!.Text = value;
            }
        }
    }

    public TrayButtonVisibility Visibility
    {
        get
        {
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
                return _trayButton!.Visibility;
            }
            else //if (.IsWindows10() == true)
            {
                return _legacyTrayButton!.Visible switch
                {
                    true => TrayButtonVisibility.Visible,
                    false => TrayButtonVisibility.Hidden,
                };
            }
        }
        set
        {
            var newVisibleState = value switch
            {
                TrayButtonVisibility.Hidden => false,
                TrayButtonVisibility.PendingVisible => throw new ArgumentException("State 'PendingVisible' is invalid for the Visibility Set operation"),
                TrayButtonVisibility.Visible => true,
                _ => throw new Exception("invalid code path"),
            };

            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
                _trayButton!.Visibility = value;
            }
            else //if (.IsWindows10() == true)
            {
                _legacyTrayButton!.Visible = newVisibleState;
            }
        }
    }
}
