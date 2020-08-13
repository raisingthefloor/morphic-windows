// BarEnums.cs: Enumerations used by the bar.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt


namespace Morphic.Bar.Bar
{
    public enum Position
    {
        Absolute = 0,
        Percent = 1,
        Left = 2,
        Top = 3,
        Right = 4,
        Bottom = 5,
        Center = 6,
        Centre = 6,
        Middle = 6
    }

    public enum ExpanderRelative
    {
        Both = 0,
        Primary,
        Secondary
    }

    public enum BarOverflow
    {
        Resize = 0,
        Scale,
        Hide
    }

    public enum BarItemSize
    {
        TextOnly,
        Small,
        Medium,
        Large
    }
}
