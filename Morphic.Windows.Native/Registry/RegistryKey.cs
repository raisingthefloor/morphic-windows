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
using System.Diagnostics;
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
        public class RegistryKey : IDisposable
        {
            bool _disposed = false;

            SafeRegistryHandle _handle;

            public delegate void RegistryKeyChangedEvent(RegistryKey sender, EventArgs e);
			//
            private struct RegistryKeyNotificationInfo
            {
                public RegistryKey RegistryKey;
                public WaitHandle WaitHandle;
                //
                public RegistryKeyChangedEvent EventHandler;

                public RegistryKeyNotificationInfo(RegistryKey registryKey, WaitHandle waitHandle, RegistryKeyChangedEvent eventHandler)
                {
                    this.RegistryKey = registryKey;
                    this.WaitHandle = waitHandle;
                    this.EventHandler = eventHandler;
                }
            }
            private static List<RegistryKeyNotificationInfo> s_registerKeyNotifyPool = new List<RegistryKeyNotificationInfo>();
            private static AutoResetEvent s_registryKeyNotifyPoolUpdatedEvent = new AutoResetEvent(false);
            private static Thread? s_registryKeyNotifyPoolThread = null;
            private static object s_registryKeyNotifyPoolLock = new object();

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
                    // dispose of our underlying registry key's access handle
                    _handle.Dispose();
                }

                _disposed = true;

                // trigger the notification pool to remove our entry (if we were subscribed)
                try
                {
					// TODO: consider having a "SubscribedToNotifications" flag in the RegistryKey (and using that flag to determine if this is necessary/appropriate)
                    s_registryKeyNotifyPoolUpdatedEvent.Set();
                }
                catch { }
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
                if ((typeof(T) == typeof(uint)) ||
                    typeof(T) == typeof(System.String))
                {
                    setValueResult = RegistryKey.SetValueForHandle(handleAsUIntPtr, name, value!);
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

            private static IMorphicResult SetValueForHandle<T>(UIntPtr handle, string? name, T value)
            {
                IntPtr ptrToData;
                uint dataSize;
                ExtendedPInvoke.RegistryValueType valueType;

                if (typeof(T) == typeof(uint))
                {
                    var dataSizeAsInt = Marshal.SizeOf<uint>();
                    ptrToData = Marshal.AllocHGlobal(dataSizeAsInt);
                    var valueAsUInt = (uint)(object)value!;
                    Marshal.StructureToPtr<uint>(valueAsUInt, ptrToData, false);
                    //
                    dataSize = (uint)dataSizeAsInt;
                    valueType = ExtendedPInvoke.RegistryValueType.REG_DWORD;
                }
                else if (typeof(T) == typeof(System.String))
                {
                    var valueAsString = (value as System.String)!;
                    ptrToData = Marshal.StringToHGlobalUni(valueAsString);
                    //
                    dataSize = (uint)((valueAsString.Length + 1 /* +1 for the null terminator */) * 2);
                    valueType = ExtendedPInvoke.RegistryValueType.REG_SZ;
                }
                else
                {
                    // unknown type
                    return IMorphicResult.ErrorResult;
                }
                //
                try
                {
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

            private static int CalculateUnicodeNullTerminatedLengthOfString(string value)
            {
                // NOTE: this has been tested with unicode characters that are both one code unit and two code units; the System.String type automatically increases "length" as appropriate 
                //       when surrogate characters (i.e. 2 chars for 1 symbol) are required
                return (value.Length + 1 /* +1 for the null terminator */) * 2 /* *2 because each character is 2 bytes wide */;
            }

            #endregion SetValue helper functions

            public IMorphicResult RegisterForValueChangeNotification(RegistryKeyChangedEvent eventHandler)
            {
                if (_disposed == true)
                {
                    return IMorphicResult.ErrorResult;
                }

                var waitHandle = new ManualResetEvent(false);

                // NOTE: REG_NOTIFY_CHANGE_LAST_SET will trigger on any changes to the key's values
                // NOTE: registration will auto-unregister after the wait handle is trigger once.  Registration will also auto-unregister when the RegistryKey is closed/disposed
                PInvoke.Win32ErrorCode regNotifyErrorCode;
                try
                {
                    // NOTE: if _handle has been disposed, this will throw an ObjectDisposedException
                    regNotifyErrorCode = PInvoke.AdvApi32.RegNotifyChangeKeyValue(_handle, false, PInvoke.AdvApi32.RegNotifyFilter.REG_NOTIFY_CHANGE_LAST_SET, waitHandle.SafeWaitHandle, true);
                }
                catch(ObjectDisposedException ex)
                {
                    return IMorphicResult.ErrorResult;
                }
                //
                switch (regNotifyErrorCode)
                {
                    case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                        break;
                    default:
                        return IMorphicResult.ErrorResult;
                }

                // add our registry key (and accompanying wait handle) to the notify pool
                // NOTE: this must be the only code which is allowed to add to the notify pool; if we change this behavior, we must re-evaluate and QA the corresponding lock strategy change
                lock(s_registryKeyNotifyPoolLock)
                {
                    var notifyInfo = new RegistryKeyNotificationInfo(this, waitHandle, eventHandler);
                    s_registerKeyNotifyPool.Add(notifyInfo);

                    if(s_registryKeyNotifyPoolThread == null)
                    {
                        s_registryKeyNotifyPoolThread = new Thread(RegistryKey.ListenForRegistryKeyChanges);
                        s_registryKeyNotifyPoolThread.IsBackground = true; // set up as a background thread (so that it shuts down automatically with our application, even if all the RegistryKeys weren't fully disposed)
                        s_registryKeyNotifyPoolThread.Start();
                    } 
                    else
                    {
                        // trigger our notify pool thread to see and watch for the new entries
                        s_registryKeyNotifyPoolUpdatedEvent.Set();
                    }
                }

                return IMorphicResult.SuccessResult;
            }

            private static void ListenForRegistryKeyChanges()
            {
                while(true)
                {
                    // get a copy of our current registry key notification pool (i.e. all registry keys which we are watching)
                    RegistryKeyNotificationInfo[] copyOfNotificationPool;
                    lock (s_registryKeyNotifyPoolLock)
                    {
						// NOTE: we intentionally copy the list into an array to make sure we have a clone of the original list (not a shared reference)
                        copyOfNotificationPool = s_registerKeyNotifyPool.ToArray();

                        // if there are no registry keys which we are subscribed to, exit our function (and shut down our thread) now
                        if (copyOfNotificationPool.Length == 0)
                        {
                            s_registryKeyNotifyPoolThread = null;
                            return;
                        }
                    }

                    // if any of the registry keys have been disposed, then remove them from our list
                    int index = 0;
                    while(index < copyOfNotificationPool.Length)
                    {
                        if (copyOfNotificationPool[index].RegistryKey._disposed == true)
                        {
                            // remove this item from the list
                            var newCopyOfNotificationPool = new RegistryKeyNotificationInfo[copyOfNotificationPool.Length - 1];
                            Array.Copy(copyOfNotificationPool, 0, newCopyOfNotificationPool, 0, index);
                            Array.Copy(copyOfNotificationPool, index + 1, newCopyOfNotificationPool, index, copyOfNotificationPool.Length - index - 1);
                            copyOfNotificationPool = newCopyOfNotificationPool;
                        }
                        else
                        {
                            // continue to the next item
                            index++;
                        }
                    }

                    // create a list of handles to wait on (first the ones which we are watching...and then the one that triggers when the list is updated)
                    var handlesToWaitOn = new WaitHandle[copyOfNotificationPool.Length + 1];
                    for (index = 0; index < copyOfNotificationPool.Length; index++)
                    {
                        handlesToWaitOn[index] = copyOfNotificationPool[index].WaitHandle;
                    }
                    handlesToWaitOn[handlesToWaitOn.Length - 1] = s_registryKeyNotifyPoolUpdatedEvent;

                    // now wait on the handles
                    var indexOfSetHandle = WaitHandle.WaitAny(handlesToWaitOn);
                    if (indexOfSetHandle == handlesToWaitOn.Length - 1)
                    {
                        // list has been updated; start processing the list again
                        continue;
                    }
                    else
                    {
                        // wait handle was triggered!
                        //
                        // capture the notification pool entry which triggered
                        var notificationPoolEntry = copyOfNotificationPool[indexOfSetHandle];
                        // call the event handler on a thread pool thread
                        Task.Run(() => { notificationPoolEntry.EventHandler(notificationPoolEntry.RegistryKey, EventArgs.Empty); });
                        // remove the registry key from our pool
                        lock(s_registryKeyNotifyPoolLock)
                        {
                            for(index = 0; index < s_registerKeyNotifyPool.Count; index++)
                            {
                                // NOTE: type WaitHandle is a class, so this comparison is a reference comparison
                                if (s_registerKeyNotifyPool[index].WaitHandle == notificationPoolEntry.WaitHandle)
                                {
                                    s_registerKeyNotifyPool.RemoveAt(index);
                                    break;
                                }
                            }
                        }
                        //
                        // if the entry we just removed hasn't been disposed, re-register it for notifications
                        if(notificationPoolEntry.RegistryKey._disposed == false)
                        {
                            // re-register the registry key for notification (using its existing event handler), since Windows auto-unregisters registrations every time the handle is triggered
                            var registerForValuechangeNotificationResult = notificationPoolEntry.RegistryKey.RegisterForValueChangeNotification(notificationPoolEntry.EventHandler);
                            if (registerForValuechangeNotificationResult.IsError)
                            {
                                Debug.Assert(false, "Could not re-register registry key for notification after raising event.");
                            }
                        }
                    }                    
                }
            }
        }
    }
}