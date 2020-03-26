using System;
using System.Collections.Generic;
using System.Text;

namespace MorphicSettings
{
    public class Display
    {

        public enum ZoomLevel
        {
            Normal,
            Percent125,
            Percent150,
            Percent200
        }

        private static Display? main;

        public static Display Main {
            get
            {
                if (main == null)
                {
                    main = new Display();
                }
                return main;
            }
        }

        public bool SetZoomLevel(ZoomLevel zoomLevel)
        {
            // TODO: implement
            return false;
        }

    }
}
