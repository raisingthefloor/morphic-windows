// Copyright 2022 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SystemSettingsDataModel = SystemSettings.DataModel;

namespace Morphic.WindowsNative.SystemSettings;

public static class SettingsDatabaseProxy
{
    private static SystemSettingsDataModel.SettingsDatabase? _settingsDatabase;

    private static ConcurrentDictionary<string, SettingItemProxy> _settingItemProxies = new();
    private static object _settingItemProxiesLock = new();

    public record GetSettingItemError : MorphicAssociatedValueEnum<GetSettingItemError.Values>
    {
        // enum members
        public enum Values
        {
            CouldNotInstantiateSettingsDatabase/*(Exception ex)*/,
            ExceptionError/*(Exception ex)*/,
            SettingNotFound,
        }

        // functions to create member instances
        public static GetSettingItemError CouldNotInstantiateSettingsDatabase(Exception ex) => new(Values.CouldNotInstantiateSettingsDatabase) {  Exception = ex };
        public static GetSettingItemError ExceptionError(Exception ex) => new(Values.ExceptionError) { Exception = ex };
        public static GetSettingItemError SettingNotFound => new(Values.SettingNotFound);

        // associated values
        public Exception? Exception { get; private set; }

        // verbatim required constructor implementation for MorphicAssociatedValueEnums
        private GetSettingItemError(Values value) : base(value) { }
    }
    //
    public static MorphicResult<SettingItemProxy, GetSettingItemError> GetSettingItem(string id)
    {
        // STEP 1: make sure our shared SettingsDatabase object is populated
        //
        // NOTE: we don't know if SettingsDatabase.GetSetting will throw an exception or not, so we "catch" out of an abundance of caution
        try
        {
            if (_settingsDatabase == null)
            {
                // attempt to create the system settings database
                _settingsDatabase = new SystemSettingsDataModel.SettingsDatabase();
            }
        }
        catch (Exception ex)
        {
            return MorphicResult.ErrorResult(GetSettingItemError.CouldNotInstantiateSettingsDatabase(ex));
        }

        // STEP 2: if we have already created a proxy for this setting item, get it now; otherwise try to instantiate it
        //
        SettingItemProxy? settingItemProxy;
        var settingItemProxyAlreadyCached = _settingItemProxies.TryGetValue(id, out settingItemProxy);
        if (settingItemProxyAlreadyCached == true)
        {
            return MorphicResult.OkResult(settingItemProxy!);
        }
        else
        {
            lock(_settingItemProxiesLock)
            {
                // NOTE: we don't know if SettingsDatabase.GetSetting will throw an exception or not, so we "catch" out of an abundance of caution
                try
                {
                    var settingItem = _settingsDatabase!.GetSetting(id);
                    if (settingItem == null)
                    {
                        // if SettingsDatabase.GetSetting returns null, this appears to mean that the setting is not found
                        return MorphicResult.ErrorResult(GetSettingItemError.SettingNotFound);
                    }
                    else
                    {
                        // wrap a proxy around this setting item
                        settingItemProxy = new SettingItemProxy(settingItem!);

                        // save the setting item proxy to our collection
                        // NOTE: we use "GetOrAdd" here so that we replace our current settingItemProxy (and set the old one to null) if another one already existed; the latter 
                        //       condition could happen if multiple threads are getting the setting item in parallel
                        settingItemProxy = _settingItemProxies.GetOrAdd(id, settingItemProxy!);

                        // return the settingItemProxy to our caller now
                        return MorphicResult.OkResult(settingItemProxy);
                    }
                }
                catch (Exception ex)
                {
                    return MorphicResult.ErrorResult(GetSettingItemError.ExceptionError(ex));
                }
            }
        }
    }

    public static SettingItemProxy? GetSettingItemOrNull(string id)
    {
        var getSettingItemResult = SettingsDatabaseProxy.GetSettingItem(id);
        if (getSettingItemResult.IsError == true)
        {
            switch (getSettingItemResult.Error!.Value)
            {
                case SettingsDatabaseProxy.GetSettingItemError.Values.CouldNotInstantiateSettingsDatabase:
                    return null;
                case SettingsDatabaseProxy.GetSettingItemError.Values.SettingNotFound:
                    return null;
                case SettingsDatabaseProxy.GetSettingItemError.Values.ExceptionError:
                    return null;
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var settingItem = getSettingItemResult.Value!;

        return settingItem;
    }
}
