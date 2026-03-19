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

namespace Morphic.Controls.TrayButton.Windows11;

internal class TrayButton : IDisposable
{
    private bool disposedValue;

    // NOTE: this class owns the HBITMAP handle and frees it on disposal or when replaced
    private record BitmapInfo
    {
        public IntPtr hBitmap { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
    private BitmapInfo? _bitmapInfo = null;
    //
    private string? _text = null;
    private TrayButtonVisibility _visibility = TrayButtonVisibility.Hidden;

    // NOTE: MouseUp is not a thread-safe event
    public event EventHandler<Morphic.Controls.MouseEventArgs>? MouseUp;

    private TrayButtonNativeWindow? _nativeWindow = null;

    private System.Windows.Forms.Timer? _reattemptShowTaskbarButtonTimer = null;
    private static readonly TimeSpan REATTEMPT_SHOW_TASKBAR_BUTTON_INTERVAL_TIMESPAN = new TimeSpan(0, 0, 10);

    public System.Drawing.Rectangle? PositionAndSize
    {
        get
        {
            return _nativeWindow?.PositionAndSize;
        }
    }

    internal TrayButton()
    {
    }

    //

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                _nativeWindow?.Dispose();

                _reattemptShowTaskbarButtonTimer?.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            if (_bitmapInfo is not null)
            {
			    // free the GDI bitmap
                if (_bitmapInfo.hBitmap != IntPtr.Zero)
                {
                    _ = Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)_bitmapInfo.hBitmap);
                }
                _bitmapInfo = null;
            }

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

    /// <summary>
    /// Sets the bitmap from a GDI HBITMAP handle. This class takes ownership of the handle;
    /// the caller must not free it after this call. Pass IntPtr.Zero to clear.
    /// </summary>
    public MorphicResult<MorphicUnit, MorphicUnit> SetBitmap(IntPtr hBitmap, int width, int height)
    {
        // capture the old handle so we can free it AFTER the native window has been updated (since ArgbImageNativeWindow
        // holds a reference to the 'current' source bitmap and may use it during the SetBitmap call)
        var oldHBitmap = (_bitmapInfo is not null) ? _bitmapInfo.hBitmap : IntPtr.Zero;
        try
		{
	        // store the new bitmap info
	        _bitmapInfo = (hBitmap != IntPtr.Zero)
	            ? new() { hBitmap = hBitmap, Width = width, Height = height }
	            : null;

	        // pass a reference to the bitmap to the native window (it does not take ownership)
	        if (_nativeWindow is not null)
	        {
	            var setBitmapResult = _nativeWindow!.SetBitmap(hBitmap, width, height);
	            if (setBitmapResult.IsError == true)
	            {
	                Debug.Assert(false, "Could not set bitmap.");
	                return MorphicResult.ErrorResult();
	            }
	        }
        }
		finally
		{
	        // free the old bitmap now that the native window has been updated with the new one
	        if (oldHBitmap != IntPtr.Zero)
	        {
	            _ = Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)oldHBitmap);
	        }
        }

        return MorphicResult.OkResult();
    }

    public string? Text
    {
        get => _text;
    }
    //
    public MorphicResult<MorphicUnit, MorphicUnit> SetText(string? value)
    {
        _text = value;

        if (_nativeWindow is not null)
        {
            var setTextResult = _nativeWindow!.SetText(_text);
            if (setTextResult.IsError == true)
            {
                Debug.Assert(false, "Could not set text.");
                return MorphicResult.ErrorResult();
            }
        }
        return MorphicResult.OkResult();
    }

    public TrayButtonVisibility Visibility
    {
        get
        {
            return _visibility;
        }
        set
        {
            switch (value)
            {
                case TrayButtonVisibility.Visible:
                    if (_visibility == TrayButtonVisibility.Hidden)
                    {
                        var showResult = this.Show();
                        if (showResult.IsError == true)
                        {
                            // NOTE: we could try to handle various IShowError error codes here
                            //
                            // NOTE: as a fallback, when "show" fails we set a timer to try to show the button
                            Debug.Assert(false, "Could not show Morphic icon (taskbar button) on taskbar; setting Visibility to .PendingVisible");
                            _visibility = TrayButtonVisibility.PendingVisible;

                            // start a timer on the new instance, to resurface the Morphic tray button icon from time to time (just in case it gets hidden under the taskbar)
                            // NOTE: we use a Windows Forms timers here (instead of a system timer) so that the function gets called on the UI thread (or at least the same thread which called this function)
                            _reattemptShowTaskbarButtonTimer = new()
                            {
                                Interval = (int)REATTEMPT_SHOW_TASKBAR_BUTTON_INTERVAL_TIMESPAN.TotalMilliseconds,
                            };
                            _reattemptShowTaskbarButtonTimer.Tick += this.ReattemptShowTaskButtonTimer_Tick;
                            _reattemptShowTaskbarButtonTimer.Start();
                        }
                    }
                    break;
                case TrayButtonVisibility.PendingVisible:
                    throw new ArgumentException("State 'PendingVisible' is invalid for the Visibility Set operation");
                case TrayButtonVisibility.Hidden:
                    if (_visibility != TrayButtonVisibility.Hidden)
                    {
                        this.Hide();
                    }
                    break;
            }
        }
    }

    //

    // NOTE: the Show() method is only concerned with immediately showing the window (by creating the native window); the Visibility property is separate, a state which indicates if the control SHOULD be shown (and whether or not it's currently shown or _trying_ to be shown (i.e. pending))
    public interface IShowError
    {
        public record CouldNotCreateWindow(ICreateNewError InnerError) : IShowError;
        public record CouldNotSetBitmap(TrayButtonNativeWindow.ISetBitmapError InnerError) : IShowError;
        public record CouldNotSetText(TrayButtonNativeWindow.IUpdateTooltipTextAndTrackingError InnerError) : IShowError;
        public record OtherError : IShowError;
    }
    //
    public MorphicResult<MorphicUnit, IShowError> Show()
    {
        if (_nativeWindow is null)
        {
            var createNativeWindowResult = this.CreateNativeWindow();
            if (createNativeWindowResult.IsError == true)
            {
                switch (createNativeWindowResult.Error!)
                {
                    case ICreateNativeWindowError.AlreadyExists:
                        Debug.Assert(false, "Race condition: native window already exists");
                        return MorphicResult.ErrorResult<IShowError>(new IShowError.OtherError());
                    case ICreateNativeWindowError.CreateFailed(ICreateNewError innerError):
                        return MorphicResult.ErrorResult<IShowError>(new IShowError.CouldNotCreateWindow(innerError));
                    case ICreateNativeWindowError.CouldNotSetBitmap(var innerError):
                        return MorphicResult.ErrorResult<IShowError>(new IShowError.CouldNotSetBitmap(innerError));
                    case ICreateNativeWindowError.CouldNotSetText(var innerError):
                        return MorphicResult.ErrorResult<IShowError>(new IShowError.CouldNotSetText(innerError));
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var nativeWindow = createNativeWindowResult.Value!;

            // store the reference to our new native window
            _nativeWindow = nativeWindow;

            // if we created the window, it is now "visible" from the perspective of the TrayButton
            // NOTE: the native window itself will show/hide depending on the topmost state of the taskbar; our control's "visibility" is strictly concerned with whether or not the control is set to be visible right now (and if it is, if it's actually visible or just trying to becoming (pending) visible)
            _visibility = TrayButtonVisibility.Visible;
        }

        return MorphicResult.OkResult();
    }

    public void Hide()
    {
        // NOTE: if we are currently "pending visible" (i.e. our timer is live), then cancel that now
        // NOTE: there is a possibility that the timer is currently executing when we dispose of it
        if (_reattemptShowTaskbarButtonTimer is not null)
        {
            _reattemptShowTaskbarButtonTimer?.Dispose();
            _reattemptShowTaskbarButtonTimer = null;
        }

        if (_nativeWindow is not null)
        {
            _nativeWindow?.Dispose();
            _nativeWindow = null;
        }

        _visibility = TrayButtonVisibility.Hidden;
    }

    //

    public void SuppressTaskbarButtonResurfaceChecks(bool suppress)
    {
        _nativeWindow?.SuppressTaskbarButtonResurfaceChecks(suppress);
    }

    //

    private interface ICreateNativeWindowError
    {
        public record AlreadyExists : ICreateNativeWindowError;
        public record CouldNotSetBitmap(TrayButtonNativeWindow.ISetBitmapError InnerError) : ICreateNativeWindowError;
        public record CouldNotSetText(TrayButtonNativeWindow.IUpdateTooltipTextAndTrackingError InnerError) : ICreateNativeWindowError;
        public record CreateFailed(ICreateNewError InnerError) : ICreateNativeWindowError;
    }
    //
    private MorphicResult<TrayButtonNativeWindow, ICreateNativeWindowError> CreateNativeWindow()
    {
        // if our native window already exists, return an error
        if (_nativeWindow is not null)
        {
            return MorphicResult.ErrorResult<ICreateNativeWindowError>(new ICreateNativeWindowError.AlreadyExists());
        }

        // create the native window
        var createNewResult = TrayButtonNativeWindow.CreateNew();
        if (createNewResult.IsError)
        {
            var innerError = createNewResult.Error!;
            return MorphicResult.ErrorResult<ICreateNativeWindowError>(new ICreateNativeWindowError.CreateFailed(innerError));
        }
        var nativeWindow = createNewResult.Value!;

        // wire up the native window's MouseUp event (so that we bubble up its event to our creator)
        nativeWindow.MouseUp += (s, e) =>
        {
            this.MouseUp?.Invoke(s, e);
        };

        // set the bitmap ("icon") for the native window; we passing a reference, but we retain ownership (since the window may be created/destroyed repeatedly over time)
        IntPtr hBitmapForNativeWindow = (_bitmapInfo is not null) ? _bitmapInfo.hBitmap : IntPtr.Zero;
        int bitmapWidthForNativeWindow = (_bitmapInfo is not null) ? _bitmapInfo.Width : 0;
        int bitmapHeightForNativeWindow = (_bitmapInfo is not null) ? _bitmapInfo.Height : 0;
        var setBitmapResult = nativeWindow.SetBitmap(hBitmapForNativeWindow, bitmapWidthForNativeWindow, bitmapHeightForNativeWindow);
        if (setBitmapResult.IsError == true)
        {
            nativeWindow.Dispose();
            //
            var innerError = setBitmapResult.Error!;
            return MorphicResult.ErrorResult<ICreateNativeWindowError>(new ICreateNativeWindowError.CouldNotSetBitmap(innerError));
        }
        //
        // set the (tooltip) text for the native window
        var setTextResult = nativeWindow.SetText(_text);
        if (setTextResult.IsError == true)
        {
            nativeWindow.Dispose();
            //
            var innerError = setTextResult.Error!;
            return MorphicResult.ErrorResult<ICreateNativeWindowError>(new ICreateNativeWindowError.CouldNotSetText(innerError));
        }

        return MorphicResult.OkResult(nativeWindow);
    }

    // NOTE: if we tried to show the taskbar button but the operation failed, keep retrying
    // NOTE: we use a Windows Forms timer here instead of a system timer (in an effort to keep the .Show() function call on the main/UI thread)
    private void ReattemptShowTaskButtonTimer_Tick(object? sender, EventArgs e)
    {
        if (_visibility == TrayButtonVisibility.PendingVisible)
        {
            var showResult = this.Show();
            if (showResult.IsSuccess == true)
            {
                // we were successfully able to show the taskbar button; the reattempt timer is not longer necessary
                _reattemptShowTaskbarButtonTimer?.Dispose();
                _reattemptShowTaskbarButtonTimer = null;
            }
        }
        else
        {
            Debug.Assert(false, "ReattemptShowTaskButtonTimerCallback was called, but Visibility is not currently set to .PendingVisible; value: " + _visibility.ToString());
            _reattemptShowTaskbarButtonTimer?.Dispose();
            _reattemptShowTaskbarButtonTimer = null;
        }
    }
}
