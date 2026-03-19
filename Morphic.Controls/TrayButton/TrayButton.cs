// Copyright 2020-2026 Raising the Floor - US, Inc.
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
#if INCLUDE_WINDOWS_10_SUPPORT
    Morphic.Controls.TrayButton.Windows10.TrayButton? _legacyTrayButton;
#endif
    Morphic.Controls.TrayButton.Windows11.TrayButton? _trayButton;
	
    private System.Drawing.Bitmap? _bitmap = null;

    // NOTE: MouseUp is not a thread-safe event
    public event EventHandler<Morphic.Controls.MouseEventArgs>? MouseUp;

    public System.Drawing.Rectangle? PositionAndSize
    {
        get
        {
#if INCLUDE_WINDOWS_10_SUPPORT
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
#endif
                // Windows 11 and newer (i.e. modern tray button)
                return _trayButton?.PositionAndSize;
#if INCLUDE_WINDOWS_10_SUPPORT
            }
            else
            {
                // Windows 10 (i.e. legacy tray button)
                return _legacyTrayButton?.PositionAndSize;
            }
#endif
        }
    }

    public TrayButton()
    {
#if INCLUDE_WINDOWS_10_SUPPORT
        if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
        {
#endif
            // Windows 11 and newer (i.e. modern tray button)
            _trayButton = new();
            _trayButton.MouseUp += (s, e) =>
            {
                this.MouseUp?.Invoke(s, e);
            };
#if INCLUDE_WINDOWS_10_SUPPORT
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
#endif
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                //
#if INCLUDE_WINDOWS_10_SUPPORT
                if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
                {
#endif
                    // Windows 11 and newer (i.e. modern tray button)
                    _trayButton?.Dispose();
#if INCLUDE_WINDOWS_10_SUPPORT
                } 
                else
                {
                    // Windows 10 (i.e. legacy tray button)
                    _legacyTrayButton?.Dispose();
                }
#endif
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // [none]

            // set large fields to null
            // [none]

            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~TrayButton()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

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
#if INCLUDE_WINDOWS_10_SUPPORT
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
#endif
                return _bitmap;
#if INCLUDE_WINDOWS_10_SUPPORT
            }
            else //if (.IsWindows10() == true)
            {
                var icon = _legacyTrayButton!.Icon;
                return (icon is not null) ? icon!.ToBitmap() : null;
            }
#endif
        }
        set
        {
#if INCLUDE_WINDOWS_10_SUPPORT
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
#endif
                _bitmap = value;
				
                // convert the managed System.Drawing.Bitmap to a GDI bitmap (handle); the Windows11.TrayButton class ultimately takes ownership of the handle (and will clean up during disposal)
                IntPtr hBitmap = IntPtr.Zero;
                int bitmapWidth = 0;
                int bitmapHeight = 0;
                if (value is not null)
                {
                    try
                    {
		                // convert the managed System.Drawing.Bitmap to a GDI bitmap (and use a transparent background color
		                // NOTE: this creates a new GDI bitmap from the source; the caller can free the original Bitmap after calling this function
                        hBitmap = value.GetHbitmap(System.Drawing.Color.FromArgb(0));
                    }
                    catch
                    {
						System.Diagnostics.Debug.Assert(false, "Could not create GDI bitmap from provided bitmap");
                        return;
                    }
                    bitmapWidth = value.Width;
                    bitmapHeight = value.Height;
                }
				
				// pass the new GDI bitmap to the trayButton; the trayButton is now its owner and will clean it up during its disposal
                _ = _trayButton!.SetBitmap(hBitmap, bitmapWidth, bitmapHeight);
#if INCLUDE_WINDOWS_10_SUPPORT
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
#endif
        }
    }

    public System.Drawing.Icon? Icon
    {
        get
        {
#if INCLUDE_WINDOWS_10_SUPPORT
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
#endif
                if (_bitmap is not null)
                {
                    var bitmapAsIconHandlePointer = _bitmap!.GetHicon();
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
#if INCLUDE_WINDOWS_10_SUPPORT
            }
            else //if (.IsWindows10() == true)
            {
                return _legacyTrayButton!.Icon;
            }
#endif
        }
        set
        {
#if INCLUDE_WINDOWS_10_SUPPORT
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
#endif
                // convert Icon to Bitmap, then set via the Bitmap property
                this.Bitmap = (value is not null) ? value!.ToBitmap() : null;
#if INCLUDE_WINDOWS_10_SUPPORT
            }
            else //if (.IsWindows10() == true)
            {
                _legacyTrayButton!.Icon = value;
            }
#endif
        }
    }

    public string? Text
    {
        get
        {
#if INCLUDE_WINDOWS_10_SUPPORT
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
#endif
                return _trayButton!.Text;
#if INCLUDE_WINDOWS_10_SUPPORT
            }
            else //if (.IsWindows10() == true)
            {
                return _legacyTrayButton!.Text;
            }
#endif
        }
        set
        {
#if INCLUDE_WINDOWS_10_SUPPORT
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
#endif
                _ = _trayButton!.SetText(value);
#if INCLUDE_WINDOWS_10_SUPPORT
            }
            else //if (.IsWindows10() == true)
            {
                _legacyTrayButton!.Text = value;
            }
#endif
        }
    }

    public TrayButtonVisibility Visibility
    {
        get
        {
#if INCLUDE_WINDOWS_10_SUPPORT
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
#endif
                return _trayButton!.Visibility;
#if INCLUDE_WINDOWS_10_SUPPORT
            }
            else //if (.IsWindows10() == true)
            {
                return _legacyTrayButton!.Visible switch
                {
                    true => TrayButtonVisibility.Visible,
                    false => TrayButtonVisibility.Hidden,
                };
            }
#endif
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

#if INCLUDE_WINDOWS_10_SUPPORT
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
#endif
                _trayButton!.Visibility = value;
#if INCLUDE_WINDOWS_10_SUPPORT
            }
            else //if (.IsWindows10() == true)
            {
                _legacyTrayButton!.Visible = newVisibleState;
            }
#endif
        }
    }

    //

    public void SuppressTaskbarButtonResurfaceChecks(bool suppress)
    {
        _trayButton?.SuppressTaskbarButtonResurfaceChecks(suppress);
    }
}
