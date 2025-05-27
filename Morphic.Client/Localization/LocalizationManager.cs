// Copyright 2024-2025 Raising the Floor - US, Inc.
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
using System.Diagnostics;
using System.Windows;

namespace Morphic.Localization;

internal class LocalizationManager
{
    private static readonly string[] ISO_639_LANGUAGE_CODES = [
        "ar", // Arabic
        "es", // Spanish
        "fa", // Persian
        "gu", // Gujarati
        "hi", // Hindi
        "ko", // Korean
        "mr", // Marathi
        "ur", // Urdu
        "vi", // Vietnamese
        "zh", // Chinese
    ];

    private static readonly string[] CULTURE_NAMES = [
        "ar-SA", // Arabic
        "es-ES", // Spanish
        "fa-IR", // Persian
        "gu-IN", // Gujarati
        "hi-IN", // Hindi
        "ko-KR", // Korean
        "mr-IN", // Marathi
        "ur-IN", // Urdu (India)
        "ur-PK", // Urdu (Pakistan)
        "vi-VN", // Vietnamese
        "zh-CN", // Simplfiied Chinese
        "zh-TW", // Traditional Chinese
    ];

    private static string? GetDefaultCultureNameForIso639LanguageCode(string iso639LanguageCode)
    {
        switch (iso639LanguageCode)
        {
            case "ar":
                return "ar-SA";
            case "es":
                return "es-ES";
            case "fa":
                return "fa-IR";
            case "gu":
                return "gu-IN";
            case "hi":
                return "hi-IN";
            case "ko":
                return "ko-KR";
            case "mr":
                return "mr-IN";
            case "ur":
                //return "ur-IN";
                return "ur-PK";
            case "vi":
                return "vi-VN";
            case "zh":
                return "zh-CN";
                //return "zh-TW";
            default:
                return null;
        }
    }

    private static string? GetClosestAvailableCultureName(string cultureName)
    {
        switch (cultureName)
        {
            /* translated cultures */
            case "ar-SA":
            case "es-ES":
            case "fa-IR":
            case "gu-IN":
            case "hi-IN":
            case "ko-KR":
            case "mr-IN":
            case "ur-IN":
            case "ur-PK":
            case "vi-VN":
            case "zh-CN":
            case "zh-TW":
                return cultureName;
            //
            /* mapped cultures (not perfect, but trying to get to the closest option) */
            case "zh-HK": // Hong Kong -> Traditional Chinese
            case "zh-MO": // Macau -> Traditional Chinese
                return "zh-TW";
            case "zh-SG": // Singapore -> Simplified Chinese
                return "zh-CN";
            //
            /* unknown culture */
            default:
                return null;
        }
    }

    private static readonly string STRING_RESOURCES_BASE_URI_STRING = "pack://application:,,,/Morphic;component/Localization/StringResources.xaml";
    //
    private static readonly string STRING_RESOURCES_URI_STRING_PREFIX = "pack://application:,,,/Morphic;component/Localization/StringResources.";
    private static readonly string STRING_RESOURCES_URI_STRING_SUFFIX = ".xaml";
    //
    //
    private static readonly string UI_RESOURCES_BASE_URI_STRING = "pack://application:,,,/Morphic;component/Localization/UIResources.xaml";
    //
    private static readonly string UI_RESOURCES_URI_STRING_PREFIX = "pack://application:,,,/Morphic;component/Localization/UIResources.";
    private static readonly string UI_RESOURCES_URI_STRING_SUFFIX = ".xaml";

    public static string GetIso639LanguageCode(System.Globalization.CultureInfo uiCultureInfo)
    {
        var iso639LanguageCode = uiCultureInfo.TwoLetterISOLanguageName;

        return iso639LanguageCode;
    }

    // NOTE: This will return the culture name in the format ll-CC where ll is the language code and cc is the country code (e.g. es-ES, en-US)
    public static string GetCultureName(System.Globalization.CultureInfo uiCultureInfo)
    {
        var cultureName = uiCultureInfo.Name;

        return cultureName;
    }

    public static MorphicResult<MorphicUnit, MorphicUnit> SetUICulture(ResourceDictionary resourcesToModify, string iso639LanguageCode, string cultureName)
    {
        var lowercaseLanguageCode = iso639LanguageCode.ToLowerInvariant();

        var foundLanguageCode = false;
        foreach(var languageCode in LocalizationManager.ISO_639_LANGUAGE_CODES)
        {
            if (languageCode.ToLowerInvariant() == lowercaseLanguageCode)
            {
                foundLanguageCode = true;
                break;
            }
        }
        //
        if (foundLanguageCode == false)
        {
            // NOTE: this does not necessarily indicate an error; in the case of English, this also just means that we don't have a "translation" (as English is the "base" i.e. "not tranlsation" set of string/UI resources)
            return MorphicResult.ErrorResult();
        }

        string validatedCultureName = null!;
        //
        var foundCultureName = false;
        foreach(var currentCultureName in LocalizationManager.CULTURE_NAMES)
        {
            if (currentCultureName == cultureName)
            {
                foundCultureName = true;
                validatedCultureName = currentCultureName;
                break;
            }
        }
        //
        // if we did not find the culture name, try to find the closest culture name
        if (foundCultureName == false)
        {
            var closestAvailableCutlureName = LocalizationManager.GetClosestAvailableCultureName(cultureName);
            if (closestAvailableCutlureName is not null)
            {
                foundCultureName = true;
                validatedCultureName = closestAvailableCutlureName;
            }
        }
        //
        // if we still did not find an exact match or a closest-available culture name, get the default culture name for the language code
        if (foundCultureName == false)
        {
            var defaultCultureName = LocalizationManager.GetDefaultCultureNameForIso639LanguageCode(iso639LanguageCode);
            if (defaultCultureName is not null)
            {
                foundCultureName = true;
                validatedCultureName = defaultCultureName;
            }
            else
            {
                Debug.Assert(false, "Invalid code path: validated language codes should always return a closest or default culture name");
            }
        }
        //
        if (foundCultureName == false)
        {
            return MorphicResult.ErrorResult();
        }

        // load the UI resources dictionary for the supported UI culture
        var stringResourcesUri = new Uri(STRING_RESOURCES_URI_STRING_PREFIX + validatedCultureName + STRING_RESOURCES_URI_STRING_SUFFIX, UriKind.Absolute);
        ResourceDictionary stringResourceDictionary;
        try
        {
            stringResourceDictionary = new ResourceDictionary { Source = stringResourcesUri };
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
        //
        // determine the index, if present, of the localized string resource dictionary entry
        int? indexOfStringResourceDictionary = null;
        for (var index = 0; index < resourcesToModify.MergedDictionaries.Count; index += 1)
        {
            var sourceOriginalString = resourcesToModify.MergedDictionaries[index].Source.OriginalString;
            if (sourceOriginalString.Contains(LocalizationManager.STRING_RESOURCES_URI_STRING_PREFIX) == true && sourceOriginalString.Contains(LocalizationManager.STRING_RESOURCES_BASE_URI_STRING) == false)
            {
                indexOfStringResourceDictionary = index;
                break;
            }
        }
        //
        // add/replace the string resource localization dictionary; note that we replace the current localization dictionary but we always keep the base dictionary (in case certain localized values do not overwrite it)
        if (indexOfStringResourceDictionary is null)
        {
            resourcesToModify.MergedDictionaries.Add(stringResourceDictionary);
        }
        else
        {
            resourcesToModify.MergedDictionaries[indexOfStringResourceDictionary!.Value] = stringResourceDictionary;
        }

        // load the UI resources dictionary for the supported UI culture
        var uiResourcesUri = new Uri(UI_RESOURCES_URI_STRING_PREFIX + lowercaseLanguageCode + UI_RESOURCES_URI_STRING_SUFFIX, UriKind.Absolute);
        ResourceDictionary uiResourceDictionary;
        try
        {
            uiResourceDictionary = new ResourceDictionary { Source = uiResourcesUri };
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
        //
        // determine the index, if present, of the localized UI resource dictionary entry
        int? indexOfUIResourceDictionary = null;
        for (var index = 0; index < resourcesToModify.MergedDictionaries.Count; index += 1)
        {
            var sourceOriginalString = resourcesToModify.MergedDictionaries[index].Source.OriginalString;
            if (sourceOriginalString.Contains(LocalizationManager.UI_RESOURCES_URI_STRING_PREFIX) == true && sourceOriginalString.Contains(LocalizationManager.UI_RESOURCES_BASE_URI_STRING) == false)
            {
                indexOfUIResourceDictionary = index;
                break;
            }
        }
        //
        // add/replace the UI resource localization dictionary; note that we replace the current localization dictionary but we always keep the base dictionary (in case certain localized values do not overwrite it)
        if (indexOfUIResourceDictionary is null)
        {
            resourcesToModify.MergedDictionaries.Add(uiResourceDictionary);
        }
        else
        {
            resourcesToModify.MergedDictionaries[indexOfUIResourceDictionary!.Value] = uiResourceDictionary;
        }

        return MorphicResult.OkResult();
    }

}
