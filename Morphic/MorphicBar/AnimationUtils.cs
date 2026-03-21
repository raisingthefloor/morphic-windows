// Copyright 2026 Raising the Floor - US, Inc.
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
using System.Text;

namespace Morphic.MorphicBar;

internal class AnimationUtils
{
    /// <summary>
    /// Smoothly animates the window to the target position and optionally to a target size,
    /// using ease-out cubic interpolation. If called while a previous animation is in progress,
    /// the current animation is cancelled and a new one starts from the window's current state.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueueTimer AnimateMoveTo(Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue, Microsoft.UI.Windowing.AppWindow appWindow, Windows.Graphics.PointInt32 targetPosition, Windows.Graphics.SizeInt32 targetSize, TimeSpan duration)
    {
        var startPosition = appWindow.Position;
        var startSize = appWindow.Size;

        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        var sizeChanging = (targetSize.Width != startSize.Width || targetSize.Height != startSize.Height);

        var moveAnimationTimer = dispatcherQueue.CreateTimer();
        moveAnimationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
        moveAnimationTimer.IsRepeating = true;
        moveAnimationTimer.Tick += (s, e) =>
        {
            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            var t = Math.Min(elapsedMilliseconds / duration.TotalMilliseconds, 1.0);

            // ease-out cubic for smooth deceleration
            t = 1.0 - Math.Pow(1.0 - t, 3);

            var x = (int)(startPosition.X + (targetPosition.X - startPosition.X) * t);
            var y = (int)(startPosition.Y + (targetPosition.Y - startPosition.Y) * t);

            if (sizeChanging)
            {
                var w = (int)(startSize.Width + (targetSize.Width - startSize.Width) * t);
                var h = (int)(startSize.Height + (targetSize.Height - startSize.Height) * t);
                appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, w, h));
            }
            else
            {
                appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            }

            if (t >= 1.0)
            {
                moveAnimationTimer.Stop();
            }
        };
        moveAnimationTimer.Start();
        return moveAnimationTimer;
    }
}
