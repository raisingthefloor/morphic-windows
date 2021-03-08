using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Windows.Native
{
    public record Win32ApiError : MorphicAssociatedValueEnum<Win32ApiError.Values>
    {
        // enum members
        public enum Values
        {
            Win32Error/*(int win32ErrorCode)*/
        }

        // functions to create member instances
        public static Win32ApiError Win32Error(int win32ErrorCode) => new Win32ApiError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

        // associated values
        public int? Win32ErrorCode { get; private set; }

        // verbatim required constructor implementation for MorphicAssociatedValueEnums
        private Win32ApiError(Values value) : base(value) { }
    }
}
