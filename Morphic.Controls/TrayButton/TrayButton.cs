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

using Morphic.Core;
using System;
using System.Diagnostics;
using Windows.Win32.UI.WindowsAndMessaging;

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

    public MorphicResult<MorphicUnit, MorphicUnit> SetIconFromFile(string filePath, int width, int height)
    {
        // load the .ico file as an HICON using LoadImage
        var hIcon = Windows.Win32.PInvoke.LoadImage(
            null,
            filePath,
            Windows.Win32.UI.WindowsAndMessaging.GDI_IMAGE_TYPE.IMAGE_ICON,
            width,
            height,
            Windows.Win32.UI.WindowsAndMessaging.IMAGE_FLAGS.LR_LOADFROMFILE
        );
        if (hIcon.IsInvalid)
        {
            Debug.Assert(false, "Could not load icon from file: " + filePath);
            return MorphicResult.ErrorResult();
        }
        var hIconRawHandle = hIcon.DangerousGetHandle();

        // extract the color bitmap (HBITMAP) from the HICON
        Windows.Win32.UI.WindowsAndMessaging.ICONINFO iconInfo;
        try
        {
            if (Windows.Win32.PInvoke.GetIconInfo(hIcon, out iconInfo) == false)
            {
                Debug.Assert(false, "Could not get icon info");
                _ = Windows.Win32.PInvoke.DestroyIcon((HICON)hIconRawHandle);
                return MorphicResult.ErrorResult();
            }
        }
        finally
        {
            // NOTE: there's a bug in SafeFileHandle which tries to clean up icons incorrectly, so prevent the safe handle from trying to free it
            hIcon.SetHandleAsInvalid();
        }

        // clean up the mask bitmap; we only need the color bitmap
        if (!iconInfo.hbmMask.IsNull)
        {
            _ = Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)iconInfo.hbmMask);
        }

        // NOTE: iconInfo.hbmColor must be cleaned up manually later (via DeleteObject)
        var hBitmap = iconInfo.hbmColor;
        if (hBitmap.IsNull)
        {
            Debug.Assert(false, "Icon has no color bitmap");
            _ = Windows.Win32.PInvoke.DestroyIcon((HICON)hIconRawHandle);
            return MorphicResult.ErrorResult();
        }

        // get the bitmap dimensions
        int bitmapWidth;
        int bitmapHeight;
        unsafe
        {
            Windows.Win32.Graphics.Gdi.BITMAP bitmapStruct;
            var getObjectResult = Windows.Win32.PInvoke.GetObject(
                (Windows.Win32.Graphics.Gdi.HGDIOBJ)hBitmap.Value,
                sizeof(Windows.Win32.Graphics.Gdi.BITMAP),
                &bitmapStruct
            );
            if (getObjectResult == 0)
            {
                Debug.Assert(false, "Could not get bitmap dimensions");
                _ = Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)hBitmap.Value);
                _ = Windows.Win32.PInvoke.DestroyIcon((HICON)hIconRawHandle);
                return MorphicResult.ErrorResult();
            }
            bitmapWidth = bitmapStruct.bmWidth;
            bitmapHeight = bitmapStruct.bmHeight;
        }

        // pass the HBITMAP/HICON to the underlying tray button
#if INCLUDE_WINDOWS_10_SUPPORT
        if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
        {
#endif
            // Windows 11: pass the HBITMAP (takes ownership); destroy the HICON since it's no longer needed
            _ = _trayButton!.SetGdiBitmap(hBitmap, bitmapWidth, bitmapHeight);
            Windows.Win32.PInvoke.DestroyIcon((Windows.Win32.UI.WindowsAndMessaging.HICON)hIconRawHandle);
#if INCLUDE_WINDOWS_10_SUPPORT
        }
        else
        {
            // Windows 10: pass the HICON directly (takes ownership); clean up the HBITMAP since the legacy path doesn't use it
            _legacyTrayButton!.SetGdiIcon((Windows.Win32.UI.WindowsAndMessaging.HICON)hIconRawHandle);
            _ = Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)hBitmap);
        }
#endif

        return MorphicResult.OkResult();
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
    }

    public bool Visible
    {
        get => this.Visibility == TrayButtonVisibility.Visible;
    }
    //
    public void SetVisible(bool value) 
    {
#if INCLUDE_WINDOWS_10_SUPPORT
        if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
        {
#endif
            _trayButton!.Visibility = value ? TrayButtonVisibility.Visible : TrayButtonVisibility.Hidden;
#if INCLUDE_WINDOWS_10_SUPPORT
        }
        else //if (.IsWindows10() == true)
        {
            _legacyTrayButton!.Visible = value;
        }
#endif
    }

    //

    public void SuppressTaskbarButtonResurfaceChecks(bool suppress)
    {
        _trayButton?.SuppressTaskbarButtonResurfaceChecks(suppress);
    }
}
