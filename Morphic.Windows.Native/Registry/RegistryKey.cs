// Copyright 2020-2021 Raising the Floor - International
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

using Microsoft.Win32.SafeHandles;
using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Morphic.Windows.Native
{
    public partial class Registry
    {
        public class RegistryKey: IDisposable
        {
            bool _disposed = false;

            SafeRegistryHandle _handle;

            internal RegistryKey(SafeRegistryHandle handle)
            {
                _handle = handle;
            }

            ~RegistryKey() => Dispose(false);
            //
            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }
            //
            protected virtual void Dispose(bool disposing)
            {
                if (_disposed == true)
                {
                    return;
                }

                if (disposing == true)
                {
                    _handle.Dispose();
                }

                _disposed = true;
            }

            public IMorphicResult<RegistryKey> OpenSubKey(string name, bool writable = false)
            {
                // configure key access requirements
                PInvoke.Kernel32.ACCESS_MASK accessMask = (PInvoke.Kernel32.ACCESS_MASK)131097 /* READ_KEY [0x2_0000 | KEY_NOTIFY = 0x0010 | KEY_ENUMERATE_SUB_KEYS = 0x0008 | KEY_QUERY_VALUE = 0x0001] */;
                if (writable == true)
                {
                    accessMask |= (PInvoke.Kernel32.ACCESS_MASK)131078 /* WRITE_KEY [0x2_0000 | KEY_CREATE_SUB_KEY = 0x0004 | KEY_SET_VALUE = 0x0002] */;
                }

                // TODO: make "notify" access an option (although it looks like it's already in the READ access mask specified above)
                accessMask |= 0x0010 /* KEY_NOTIFY */;

                // open our key
                SafeRegistryHandle subKeyHandle;
                var openKeyErrorCode = PInvoke.AdvApi32.RegOpenKeyEx(_handle, name, PInvoke.AdvApi32.RegOpenKeyOptions.None, accessMask, out subKeyHandle);
                switch (openKeyErrorCode)
                {
                    case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                        break;
                    default:
                        // NOTE: in the future, we may want to consider returning a specific result (i.e. "could not open for write access" etc.)
                        return IMorphicResult<RegistryKey>.ErrorResult();
                }
                var subKey = new RegistryKey(subKeyHandle);

                return IMorphicResult<RegistryKey>.SuccessResult(subKey);
            }

            // NOTE: this function is provided for legacy code compatibility (i.e. for code designed around Microsoft.Win32 registry functions)
            public IMorphicResult<object> GetValue(string? name)
            {
                var getValueAndTypeAsObjectResult = this.GetValueAndTypeAsObject(name);
                if (getValueAndTypeAsObjectResult.IsError == true)
                {
                    return IMorphicResult<object>.ErrorResult();
                }
                var valueType = getValueAndTypeAsObjectResult.Value.ValueType;
                var data = getValueAndTypeAsObjectResult.Value.Data;

                return IMorphicResult<object>.SuccessResult(data);
            }

            public IMorphicResult<T> GetValue<T>(string? name)
            {
                var getValueAndTypeAsObjectResult = this.GetValueAndTypeAsObject(name);
                if (getValueAndTypeAsObjectResult.IsError == true)
                {
                    return IMorphicResult<T>.ErrorResult();
                }
                var valueType = getValueAndTypeAsObjectResult.Value.ValueType;
                var data = getValueAndTypeAsObjectResult.Value.Data;

                if ((typeof(T) == typeof(uint)) && (valueType == ExtendedPInvoke.RegistryValueType.REG_DWORD))
                {
                    return IMorphicResult<T>.SuccessResult((T)data);
                }
                else
                {
                    // for all other types (and for type mismatches), return an error
                    return IMorphicResult<T>.ErrorResult();
                }
            }

            private struct GetValueAndTypeAsObjectResult
            {
                public object Data;
                public ExtendedPInvoke.RegistryValueType ValueType;

                public GetValueAndTypeAsObjectResult(object data, ExtendedPInvoke.RegistryValueType valueType) {
                    this.Data = data;
                    this.ValueType = valueType;
                }
            }
            private IMorphicResult<GetValueAndTypeAsObjectResult> GetValueAndTypeAsObject(string? name)
            {
                var handleAsUIntPtr = (UIntPtr)(_handle.DangerousGetHandle().ToInt64());

                // pass 1: set dataSize to zero (so that RegQueryValueEx returns the size of the value
                uint dataSize = 0;
                ExtendedPInvoke.RegistryValueType valueType;
                var queryValueErrorCode = ExtendedPInvoke.RegQueryValueEx(handleAsUIntPtr, name, IntPtr.Zero, out valueType, IntPtr.Zero, ref dataSize);
                switch (queryValueErrorCode)
                {
                    case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                        break;
                    default:
                        // NOTE: in the future, we may want to consider returning a specific result (i.e. "could not open for write access" etc.)
                        return IMorphicResult<GetValueAndTypeAsObjectResult>.ErrorResult();
                }

                // pass 2: capture the actual data
                var getValueResult = RegistryKey.GetValueForHandleAsUInt32(handleAsUIntPtr, name, dataSize);
                if (getValueResult.IsError)
                {
                    return IMorphicResult<GetValueAndTypeAsObjectResult>.ErrorResult();
                }

                var data = getValueResult.Value;
                return IMorphicResult<GetValueAndTypeAsObjectResult>.SuccessResult(new GetValueAndTypeAsObjectResult(data, valueType));
            }

            #region GetValue helper functions

            private static IMorphicResult<uint> GetValueForHandleAsUInt32(UIntPtr handle, string? name, uint dataSize)
            {
                var ptrToData = Marshal.AllocHGlobal((int)dataSize);
                try
                {
                    var mutableDataSize = dataSize;
                    ExtendedPInvoke.RegistryValueType valueType;
                    var queryValueErrorCode = ExtendedPInvoke.RegQueryValueEx(handle, name, IntPtr.Zero, out valueType, ptrToData, ref mutableDataSize);
                    switch (queryValueErrorCode)
                    {
                        case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                            break;
                        default:
                            // NOTE: in the future, we may want to consider returning a specific result (i.e. "could not open for write access" etc.)
                            return IMorphicResult<uint>.ErrorResult();
                    }

                    // validate value type and data size
                    if (valueType != ExtendedPInvoke.RegistryValueType.REG_DWORD)
                    {
                        return IMorphicResult<uint>.ErrorResult();
                    }
                    //
                    if (mutableDataSize != dataSize)
                    {
                        return IMorphicResult<uint>.ErrorResult();
                    }

                    // capture and return result
                    var data = Marshal.PtrToStructure<uint>(ptrToData);
                    return IMorphicResult<uint>.SuccessResult(data);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptrToData);
                }
            }

            #endregion GetValue helper functions

            // NOTE: this function is provided for legacy code compatibility (i.e. for code designed around Microsoft.Win32 registry functions)
            public IMorphicResult SetValue(string? name, object value)
            {
                if(value.GetType() == typeof(uint))
                {
                    return this.SetValue<uint>(name, (uint)value);
                }
                else
                {
                    // unknown type
                    return IMorphicResult.ErrorResult;
                }
            }

            public IMorphicResult SetValue<T>(string? name, T value)
            {
                var handleAsUIntPtr = (UIntPtr)(_handle.DangerousGetHandle().ToInt64());

                IMorphicResult setValueResult;
                if (typeof(T) == typeof(uint))
                {
                    setValueResult = RegistryKey.SetValueForHandle(handleAsUIntPtr, name, (uint)(object)value!);
                }
                else
                {
                    // unknown type
                    return IMorphicResult.ErrorResult;
                }
                if (setValueResult.IsError == true)
                {
                    return IMorphicResult.ErrorResult;
                }

                return IMorphicResult.SuccessResult;
            }

            #region SetValue helper functions

            private static IMorphicResult SetValueForHandle(UIntPtr handle, string? name, uint value)
            {
                var dataSizeAsInt = Marshal.SizeOf<uint>();
                var ptrToData = Marshal.AllocHGlobal(dataSizeAsInt);
                Marshal.StructureToPtr<uint>(value, ptrToData, false);
                try
                {
                    var dataSize = (uint)dataSizeAsInt;
                    var valueType = ExtendedPInvoke.RegistryValueType.REG_DWORD;
                    var setValueErrorCode = ExtendedPInvoke.RegSetValueEx(handle, name, 0, valueType, ptrToData, dataSize);
                    switch (setValueErrorCode)
                    {
                        case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                            break;
                        default:
                            // NOTE: in the future, we may want to consider returning a specific result (i.e. "could not open for write access" etc.)
                            return IMorphicResult.ErrorResult;
                    }

                    // setting the value was a success
                    return IMorphicResult.SuccessResult;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptrToData);
                }
            }

            #endregion SetValue helper functions

            public IMorphicResult RegisterForValueChangeNotification(WaitHandle waitHandle)
            {
                // NOTE: REG_NOTIFY_CHANGE_LAST_SET will trigger on any changes to the key's values
                // NOTE: registration will auto-unregister after the wait handle is trigger once.  Registration will also auto-unregister when the RegistryKey is closed/disposed
                var regNotifyErrorCode = PInvoke.AdvApi32.RegNotifyChangeKeyValue(_handle, false, PInvoke.AdvApi32.RegNotifyFilter.REG_NOTIFY_CHANGE_LAST_SET, waitHandle.SafeWaitHandle, true);
                switch (regNotifyErrorCode)
                {
                    case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                        break;
                    default:
                        return IMorphicResult.ErrorResult;
                }

                return IMorphicResult.SuccessResult;
            }
        }
    }
}