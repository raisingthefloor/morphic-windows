using System;
using System.Collections.Generic;
using System.Text;

namespace Morphic.Windows.Native.OsVersion
{
    public enum Windows10Version
    {
        v1809,
        v1903,
        v1909,
        v2004,
        v20H2,
        vFuture // any future release we're not yet aware of
    }
    public struct OsVersion
    {
        public static Windows10Version? GetWindows10Version()
        {
            var platform = System.Environment.OSVersion.Platform;
            var version = System.Environment.OSVersion.Version;

            if ((version.Major == 10) && (version.Minor == 0))
            {
                switch (version.Build)
                {
                    case 17763:
                        return Windows10Version.v1809;
                    case 18362:
                        return Windows10Version.v1903;
                    case 18363:
                        return Windows10Version.v1909;
                    case 19041:
                        return Windows10Version.v2004;
                    case 19042:
                        return Windows10Version.v20H2;
                    default:
                        if (version.Build > 19042)
                        {
                            return Windows10Version.vFuture;
                        }
                        else
                        {
                            return null;
                        }
                }
            }
            else if ((version.Major == 10) && (version.Minor > 0))
            {
                return Windows10Version.vFuture;
            }
            else if (version.Major > 11)
            {
                return Windows10Version.vFuture;
            }
            else /* if (version.Major < 10) */
            {
                return null;
            }
        }
    }
}
