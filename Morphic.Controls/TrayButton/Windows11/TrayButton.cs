// Copyright 2020-2023 Raising the Floor - US, Inc.
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
     private System.Drawing.Bitmap? _bitmap = null;
     private string? _text = null;
     private bool _visible = false;

     public event System.Windows.Forms.MouseEventHandler? MouseUp;

     private TrayButtonNativeWindow? _nativeWindow = null;

     private bool disposedValue;

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
                    // TODO: dispose managed state (managed objects)
               }

               // TODO: free unmanaged resources (unmanaged objects) and override finalizer
               this.DestroyNativeWindow();

               // TODO: set large fields to null
               disposedValue = true;
          }
     }

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
               return _bitmap;
          }
          set
          {
               _bitmap = value;

               _nativeWindow?.SetBitmap(_bitmap);
          }
     }

     public string? Text
     {
          get
          {
               return _text;
          }
          set
          {
               _text = value;

               _nativeWindow?.SetText(_text);
          }
     }

     public bool Visible
     {
          set
          {
               if (_visible != value)
               {
                    switch (value)
                    {
                         case true:
                              var showResult = this.Show();
                              Debug.Assert(showResult.IsSuccess == true, "Could not show Morphic icon on task bar.");
                              break;
                         case false:
                              this.Hide();
                              break;
                    }
               }
          }
          get
          {
               return _visible;
          }
     }

     //

     public MorphicResult<MorphicUnit, MorphicUnit> Show()
     {
          _visible = true;

          if (_nativeWindow is null)
          {
               var createNativeWindowResult = this.CreateNativeWindow();
               if (createNativeWindowResult.IsError == true)
               {
                    return MorphicResult.ErrorResult();
               }
          }

          return MorphicResult.OkResult();
     }

     public void Hide()
     {
          _visible = false;

          if (_nativeWindow is not null)
          {
               this.DestroyNativeWindow();
          }
     }

     //

     public void SuppressTaskbarButtonResurfaceChecks(bool suppress)
     {
          _nativeWindow?.SuppressTaskbarButtonResurfaceChecks(suppress);
     }

     //

     private MorphicResult<MorphicUnit, MorphicUnit> CreateNativeWindow()
     {
          // if our native window already exists, return an error
          if (_nativeWindow is not null)
          {
               return MorphicResult.ErrorResult();
          }

          // create the native window
          var createNewResult = TrayButtonNativeWindow.CreateNew();
          if (createNewResult.IsError)
          {
               return MorphicResult.ErrorResult();
          }
          var nativeWindow = createNewResult.Value!;

          // wire up the native window's MouseUp event (so that we pass along its event to our creator)
          nativeWindow.MouseUp += (s, e) =>
          {
               this.MouseUp?.Invoke(s, e);
          };

          // set the bitmap ("icon") for the native window
          nativeWindow.SetBitmap(_bitmap);
          //
          // set the (tooltip) text for the native window
          nativeWindow.SetText(_text);

          // store the reference to our new native window
          _nativeWindow = nativeWindow;

          return MorphicResult.OkResult();
     }

     private void DestroyNativeWindow()
     {
          _nativeWindow?.Dispose();
          _nativeWindow = null;
     }
}
