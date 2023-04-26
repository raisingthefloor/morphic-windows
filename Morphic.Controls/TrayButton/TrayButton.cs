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

using Morphic.Controls.TrayButton.Windows10;
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
     Morphic.Controls.TrayButton.Windows10.TrayButton _trayButton;

     public event MouseEventHandler? MouseUp;

     public TrayButton()
     {
          _trayButton = new();
          _trayButton.MouseUp += _trayButton_MouseUp;
     }

     public void Dispose()
     {
          _trayButton.Dispose();
     }

     public Icon? Icon
     {
          get
          {
               return _trayButton.Icon;
          }
          set
          {
               _trayButton.Icon = value;
          }
     }

     /// <summary>Tooltip for the tray button.</summary>
     public string? Text
     {
          get
          {
               return _trayButton.Text;
          }
          set
          {
               _trayButton.Text = value;
          }
     }

     /// <summary>Show or hide the tray button.</summary>
     public bool Visible
     {
          get
          {
               return _trayButton.Visible;
          }
          set
          {
               _trayButton.Visible = value;
          }
     }

     private void _trayButton_MouseUp(object? sender, MouseEventArgs e)
     {
          this.MouseUp?.Invoke(sender, e);
     }
}
