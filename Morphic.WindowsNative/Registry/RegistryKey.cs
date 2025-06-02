// Copyright 2020-2025 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Morphic.WindowsNative;

public partial class Registry
{
    public class RegistryKey : IDisposable
    {
        private bool disposedValue;

        private SafeRegistryHandle _handle;

        public delegate void RegistryKeyChangedEventHandler(RegistryKey sender, EventArgs e);

        private record RegistryKeyNotificationInfo
        {
            public readonly RegistryKey RegistryKey;
            public readonly WaitHandle WaitHandle;
            //
            private List<RegistryKeyChangedEventHandler> _eventHandlers;
            private object _eventHandlersLock = new object();

            internal bool MarkedForDisposal { get; private set; } = false;

            public RegistryKeyNotificationInfo(RegistryKey registryKey, WaitHandle waitHandle, RegistryKeyChangedEventHandler eventHandler)
            {
                this.RegistryKey = registryKey;
                this.WaitHandle = waitHandle;
                //
                _eventHandlers = new() { eventHandler };
            }

            internal List<RegistryKeyChangedEventHandler> GetCopyOfEventHandlers()
            {
                List<RegistryKeyChangedEventHandler> result = new();
                lock (_eventHandlersLock)
                {
                    foreach (var eventHandler in _eventHandlers)
                    {
                        result.Add(eventHandler);
                    }
                }
                return result;
            }

            internal int GetEventHandlersCount()
            {
                return _eventHandlers.Count;
            }

            internal void AddEventHandler(RegistryKeyChangedEventHandler eventHandler)
            {
                lock (_eventHandlersLock)
                {
                    // if the event handler is already in our list, return early
                    foreach (var handler in _eventHandlers)
                    {
                        if (handler == eventHandler)
                        {
                            return;
                        }
                    }

                    _eventHandlers.Add(eventHandler);
                }
            }

            // NOTE: this function returns true if the event handler was removed and false if the event handler was not present
            internal bool RemoveEventHandler(RegistryKeyChangedEventHandler eventHandler)
            {
                lock (_eventHandlersLock)
                {
                    // if the event handler is already in our list, return early
                    for (var index = 0; index < _eventHandlers.Count; index += 1)
                    {
                        var handler = _eventHandlers[index];
                        if (handler == eventHandler)
                        {
                            _eventHandlers.RemoveAt(index);
                            return true;
                        }
                    }
                }

                return false;
            }

            internal void MarkForDisposal()
            {
                this.MarkedForDisposal = true;
            }
        }
        private static List<RegistryKeyNotificationInfo> s_registerKeyNotifyPool = new List<RegistryKeyNotificationInfo>();
        private static AutoResetEvent s_registryKeyNotifyPoolUpdatedEvent = new AutoResetEvent(false);
        private static Thread? s_registryKeyNotifyPoolThread = null;
        private static object s_registryKeyNotifyPoolLock = new object();

        private RegistryKeyNotificationInfo? _registryKeyNotifyPoolEntry;
        private static object _registryKeyNotifyPoolEntriesLock = new object();

        internal RegistryKey(SafeRegistryHandle handle)
        {
            _handle = handle;
        }
        //
        internal RegistryKey(Windows.Win32.System.Registry.HKEY hkey)
        {
            IntPtr hkeyValueAsIntPtr;
            unsafe 
            {
                hkeyValueAsIntPtr = new IntPtr(hkey.Value);
            }
            var handle = new SafeRegistryHandle(hkeyValueAsIntPtr, ownsHandle: false);

            _handle = handle;
        }

        // NOTE: we override the finalizer because 'Dispose(bool disposing)' has code to free unmanaged resources
        ~RegistryKey()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    var lockEntered = Monitor.TryEnter(_registryKeyNotifyPoolEntriesLock);
                    try
                    {
                        if (_registryKeyNotifyPoolEntry is not null)
                        {
                            // if we are subscribed to notifications, mark our notify pool's entry as "being disposed"
                            // NOTE: this will indicate to the registry key notify pool handler thread that this registry key should be removed from the pool once its wait handle is
                            //       triggered; its wait handle should be triggered when our handle is disposed
                            _registryKeyNotifyPoolEntry.MarkForDisposal();
                        }
                    }
                    finally
                    {
                        if (lockEntered == true)
                        {
                            Monitor.Exit(_registryKeyNotifyPoolEntriesLock);
                        }
                    }

                    // dispose of our underlying registry key's access handle
                    _handle.Dispose();
                }

                // free unmanaged resources (unmanaged objects)
                // NOTE: if we have unmanaged resources to free, we need to implement the finalizer (~RegistryKey) function for this class
                // [none]

                // set large fields to null
                // [none]

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        //

        public MorphicResult<bool, Win32ApiError> SubKeyExists(string name)
        {
            var getSubKeyNamesResult = this.GetSubKeyNames();
            if (getSubKeyNamesResult.IsError == true)
            {
                switch (getSubKeyNamesResult.Error!.Value)
                {
                    case Win32ApiError.Values.Win32Error:
                        return MorphicResult.ErrorResult(Win32ApiError.Win32Error(getSubKeyNamesResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var keyNames = getSubKeyNamesResult.Value!;

            var subKeyExists = false;
            foreach (var keyName in keyNames)
            {
                if (String.Equals(keyName, name, StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    subKeyExists = true;
                    break;
                }
            }

            return MorphicResult.OkResult(subKeyExists);
        }

        public MorphicResult<List<string>, Win32ApiError> GetSubKeyNames()
        {
            var handleAsUIntPtr = (UIntPtr)(_handle.DangerousGetHandle().ToInt64());

            // see: https://learn.microsoft.com/en-us/windows/win32/sysinfo/registry-element-size-limits
            const int MAX_KEY_NAME_LENGTH = 256; // 255 characters (plus null terminator to be safe)

            List<string> result = new();

            uint index = 0;
            while (true)
            {
                var keyName = new StringBuilder(MAX_KEY_NAME_LENGTH);
                var keyNameLength = (uint)MAX_KEY_NAME_LENGTH;

                var enumKeyErrorCode = ExtendedPInvoke.RegEnumKeyEx(handleAsUIntPtr, index, keyName, ref keyNameLength, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (enumKeyErrorCode == PInvoke.Win32ErrorCode.ERROR_SUCCESS)
                {
                    // expected condition; nothing to do
                }
                else if (enumKeyErrorCode == PInvoke.Win32ErrorCode.ERROR_NO_MORE_ITEMS)
                {
                    // no more items
                    break;
                }
                else
                {
                    return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)enumKeyErrorCode));
                }

                // NOTE: the RegEnumKeyEx function returns the string length in characters, without including the null terminator in the count
                var element = keyName.ToString(0, (int)keyNameLength);
                result.Add(element);

                index += 1;
            }

            return MorphicResult.OkResult(result);
        }

        public MorphicResult<RegistryKey, IWin32ApiError> OpenSubKey(string name, bool writable = false)
        {
            // configure key access requirements
            // see: https://learn.microsoft.com/en-us/windows/win32/sysinfo/registry-key-security-and-access-rights
            // NOTE: "notify" is already included in the standard KEY_READ access flags, so we don't make it an optional permission for the caller (i.e. we didn't include "bool notifiable" as a parameter)
            //
            // set our base permissions as read (i.e. read, query value, enumerate subkeys, notify)
            var accessMask = Windows.Win32.System.Registry.REG_SAM_FLAGS.KEY_READ;
            //
            if (writable == true)
            {
                // add write permissions (i.e. write, set value, create subkey)
                accessMask |= Windows.Win32.System.Registry.REG_SAM_FLAGS.KEY_WRITE;
            }

            // open our key
            SafeRegistryHandle subKeyHandle;
            var openKeyErrorCode = Windows.Win32.PInvoke.RegOpenKeyEx(_handle, name, 0/* None */, accessMask, out subKeyHandle);
            switch (openKeyErrorCode)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    break;
                default:
                    // NOTE: in the future, we may want to consider returning a specific result (i.e. "could not open for write access" etc.) rather than the Win32 error code
                    return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error(unchecked((uint)openKeyErrorCode)));
            }
            var subKey = new RegistryKey(subKeyHandle);

            return MorphicResult.OkResult(subKey);
        }

        //

        public MorphicResult<List<string?>, Win32ApiError> GetValueNames()
        {
            var handleAsUIntPtr = (UIntPtr)(_handle.DangerousGetHandle().ToInt64());

            // see: https://learn.microsoft.com/en-us/windows/win32/sysinfo/registry-element-size-limits
            const int MAX_VALUE_NAME_LENGTH = 16_384; // 16383 characters (plus null terminator to be safe)

            List<string?> result = new();

            uint index = 0;
            while (true)
            {
                var valueName = new StringBuilder(MAX_VALUE_NAME_LENGTH);
                var valueNameLength = (uint)MAX_VALUE_NAME_LENGTH;

                var enumValueErrorCode = ExtendedPInvoke.RegEnumValue(handleAsUIntPtr, index, valueName, ref valueNameLength, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (enumValueErrorCode == PInvoke.Win32ErrorCode.ERROR_SUCCESS)
                {
                    // expected condition; nothing to do
                }
                else if (enumValueErrorCode == PInvoke.Win32ErrorCode.ERROR_NO_MORE_ITEMS)
                {
                    // no more items
                    break;
                }
                else
                {
                    return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)enumValueErrorCode));
                }

                // NOTE: the RegEnumValue function returns the string length in characters, without including the null terminator in the count
                var element = valueName.ToString(0, (int)valueNameLength);
                result.Add(element);

                index += 1;
            }

            return MorphicResult.OkResult(result);
        }

        //

        //    //

        public interface IRegistryGetValueError
        {
            public record TypeMismatch : IRegistryGetValueError;
            public record UnsupportedType : IRegistryGetValueError;
            public record ValueDoesNotExist : IRegistryGetValueError;
            public record Win32Error(int Win32ErrorCode) : IRegistryGetValueError;
        }

        // NOTE: this function is provided for legacy code compatibility (i.e. for code designed around Microsoft.Win32 registry functions)
        public MorphicResult<object, IRegistryGetValueError> GetValueData(string? valueName)
        {
            var getValueAndTypeAsObjectResult = this.GetValueDataAndTypeAsObject(valueName);
            if (getValueAndTypeAsObjectResult.IsError == true)
            {
                return MorphicResult.ErrorResult<IRegistryGetValueError>(getValueAndTypeAsObjectResult.Error!);
            }
            //var valueType = getValueAndTypeAsObjectResult.Value.ValueType;
            var data = getValueAndTypeAsObjectResult.Value.ValueData;

            return MorphicResult.OkResult(data);
        }

        // NOTE: see both implementations of GetValueDataOrNull; they (both struct- and class-specific) must be kept in sync
        // NOTE: this first implementation of GetValueDataOrNull is used for struct types
        public MorphicResult<T?, IRegistryGetValueError> GetValueDataOrNull<T>(string? valueName) where T : struct
        {
            var getValueDataResult = this.GetValueData<T>(valueName);
            if (getValueDataResult.IsError == true)
            {
                switch (getValueDataResult.Error!)
                {
                    case IRegistryGetValueError.ValueDoesNotExist:
                        return MorphicResult.OkResult<T?>(null);
                    default:
                        return MorphicResult.ErrorResult(getValueDataResult.Error!);
                }
            }
            var valueData = getValueDataResult.Value!;

            return MorphicResult.OkResult<T?>(valueData);
        }
        //
        // NOTE: this second implementation of GetValueDataOrNull (with the _ param allowing it to act as an overload) is a kludge so that C# will work with both Nullable value types and (already-traditionally-nullable) reference types
        public MorphicResult<T?, IRegistryGetValueError> GetValueDataOrNull<T>(string? valueName, object? _ = null) where T : class
        {
            var getValueDataResult = this.GetValueData<T>(valueName);
            if (getValueDataResult.IsError == true)
            {
                switch (getValueDataResult.Error!)
                {
                    case IRegistryGetValueError.ValueDoesNotExist:
                        return MorphicResult.OkResult<T?>(null);
                    default:
                        return MorphicResult.ErrorResult(getValueDataResult.Error!);
                }
            }
            var valueData = getValueDataResult.Value!;

            return MorphicResult.OkResult<T?>(valueData);
        }

        public MorphicResult<T, IRegistryGetValueError> GetValueData<T>(string? valueName)
        {
            var getValueAndTypeAsObjectResult = this.GetValueDataAndTypeAsObject(valueName);
            if (getValueAndTypeAsObjectResult.IsError == true)
            {
                return MorphicResult.ErrorResult(getValueAndTypeAsObjectResult.Error!);
            }
            var valueType = getValueAndTypeAsObjectResult.Value.ValueType;
            var valueData = getValueAndTypeAsObjectResult.Value.ValueData;

            if (typeof(T) == typeof(string))
            {
                if (valueType == Windows.Win32.System.Registry.REG_VALUE_TYPE.REG_SZ)
                {
                    return MorphicResult.OkResult((T)valueData);
                }
                else
                {
                    return MorphicResult.ErrorResult<IRegistryGetValueError>(new IRegistryGetValueError.TypeMismatch());
                }
            }
            if (typeof(T) == typeof(uint))
            {
                if (valueType == Windows.Win32.System.Registry.REG_VALUE_TYPE.REG_DWORD)
                {
                    return MorphicResult.OkResult((T)valueData);
                }
                else
                {
                    return MorphicResult.ErrorResult<IRegistryGetValueError>(new IRegistryGetValueError.TypeMismatch());
                }
            }
            else
            {
                // for all other types, return an error
                return MorphicResult.ErrorResult<IRegistryGetValueError>(new IRegistryGetValueError.UnsupportedType());
            }
        }

        private struct GetValueDataAndTypeAsObjectResult
        {
            public object ValueData;
            public Windows.Win32.System.Registry.REG_VALUE_TYPE ValueType;

            public GetValueDataAndTypeAsObjectResult(object data, Windows.Win32.System.Registry.REG_VALUE_TYPE valueType)
            {
                this.ValueData = data;
                this.ValueType = valueType;
            }
        }
        //
        private MorphicResult<GetValueDataAndTypeAsObjectResult, IRegistryGetValueError> GetValueDataAndTypeAsObject(string? valueName)
        {
            // pass 1: set dataSize to zero (so that RegQueryValueEx returns the size of the value)
            uint valueDataSize = 0;
            Windows.Win32.System.Registry.REG_VALUE_TYPE valueType;
            Windows.Win32.Foundation.WIN32_ERROR queryValueErrorCode;
            unsafe
            {
                queryValueErrorCode = Windows.Win32.PInvoke.RegQueryValueEx(_handle, valueName, &valueType, null, &valueDataSize);
            }
            switch (queryValueErrorCode)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    break;
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_FILE_NOT_FOUND:
                    return MorphicResult.ErrorResult<IRegistryGetValueError>(new IRegistryGetValueError.ValueDoesNotExist());
                default:
                    // NOTE: in the future, we may want to consider returning a specific result (i.e. "could not open for write access" etc.)
                    return MorphicResult.ErrorResult<IRegistryGetValueError>(new IRegistryGetValueError.Win32Error((int)queryValueErrorCode));
            }

            // pass 2: capture the actual value data
            Span<byte> data = new byte[(int)valueDataSize];
            object valueData;

            var mutableDataSize = valueDataSize;
            unsafe
            {
                fixed (byte* pointerToData = data)
                {
                    queryValueErrorCode = Windows.Win32.PInvoke.RegQueryValueEx(_handle, valueName, &valueType, pointerToData, &mutableDataSize);
                }
            }
            switch (queryValueErrorCode)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    break;
                default:
                    // NOTE: in the future, we may want to consider returning a specific result (i.e. "could not open for write access" etc.)
                    return MorphicResult.ErrorResult<IRegistryGetValueError>(new IRegistryGetValueError.Win32Error((int)queryValueErrorCode));
            }

            // capture and return result
            switch (valueType)
            {
                case Windows.Win32.System.Registry.REG_VALUE_TYPE.REG_DWORD:
                    {
                        valueData = BitConverter.ToUInt32(data);
                    }
                    break;
                case Windows.Win32.System.Registry.REG_VALUE_TYPE.REG_SZ:
                    {
                        var valueDataAsChars = System.Text.Encoding.Unicode.GetChars(data.ToArray(), 0, (int)valueDataSize);
                        if (valueDataAsChars.Length > 0)
                        {
                            if (valueDataAsChars[valueDataAsChars.Length - 1] == '\0')
                            {
                                valueData = new String(valueDataAsChars, 0, valueDataAsChars.Length - 1);
                            }
                            else
                            {
                                Debug.Assert(false, "RegQueryValueEx returned a string which is not null terminated.");
                                valueData = new String(valueDataAsChars, 0, valueDataAsChars.Length);
                            }
                        }
                        else
                        {
                            Debug.Assert(false, "RegQueryValueEx returned a zero-character string (no characters, no null termination).");
                            valueData = "";
                        }
                    }
                    break;
                default:
                    {
                        Debug.Assert(false, "Support for this registry value type is not yet implemented.");
                        return MorphicResult.ErrorResult<IRegistryGetValueError>(new IRegistryGetValueError.UnsupportedType());
                    }
            }

            return MorphicResult.OkResult(new GetValueDataAndTypeAsObjectResult(valueData, valueType));
        }

        //

        public interface IRegistryDeleteValueError
        {
            public record ValueDoesNotExist : IRegistryDeleteValueError;
            public record Win32Error(int Win32ErrorCode) : IRegistryDeleteValueError;
        }

        public MorphicResult<MorphicUnit, IRegistryDeleteValueError> DeleteValue(string? valueName)
        {
            Windows.Win32.Foundation.WIN32_ERROR deleteValueErrorCode;
            deleteValueErrorCode = Windows.Win32.PInvoke.RegDeleteValue(_handle, valueName);
            switch (deleteValueErrorCode)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    break;
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_FILE_NOT_FOUND:
                    return MorphicResult.ErrorResult<IRegistryDeleteValueError>(new IRegistryDeleteValueError.ValueDoesNotExist());
                default:
                    // NOTE: in the future, we may want to consider returning a specific result (i.e. "could not open for write access" etc.)
                    return MorphicResult.ErrorResult<IRegistryDeleteValueError>(new IRegistryDeleteValueError.Win32Error((int)deleteValueErrorCode));
            }

            // deleting the value was a success
            return MorphicResult.OkResult();
        }

        //

        public interface IRegistrySetValueError
        {
            public record UnsupportedType : IRegistrySetValueError;
            public record Win32Error(int Win32ErrorCode) : IRegistrySetValueError;
        }

        public MorphicResult<MorphicUnit, IRegistrySetValueError> SetValue<T>(string? valueName, T valueData)
        {
            ReadOnlySpan<byte> readOnlyValueData;
            uint valueDataSize;
            Windows.Win32.System.Registry.REG_VALUE_TYPE valueType;

            if (typeof(T) == typeof(uint))
            {
                var dataSizeAsInt = Marshal.SizeOf<uint>();
                //
                var valueAsUInt = (uint)(object)valueData!;
                var valueAsBytes = BitConverter.GetBytes((uint)valueAsUInt);
                readOnlyValueData = new ReadOnlySpan<byte>(valueAsBytes);
                //
                valueDataSize = (uint)dataSizeAsInt;
                valueType = Windows.Win32.System.Registry.REG_VALUE_TYPE.REG_DWORD;
            }
            else if (typeof(T) == typeof(System.String))
            {
                var valueAsString = (valueData as System.String)!;
                var valueAsBytes = System.Text.Encoding.Unicode.GetBytes(valueAsString);
                readOnlyValueData = new ReadOnlySpan<byte>(valueAsBytes);
                //
                valueDataSize = (uint)((valueAsString.Length + 1 /* +1 for the null terminator */) * 2);
                valueType = Windows.Win32.System.Registry.REG_VALUE_TYPE.REG_SZ;
            }
            else
            {
                // unknown type
                return MorphicResult.ErrorResult<IRegistrySetValueError>(new IRegistrySetValueError.UnsupportedType());
            }
            //
            Windows.Win32.Foundation.WIN32_ERROR setValueErrorCode;
            unsafe
            {
                setValueErrorCode = Windows.Win32.PInvoke.RegSetValueEx(_handle, valueName, valueType, readOnlyValueData);
            }
            switch (setValueErrorCode)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    break;
                default:
                    // NOTE: in the future, we may want to consider returning a specific result (i.e. "could not open for write access" etc.)
                    return MorphicResult.ErrorResult<IRegistrySetValueError>(new IRegistrySetValueError.Win32Error(unchecked((int)setValueErrorCode)));
            }

            // setting the value was a success
            return MorphicResult.OkResult();
        }

        //

        public event RegistryKeyChangedEventHandler RegistryKeyChangedEvent
        {
            add
            {
                var registerResult = this.RegisterForValueChangeNotification(value);
                Debug.Assert(registerResult.IsSuccess, "Could not register for registry value change notification");
            }
            remove
            {
                this.UnregisterFromValueChangeNotification(value);
            }
        }

        private MorphicResult<MorphicUnit, MorphicUnit> RegisterForValueChangeNotification(RegistryKeyChangedEventHandler eventHandler)
        {
            // if we are have already registered a registry key watch with the win32 API, simply add our event handler to our existing notification list
            lock (_registryKeyNotifyPoolEntriesLock)
            {
                if (_registryKeyNotifyPoolEntry is not null)
                {
                    _registryKeyNotifyPoolEntry.AddEventHandler(eventHandler);
                    return MorphicResult.OkResult();
                }
            }

            if (disposedValue == true)
            {
                return MorphicResult.ErrorResult();
            }

            var waitHandle = new ManualResetEvent(false);

            // NOTE: registration will auto-unregister after the wait handle is triggered once.  Registration will also auto-unregister when the RegistryKey is closed/disposed.
            var regNotifyChangeKeyValueResult = this.RegisterWaitHandleForValueChangeNotification(waitHandle);
            if (regNotifyChangeKeyValueResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }

            // add our registry key (and accompanying wait handle) to the notify pool
            // NOTE: this must be the only code which is allowed to add to the notify pool; if we change this behavior, we must re-evaluate and QA the corresponding change in lock strategy
            lock (s_registryKeyNotifyPoolLock)
            {
                var notifyInfo = new RegistryKeyNotificationInfo(this, waitHandle, eventHandler);
                s_registerKeyNotifyPool.Add(notifyInfo);

                if (s_registryKeyNotifyPoolThread is null)
                {
                    // start up our notify pool thread
                    s_registryKeyNotifyPoolThread = new Thread(RegistryKey.ListenForRegistryKeyChanges);
                    s_registryKeyNotifyPoolThread.IsBackground = true; // set up as a background thread (so that it shuts down automatically with our application, even if all the RegistryKeys weren't fully disposed)
                    s_registryKeyNotifyPoolThread.Start();
                }
                else
                {
                    // trigger our already-started notify pool thread--so that it's aware of the new entries
                    s_registryKeyNotifyPoolUpdatedEvent.Set();
                }
            }

            return MorphicResult.OkResult();
        }

        private void UnregisterFromValueChangeNotification(RegistryKeyChangedEventHandler eventHandler)
        {
            lock (_registryKeyNotifyPoolEntriesLock)
            {
                if (_registryKeyNotifyPoolEntry is not null)
                {
                    _registryKeyNotifyPoolEntry?.RemoveEventHandler(eventHandler);

                    if (_registryKeyNotifyPoolEntry?.GetEventHandlersCount() == 0)
                    {
                        // if we are subscribed to notifications, mark our notify pool's entry as "being disposed"
                        // NOTE: this will indicate to the registry key notify pool handler thread that this registry key should be removed from the pool once its wait handle is
                        //       triggered; its wait handle should be triggered when our handle is disposed
                        _registryKeyNotifyPoolEntry?.MarkForDisposal();
                        _registryKeyNotifyPoolEntry = null;

                        // NOTE: there is no way to de-register a notification from the system other than closing out its registration thread (if REG_NOTIFY_THREAD_AGNOSTIC was not supplied
                        //       as a parameter) or by closing the handle; therefore we will technically continue watching for the notification--but we will clear the entry out once the
                        //       notification expires

                        // NOTE: in an ideal scenario, we would close out the notification's wait handle to unregister; unfortunately the documentation for RegNotifyChangeKeyValue does not indicate 
                        //       that this will actually cancel the notification request; therefore we persist our notification request data (out of an abundance of caution) until it is definitely
                        //       no longer needed; this may have the side-effect of creating a large list of old notifications--so our caller should be careful not to register too many
                        //       notification requests (and should not repeatedly subscribe, unsubscribe and then resubscribe to notifications for the same open registry key.
                    }
                }
            }
        }

        private static void ListenForRegistryKeyChanges()
        {
            while (true)
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

                // create a list of handles to wait on (first the ones which we are watching...and then the one that triggers when the list is updated)
                var handlesToWaitOn = new WaitHandle[copyOfNotificationPool.Length + 1];
                for (var index = 0; index < copyOfNotificationPool.Length; index++)
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
                    //
                    // if the entry whose wait handle was triggered (by its handle being closed) has been marked for disposal, remove it from our main notification pool
                    if (notificationPoolEntry.MarkedForDisposal == true)
                    {
                        // remove this registry keys' entry from the notification pool
                        lock (s_registryKeyNotifyPoolLock)
                        {
                            for (var index = 0; index < s_registerKeyNotifyPool.Count; index += 1)
                            {
                                // NOTE: type WaitHandle is a class, so this comparison is a reference comparison
                                if (s_registerKeyNotifyPool[index].WaitHandle == notificationPoolEntry.WaitHandle)
                                {
                                    s_registerKeyNotifyPool.RemoveAt(index);
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            // otherwise, call the event handler on a thread pool thread
                            var eventHandlers = notificationPoolEntry.GetCopyOfEventHandlers();
                            foreach (var eventHandler in eventHandlers)
                            {
                                // NOTE: we launch the events in separate tasks; this may result in multiple events being called simultaneously for the same registry key notification
                                //       event (and in multiple threads); the caller should queue these using a concurrent task scheduler (e.g. MorphicSequentialTaskScheduler) or dispatch them
                                //       to their main/UI thread if the events need to be handled sequentially or one at a time
                                Task.Run(() =>
                                {
                                    eventHandler(notificationPoolEntry.RegistryKey, EventArgs.Empty);
                                });
                            }
                        }
                        finally
                        {
                            // if the entry hasn't been marked for disposal, re-register it for notifications
                            // NOTE: normally, notificationPoolEntry.MarkedForDisposal would always be false here; however some time may have lapsed while we were calling the
                            //       event handlers, and they may have themselves unsubscribed from the event, so we check here one more time
                            if (notificationPoolEntry.MarkedForDisposal == false)
                            {
                                // NOTE: registration will auto-unregister after the wait handle is triggered once.  Registration will also auto-unregister when the RegistryKey is closed/disposed.
                                var regNotifyChangeKeyValueResult = notificationPoolEntry.RegistryKey.RegisterWaitHandleForValueChangeNotification(notificationPoolEntry.WaitHandle);
                                if (regNotifyChangeKeyValueResult.IsError == true)
                                {
                                    switch (regNotifyChangeKeyValueResult.Error!)
                                    {
                                        case IRegisterWaitHandleForValueChangeNotificationError.ObjectDisposed:
                                            // if the object has been disposed but the pool entry was not already marked for disposal, mark it for disposal now
                                            notificationPoolEntry.MarkForDisposal();
                                            break;
                                        case IRegisterWaitHandleForValueChangeNotificationError.Win32Error(_):
                                        default:
                                            break;
                                    }

                                    // NOTE: we may want to consider logging this error, for in-field diagnostics of notification failures
                                    Debug.Assert(false, "Error: could not re-register notification pool entry to watch for changes to values for registry key");
                                }
                            }
                        }
                    }
                }
            }
        }

        public interface IRegisterWaitHandleForValueChangeNotificationError
        {
            public record ObjectDisposed : IRegisterWaitHandleForValueChangeNotificationError;
            public record Win32Error(int Win32ErrorCode) : IRegisterWaitHandleForValueChangeNotificationError;
        }

        private MorphicResult<MorphicUnit, IRegisterWaitHandleForValueChangeNotificationError> RegisterWaitHandleForValueChangeNotification(WaitHandle waitHandle)
        {
            // NOTE: registration will auto-unregister after the wait handle is triggered once.  Registration will also auto-unregister when the RegistryKey is closed/disposed.
            Windows.Win32.Foundation.WIN32_ERROR regNotifyErrorCode;
            try
            {
                // set up our notify filter; we are only watching for value changes (and not for changes to the names of subkeys, to attributes of the key, etc.)
                // NOTE: to change the notification filter, one must close and re-open a registry key; if we want to expand this watch in the future, we need to do it centrally 
                //       and then filter out the unwanted messages; alternatively we could make the watch properties a constructor overload
                // NOTE: REG_NOTIFY_CHANGE_LAST_SET will trigger on any changes to the key's values
                var notifyFilter = Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET;
                // NOTE: normally, RegNotifyChangeKeyValue must be called on a thread which is persistent (i.e. either the main thread or a persistent thread pool thread); however,
                //       by specifying a notify filter of REG_NOTIFY_THREAD_AGNOSTIC our notification thread pool will be notified when any original thread terminates so that it can
                //       re-register the notification (this functionality is available on Windows 8 or newer)
                notifyFilter |= Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_THREAD_AGNOSTIC;
                // NOTE: if _handle has been disposed, this will throw an ObjectDisposedException
                regNotifyErrorCode = Windows.Win32.PInvoke.RegNotifyChangeKeyValue(_handle, false, notifyFilter, waitHandle.SafeWaitHandle, true);
            }
            catch (ObjectDisposedException)
            {
                return MorphicResult.ErrorResult<IRegisterWaitHandleForValueChangeNotificationError>(new IRegisterWaitHandleForValueChangeNotificationError.ObjectDisposed());
            }
            //
            switch (regNotifyErrorCode)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    return MorphicResult.OkResult();
                default:
                    return MorphicResult.ErrorResult<IRegisterWaitHandleForValueChangeNotificationError>(new IRegisterWaitHandleForValueChangeNotificationError.Win32Error((int)regNotifyErrorCode));
            }
        }
    }
}