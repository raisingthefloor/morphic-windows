// Copyright 2026 Raising the Floor - US, Inc.
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

namespace Morphic.Controls;

internal static class RectMath
{
    internal static bool RectIsInside(Windows.Win32.Foundation.RECT self, Windows.Win32.Foundation.RECT rect)
    {
        return ((self.left >= rect.left) && (self.right <= rect.right) && (self.top >= rect.top) && (self.bottom <= rect.bottom));
    }

    internal static bool RectHasNonZeroWidthOrHeight(Windows.Win32.Foundation.RECT self)
    {
        return ((self.left == self.right) || (self.top == self.bottom));
    }

    internal static bool RectIntersects(Windows.Win32.Foundation.RECT self, Windows.Win32.Foundation.RECT rect)
    {
        bool overlapsHorizontally = (self.right > rect.left) && (self.left < rect.right);
        bool overlapsVertically = (self.bottom > rect.top) && (self.top < rect.bottom);
        return overlapsHorizontally && overlapsVertically;
    }
}
