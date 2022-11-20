// Copyright 2022 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SystemSettingsDataModel = SystemSettings.DataModel;

namespace Morphic.WindowsNative.SystemSettings;

public class SettingItemProxy
{
    // NOTE: this enum should match SystemSettings.DataModel.SettingType; it may seem redundant, but we are providing it so that we can use the raw SystemSettings classes internally
    //       (WinRT interop) and expose a stable value externally
    public enum SettingType
    {
        Action,
        Boolean,
        Custom,
        DisplayString,
        LabeledString,
        List,
        Plugin,
        Range,
        SettingCollection,
        String
    }

    private SystemSettingsDataModel.ISettingItem _settingItem;

    internal SettingItemProxy(SystemSettingsDataModel.ISettingItem settingItem)
    {
        _settingItem = settingItem;
    }

    public string Id => _settingItem.Id;
    public bool IsApplicable => _settingItem.IsApplicable;
    public bool IsEnabled => _settingItem.IsEnabled;

    public record GetSettingTypeError : MorphicAssociatedValueEnum<GetSettingTypeError.Values>
    {
        // enum members
        public enum Values
        {
            ExceptionError/*(Exception ex)*/,
            UnknownType,
        }

        // functions to create member instances
        public static GetSettingTypeError ExceptionError(Exception ex) => new(Values.ExceptionError) { Exception = ex };
        public static GetSettingTypeError UnknownType => new(Values.UnknownType);

        // associated values
        public Exception? Exception { get; private set; }

        // verbatim required constructor implementation for MorphicAssociatedValueEnums
        private GetSettingTypeError(Values value) : base(value) { }
    }
    //
    public MorphicResult<SettingItemProxy.SettingType, GetSettingTypeError> GetSettingType()
    {
        // NOTE: we're unsure if ISettingItem.GetType() can throw an exception; we're catching exceptions anyway, out of an abundance of caution
        SystemSettingsDataModel.SettingType settingItemType;
        try
        {
            settingItemType = _settingItem.Type;
        }
        catch (Exception ex)
        {
            return MorphicResult.ErrorResult(GetSettingTypeError.ExceptionError(ex));
        }

        // convert the setting item's type to the publicly-exposed SettingItemProxy.SettingType enum
        SettingItemProxy.SettingType? result = SettingItemProxy.ConvertWinRtSettingTypeToPublicSettingType(settingItemType);
        if (result is null)
        {
            return MorphicResult.ErrorResult(GetSettingTypeError.UnknownType);
        }
        else
        {
            return MorphicResult.OkResult(result!.Value);
        }
    }

    private static SettingItemProxy.SettingType? ConvertWinRtSettingTypeToPublicSettingType(SystemSettingsDataModel.SettingType settingType)
    {
        switch (settingType)
        {
            case SystemSettingsDataModel.SettingType.Action:
                return SettingItemProxy.SettingType.Action;
            case SystemSettingsDataModel.SettingType.Boolean:
                return SettingItemProxy.SettingType.Boolean;
            case SystemSettingsDataModel.SettingType.Custom:
                return SettingItemProxy.SettingType.Custom;
            case SystemSettingsDataModel.SettingType.DisplayString:
                return SettingItemProxy.SettingType.DisplayString;
            case SystemSettingsDataModel.SettingType.LabeledString:
                return SettingItemProxy.SettingType.LabeledString;
            case SystemSettingsDataModel.SettingType.List:
                return SettingItemProxy.SettingType.List;
            case SystemSettingsDataModel.SettingType.Plugin:
                return SettingItemProxy.SettingType.Plugin;
            case SystemSettingsDataModel.SettingType.Range:
                return SettingItemProxy.SettingType.Range;
            case SystemSettingsDataModel.SettingType.SettingCollection:
                return SettingItemProxy.SettingType.SettingCollection;
            case SystemSettingsDataModel.SettingType.String:
                return SettingItemProxy.SettingType.String;
            default:
                return null;
        }
    }

    public record GetValueError : MorphicAssociatedValueEnum<GetValueError.Values>
    {
        // enum members
        public enum Values
        {
            ExceptionError/*(Exception ex)*/,
            Timeout,
            TypeMismatch,
        }

        // functions to create member instances
        public static GetValueError ExceptionError(Exception ex) => new(Values.ExceptionError) { Exception = ex };
        public static GetValueError Timeout => new(Values.Timeout);
        public static GetValueError TypeMismatch => new(Values.TypeMismatch);

        // associated values
        public Exception? Exception { get; private set; }

        // verbatim required constructor implementation for MorphicAssociatedValueEnums
        private GetValueError(Values value) : base(value) { }
    }
    //
    public async Task<MorphicResult<T?, GetValueError>> GetValueAsync<T>(TimeSpan? timeout = null) where T : struct
    {
        var result = await this.GetValueAsync<T>("Value", timeout);
        return result;
    }
    //
    public async Task<MorphicResult<T?, GetValueError>> GetValueAsync<T>(string name, TimeSpan? timeout = null) where T : struct
    {
        if (timeout is null)
        {
            timeout = TimeSpan.Zero;
        }
        if (timeout!.Value.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout may not exceed Int32.MaxValue milliseconds.");
        }
        var timeoutInMilliseconds = (int)timeout!.Value.TotalMilliseconds;

        Stopwatch timeoutStopwatch = Stopwatch.StartNew();

        object? valueAsObject;

        // NOTE: we check the value in an infinite loop in case the IsApplicable/IsEnabled settings are in flux; this allows us to handle edge case scenarios (which may never
        //       happen) where the setting is available and enabled, then we get the value, but then the setting is not available/enabled--so we're not totally confident in the
        //       value itself (i.e. we could be getting a null/false/default value instead); this is not a foolproof scenario, but it's our best attempt
        while (true)
        {
            // step 1: make sure that the setting is both applicable and enabled
            // NOTE: in our analysis of ISettingItem, these were always both true or always both false; we don't know the difference, so out of an abundance of caution we check both
            var remainingTimeout = (int)Math.Max(timeoutInMilliseconds - timeoutStopwatch.ElapsedMilliseconds, 0);
            var waitResult = await this.WaitForIsApplicableOrIsEnabledEventAsync(remainingTimeout);
            if (waitResult.IsError == true)
            {
                switch (waitResult.Error!.Value)
                {
                    case MorphicTimeoutError.Values.Timeout:
                        return MorphicResult.ErrorResult(GetValueError.Timeout);
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }

            // STEP 2: once the value state is (hopefully) valid, capture the value of the setting
            // NOTE: theoretically, IsEnabled could be set to false at any moment so we are not 100% guaranteed to get the correct value; the caller should ideally use an event
            //       handler strategy to also capture updates to the value (since the "Value" SettingsChanged handler seems to be executed every time IsApplicable/IsEnabled or Value
            //       is updated...and they seem to be updated together, in that order)
            // NOTE: we're unsure if ISettingItem.GetValue(string) can throw an exception; we're catching exceptions anyway, out of an abundance of caution
            try
            {
                valueAsObject = _settingItem.GetValue(name);
                if (valueAsObject == null)
                {
                    return MorphicResult.OkResult<T?>(null);
                }
            }
            catch (Exception ex)
            {
                return MorphicResult.ErrorResult(GetValueError.ExceptionError(ex));
            }

            // STEP 3: make sure that the setting is still applicable/enabled (see notes on STEP 1), as a sanity check that our value is still good
            bool isApplicable = _settingItem.IsApplicable;
            bool isEnabled = _settingItem.IsEnabled;
            if (isApplicable == true && isEnabled == true)
            {
                break;
            }
            else
            {
                // continue to the next iteration of the loop; try, try again
            }
        }

        // STEP 4: if the value is deemed as almost-certainly valid, go ahead and attempt to cast it to type T now
        var valueAsT = valueAsObject as T?;
        if (valueAsT is null)
        {
            // the type provided by the caller is not cast-compatible with the type returned by WinRT
            return MorphicResult.ErrorResult(GetValueError.TypeMismatch);
        }

        // STEP 5: return the value to the caller
        return MorphicResult.OkResult<T?>(valueAsT!.Value);
    }

    public record SetValueError : MorphicAssociatedValueEnum<SetValueError.Values>
    {
        // enum members
        public enum Values
        {
            ExceptionError/*(Exception ex)*/,
            //SettingNotApplicableOrNotEnabledAfterSet,
            Timeout,
            //TypeMismatch,
        }

        // functions to create member instances
        public static SetValueError ExceptionError(Exception ex) => new(Values.ExceptionError) { Exception = ex };
        //public static SetValueError SettingNotApplicableOrNotEnabledAfterSet => new(Values.SettingNotApplicableOrNotEnabledAfterSet);
        public static SetValueError Timeout => new(Values.Timeout);
        //public static SetValueError TypeMismatch => new(Values.TypeMismatch);

        // associated values
        public Exception? Exception { get; private set; }

        // verbatim required constructor implementation for MorphicAssociatedValueEnums
        private SetValueError(Values value) : base(value) { }
    }
    //
    public async Task<MorphicResult<MorphicUnit, SetValueError>> SetValueAsync<T>(T value, TimeSpan? timeout = null) where T : struct
    {
        var result = await this.SetValueAsync("Value", value, timeout);
        return result;
    }
    //
    // NOTE: callers should "get" the value after setting it, just to be sure that the value was set correctly.  [Note that technically another app could be setting the value
    //       in parallel, so if the value doesn't match then we can't really be sure that our "set" wasn't reversed by another app.]
    public async Task<MorphicResult<MorphicUnit, SetValueError>> SetValueAsync<T>(string name, T value, TimeSpan? timeout = null) where T : struct
    {
        if (timeout is null)
        {
            timeout = TimeSpan.Zero;
        }
        if (timeout!.Value.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout may not exceed Int32.MaxValue milliseconds.");
        }
        var timeoutInMilliseconds = (int)timeout!.Value.TotalMilliseconds;

        Stopwatch timeoutStopwatch = Stopwatch.StartNew();

        // step 1: make sure that the setting is both applicable and enabled
        // NOTE: in our analysis of ISettingItem, these were always both true or always both false; we don't know the difference, so out of an abundance of caution we check both
        var remainingTimeout = (int)Math.Max(timeoutInMilliseconds - timeoutStopwatch.ElapsedMilliseconds, 0);
        var waitResult = await this.WaitForIsApplicableOrIsEnabledEventAsync(remainingTimeout);
        if (waitResult.IsError == true)
        {
            switch (waitResult.Error!.Value)
            {
                case MorphicTimeoutError.Values.Timeout:
                    return MorphicResult.ErrorResult(SetValueError.Timeout);
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }

        // STEP 2: once the value state is (hopefully) valid, set the value of the setting
        // NOTE: theoretically, IsEnabled could be set to false at any moment so we are not 100% guaranteed that our "set" will actually set the value; the caller may want to
        //       capture the "ValueChanged" and/or "IsApplicable/IsEnabled" event handlers to react to the actual value change, to ensure that the value changed, etc.
        // NOTE: we're unsure if ISettingItem.SetValue(string, object) can throw an exception; we're catching exceptions anyway, out of an abundance of caution
        try
        {
            _settingItem.SetValue(name, value);
        }
        catch (Exception ex)
        {
            // NOTE: if the exception returned here is "type mismatch", we should return that as an error instead
            return MorphicResult.ErrorResult(SetValueError.ExceptionError(ex));
        }

        // NOTE: as we aren't confident that IsApplicable/IsEnabled won't be changed as a result of our SET operation, we have commented out this step (but can bring it back,
        //       if further analysis shows that it is necessary or useful)
        //// STEP 3: make sure that the setting is still applicable/enabled (see notes on STEP 1), as a sanity check that our value was set correctly
        //bool isApplicable = _settingItem.IsApplicable;
        //bool isEnabled = _settingItem.IsEnabled;
        //if (isApplicable == true && isEnabled == true)
        //{
        //    // continue; all is good
        //}
        //else
        //{
        //    return MorphicResult.ErrorResult(SetValueError.SettingNotApplicableOrNotEnabledAfterSet);
        //}

        // STEP 4: return success
        return MorphicResult.OkResult();
    }

    private async Task<MorphicResult<MorphicUnit, MorphicTimeoutError>> WaitForIsApplicableOrIsEnabledEventAsync(int timeoutInMilliseconds)
    {
        var isApplicableOrIsEnabledChangedWaitHandle = new AutoResetEvent(false);
        var waitForApplicableOrEnabledChangedHandler = new Windows.Foundation.TypedEventHandler<object, string>((object sender, string args) =>
        {
            switch (args)
            {
                case "IsApplicable":
                case "IsEnabled":
                    isApplicableOrIsEnabledChangedWaitHandle.Set();
                    break;
                default:
                    break;
            }
        });
        var isWatchingForIsApplicableOrIsEnabledChangedEvent = false;

        try
        {
            Stopwatch timeoutStopwatch = Stopwatch.StartNew();
            // NOTE: in our analysis of ISettingItem, these were always both true or always both false; we don't know the difference, so out of an abundance of caution we check both
            // NOTE: we use an infinite loop so that we can wait multiple times (if, for instance, we get events for one but not both states); it will terminate upon TIMEOUT in the 
            //       worst-case scenario
            while (true)
            {
                var isApplicable = _settingItem!.IsApplicable;
                var isEnabled = _settingItem!.IsEnabled;

                if (isApplicable == true && isEnabled == true)
                {
                    break;
                }

                // NOTE: if we reach this point, we need to wait for the event to change
                var remainingTimeout = (int)Math.Max(timeoutInMilliseconds - timeoutStopwatch.ElapsedMilliseconds, 0);
                if (remainingTimeout < 0)
                {
                    return MorphicResult.ErrorResult(MorphicTimeoutError.Timeout);
                }

                // if we're not watching for the .IsApplicable or .IsEnabled property to change, wire up an event handler now
                if (isWatchingForIsApplicableOrIsEnabledChangedEvent == false)
                {
                    _settingItem.SettingChanged += waitForApplicableOrEnabledChangedHandler;
                    isWatchingForIsApplicableOrIsEnabledChangedEvent = true;
                }
                //
                // wait for the IsApplicable/IsEnabled event handler to fire (or for our timeout to expire, whichever comes first)
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var waitHandleRegistration = ThreadPool.RegisterWaitForSingleObject(isApplicableOrIsEnabledChangedWaitHandle, delegate { taskCompletionSource.SetResult(true); }, null, remainingTimeout, true);
                var waitTask = taskCompletionSource.Task;
                await waitTask.ContinueWith((_) => waitHandleRegistration.Unregister(null));
            };

            return MorphicResult.OkResult();
        }
        finally
        {
            if (isWatchingForIsApplicableOrIsEnabledChangedEvent == true)
            {
                _settingItem.SettingChanged -= waitForApplicableOrEnabledChangedHandler;
                isWatchingForIsApplicableOrIsEnabledChangedEvent = false;
            }
        }
    }

    private bool _settingsChangedEventHandlerIsSubscribed = false;
    //
    private EventHandler? _isApplicableChanged = null;
    private EventHandler? _isEnabledChanged = null;
    private EventHandler? _valueChanged = null;
    //
    private object _eventsLock = new object();

    public event EventHandler ValueChanged
    {
        add
        {
            lock (_eventsLock)
            {
                if (_settingsChangedEventHandlerIsSubscribed == false)
                {
                    _settingItem.SettingChanged += _settingItem_SettingChanged;
                    _settingsChangedEventHandlerIsSubscribed = true;
                }
                _valueChanged += value;
            }
        }
        remove
        {
            lock (_eventsLock)
            {
                _valueChanged -= value;
            }

            this.UnsubscribeSettingChangedEventHandlerIfEventsAreEmpty();
        }
    }

    private void UnsubscribeSettingChangedEventHandlerIfEventsAreEmpty()
    {
        lock (_eventsLock)
        {
            var isApplicableChangedIsEmpty = (_isApplicableChanged is null || _isApplicableChanged!.GetInvocationList().Length == 0);
            var isEnabledChangedIsEmpty = (_isEnabledChanged is null || _isEnabledChanged!.GetInvocationList().Length == 0);
            var valueChangedIsEmpty = (_valueChanged is null || _valueChanged!.GetInvocationList().Length == 0);

            if (isApplicableChangedIsEmpty == true && isEnabledChangedIsEmpty == true && valueChangedIsEmpty == true)
            {
                _settingItem.SettingChanged -= _settingItem_SettingChanged;
                _settingsChangedEventHandlerIsSubscribed = false;
            }
        }
    }

    private void _settingItem_SettingChanged(object sender, string args)
    {
        // NOTE: we raise each event subscription individually in case any of them throw exceptions; we do so asynchronously, so users should queue their events when raised
        //        or dispatch to the main thread
        switch (args)
        {
            case "IsApplicable":
                {
                    var invocationList = _isApplicableChanged?.GetInvocationList();
                    if (invocationList is not null)
                    {
                        foreach (EventHandler element in invocationList!)
                        {
                            Task.Run(() => {
                                element.Invoke(sender, EventArgs.Empty);
                            });
                        }
                    }
                    //Task.Run(() => {
                    //    _isApplicableChanged?.Invoke(sender, EventArgs.Empty);
                    //});
                }
                break;
            case "IsEnabled":
                {
                    var invocationList = _isEnabledChanged?.GetInvocationList();
                    if (invocationList is not null)
                    {
                        foreach (EventHandler element in invocationList!)
                        {
                            Task.Run(() => {
                                element.Invoke(sender, EventArgs.Empty);
                            });
                        }
                    }
                    //Task.Run(() => {
                    //    _isApplicableChanged?.Invoke(sender, EventArgs.Empty);
                    //});
                }
                break;
            case "Value":
                {
                    var invocationList = _valueChanged?.GetInvocationList();
                    if (invocationList is not null)
                    {
                        foreach (EventHandler element in invocationList!)
                        {
                            Task.Run(() => {
                                element.Invoke(sender, EventArgs.Empty);
                            });
                        }
                    }
                    //Task.Run(() => {
                    //    _isApplicableChanged?.Invoke(sender, EventArgs.Empty);
                    //});
                }
                break;
            default:
                break;
        }
    }
}
