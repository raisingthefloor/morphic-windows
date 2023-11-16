// Copyright 2020-2022 Raising the Floor - US, Inc.
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

namespace Morphic.Client.Bar.UI.BarControls;

using System.Windows;
using Data;
using Morphic.Client.Bar.Data.Actions;

/// <summary>
/// The control for Button bar items.
/// </summary>
public partial class ImageBarControl : BarItemControl
{
     public ImageBarControl(BarImage barItem) : base(barItem)
     {
          this.InitializeComponent();
     }

     public new BarButton BarItem => (BarButton) base.BarItem;

     private async void BarItemControl_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
     {
          // NOTE: we do not want our bar item control itself to get focus; instead we want the button to get the focus
          
          // to accomplish this focus movement smoothly, we reject the focus on the bar item control by asking that focus move to the next control (i.e. the inner button, if it's focus-enabled)
          var request = new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next);
          this.MoveFocus(request);
     }

     private async void Button_OnClick(object sender, RoutedEventArgs e)
     {
          if (this.BarItem.Action is not null)
          {
               await this.BarItem.Action.InvokeAsync();
          }
     }

     private async void Button_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
     {
          if (this.BarItem.Action is not null)
          {
               if (this.BarItem.Action is InternalAction)
               {
                    if (((InternalAction)this.BarItem.Action).FunctionOnRightClickAlso == true)
                    {
                         await this.BarItem.Action.InvokeAsync();
                    }
               }
          }
     }
}

