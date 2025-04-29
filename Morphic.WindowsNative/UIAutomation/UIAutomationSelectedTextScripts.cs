// Copyright 2022-2025 Raising the Floor - US, Inc.
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UIAutomationClient;

namespace Morphic.WindowsNative.UIAutomation;

public class UIAutomationSelectedTextScripts
{
    public interface ICaptureSelectedTextError
    {
        // functions to create member instances
        public record ComInterfaceInstantiationFailed : ICaptureSelectedTextError;
        public record TextRangeIsNull : ICaptureSelectedTextError;
        public record Win32Error(int Win32ErrorCode) : ICaptureSelectedTextError;
    }
    //
    // NOTE: this function returns an OkResult with a null string? value if the focused window doesn't have a focused element that supports selecting text
    public static MorphicResult<string?, ICaptureSelectedTextError> GetSelectedText()
    {
        // instantiate our UIAutomation class (via COM interop)
        // NOTE: we use the CUIAutomation8 object (which implements IUIAutomation2 through IUIAutomation6); this new interface was introduced in Windows 8 and allows for timeouts, etc.; the latest features of interface IUIAutomation6 are only available in Windows 10 v1809 and newer
        CUIAutomation8? comUIAutomation = null;
        try
        {
            comUIAutomation = new CUIAutomation8();
        }
        catch
        {
            // if our COM instantiation failed (e.g. FileNotFound exception), return that error condition to the caller
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
        }
        // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
        if (comUIAutomation is null)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
        }
        try
        {
            // NOTE: at this point, we know that we have a COM-instantiated CUIAutomation8 instance (which should be compatible with IUIAutomation2 through IUIAutomation6 interfaces on Windows 10 v1809+ and newer)

            var getSelectedTextUsingUIAutomationResult = UIAutomationSelectedTextScripts.GetSelectedTextUsingUIAutomation(comUIAutomation!);
            if (getSelectedTextUsingUIAutomationResult.IsError == true)
            {
                return MorphicResult.ErrorResult(getSelectedTextUsingUIAutomationResult.Error!);
            }
            var selectedText = getSelectedTextUsingUIAutomationResult.Value;

            return MorphicResult.OkResult(selectedText);
        }
        finally
        {
            // manually release our COM UIAutomation object and set its manual wrapper reference to null
            if (comUIAutomation is not null)
            {
                Marshal.ReleaseComObject(comUIAutomation);
                comUIAutomation = null;
            }
        }
    }

    private static MorphicResult<string?, ICaptureSelectedTextError> GetSelectedTextUsingUIAutomation(CUIAutomation8 comUIAutomation)
    {
        // when we obtain our UI element, we need to make sure it supports the text pattern; we specify this via a cache request
        IUIAutomationCacheRequest? comUIAutomationCacheRequest = null;
        try
        {
            comUIAutomationCacheRequest = comUIAutomation!.CreateCacheRequest();
        }
        catch (COMException ex)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
        }
        // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
        if (comUIAutomationCacheRequest is null)
        {
            Debug.Assert(false, "CUIAutomation8.CreateCacheRequest should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
        }

        try
        {
            // NOTE: at this point, we know that we have a COM-instantiated IUIAutomationCacheRequest instance (which we can now populate with the cache request information we need to supply when we search for our focused element)

            // identify the Text control pattern in our cache request
            // NOTE: although UIA_TextPattern2Id was introduced in Windows 8, we don't need any additional capabilities from this newer pattern; to avoid any (unlikely) app compatibility issues, we're using the earlier (non-extended) interface
            comUIAutomationCacheRequest!.AddPattern(UIA_PatternIds.UIA_TextPatternId);
            //
            // make sure that the Text control pattern is available for the hWnd's automation element (in our cache request)
            // NOTE: although UIA_IsTextPattern2AvailablePropertyId was introduced in Windows 8, we don't need any additional capabilities from this newer pattern; to avoid any (unlikely) app compatibility issues, we're using the earlier (non-extended) interface
            comUIAutomationCacheRequest!.AddProperty(UIA_PropertyIds.UIA_IsTextPatternAvailablePropertyId);

            // using our cache request properties (i.e. text element), find the focused element; this should be the actual element which we query to retrieve the selected text
            IUIAutomationElement? comUIAutomationElement = null;
            try
            {
                comUIAutomationElement = comUIAutomation!.GetFocusedElementBuildCache(comUIAutomationCacheRequest!);
            }
            catch (COMException ex)
            {
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
            }
            // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
            // NOTE: in our testing, we once got back "null", but were unable to determine the cause; it _might_ be because we weren't focused on a window which has a document control; we may need to explore this scenario further, add warnings/errors/fallbacks etc.
            if (comUIAutomationElement is null)
            {
                Debug.Assert(false, "CUIAutomationElement.GetFocusedElementBuildCache should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
            }
            //
            try
            {
                // NOTE: at this point, we should have the handle of an element which we can query for selected text

                var queryElementForSelectedTextResult = UIAutomationSelectedTextScripts.QueryElementForSelectedText(comUIAutomationElement!);
                if (queryElementForSelectedTextResult.IsError == true)
                {
                    return MorphicResult.ErrorResult(queryElementForSelectedTextResult.Error!);
                }
                string? selectedText = queryElementForSelectedTextResult.Value;

                return MorphicResult.OkResult(selectedText);
            }
            finally
            {
                if (comUIAutomationElement is not null)
                {
                    Marshal.ReleaseComObject(comUIAutomationElement);
                    comUIAutomationElement = null;
                }
            }
        }
        finally
        {
            if (comUIAutomationCacheRequest is not null)
            {
                Marshal.ReleaseComObject(comUIAutomationCacheRequest);
                comUIAutomationCacheRequest = null;
            }
        }
    }

    private static MorphicResult<string?, ICaptureSelectedTextError> QueryElementForSelectedText(IUIAutomationElement comUIAutomationElement)
    {
        // NOTE: at this point, we should have the handle of an element which we can query for selected text

        // determine if the child element supports the text pattern (so that we can query for selected text)
        var isTextPatternAvailable = (bool)comUIAutomationElement.GetCachedPropertyValue(UIA_PropertyIds.UIA_IsTextPatternAvailablePropertyId);
        if (isTextPatternAvailable == false)
        {
            // if the focused element doesn't support the text pattern, return null
            return MorphicResult.OkResult<string?>(null);
        }

        // we now know that the control supports text selected; let's retrieve the selected text

        // create an IUIAutomationTextPattern-compatible object (as an IUnknown) for the element using the text pattern
        IUIAutomationTextPattern? comUIAutomationTextPattern = null;
        try
        {
            comUIAutomationTextPattern = (IUIAutomationTextPattern)comUIAutomationElement.GetCachedPattern(UIA_PatternIds.UIA_TextPatternId);
        }
        catch (COMException ex)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
        }
        // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
        if (comUIAutomationTextPattern is null)
        {
            Debug.Assert(false, "IUIAutomationElement.GetCachedPattern should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
        }
        //
        try
        {
            // NOTE: at this point, we have a COM-instantiated IUIAutomationTextPattern-compatible object which we can use to capture text from the current selection
            IUIAutomationTextRangeArray? comUIAutomationTextRangeArray = null;
            try
            {
                comUIAutomationTextRangeArray = comUIAutomationTextPattern!.GetSelection();
            }
            catch (COMException ex)
            {
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
            }
            // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
            if (comUIAutomationTextRangeArray is null)
            {
                Debug.Assert(false, "IUIAutomationTextPattern.GetSelection should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
            }
            //
            try
            {
                // NOTE: at this point, we have a COM-instantiated IUIAutomationTextRangeArray (text range array); now let's extract each text range from this array, convert it to a string, and then concatenate all those ranges together into a string to return to our caller

                var extractStringFromTextRangeArrayResult = UIAutomationSelectedTextScripts.ExtractStringFromTextRangeArray(comUIAutomationTextRangeArray);
                if (extractStringFromTextRangeArrayResult.IsError == true)
                {
                    return MorphicResult.ErrorResult(extractStringFromTextRangeArrayResult.Error!);
                }
                string? selectedText = extractStringFromTextRangeArrayResult.Value;

                // return the selected text to the caller
                return MorphicResult.OkResult(selectedText);
            }
            finally
            {
                if (comUIAutomationTextRangeArray is not null)
                {
                    Marshal.ReleaseComObject(comUIAutomationTextRangeArray);
                    comUIAutomationTextRangeArray = null;
                }
            }
        }
        finally
        {
            if (comUIAutomationTextPattern is not null)
            {
                Marshal.ReleaseComObject(comUIAutomationTextPattern);
                comUIAutomationTextPattern = null;
            }
        }
    }

    private static MorphicResult<string?, ICaptureSelectedTextError> ExtractStringFromTextRangeArray(IUIAutomationTextRangeArray comUIAutomationTextRangeArray)
    {
        // NOTE: at this point, we have a COM-instantiated IUIAutomationTextRangeArray (text range array); now let's extract each text range from this array, convert it to a string, and then concatenate all those ranges together into a string to return to our caller

        StringBuilder selectedTextBuilder = new();
        var numberOfTextRanges = comUIAutomationTextRangeArray.Length;
        if (numberOfTextRanges > 0)
        {
            for (var iTextRange = 0; iTextRange < numberOfTextRanges; iTextRange += 1)
            {
                IUIAutomationTextRange? comUIAutomationTextRange = null;
                try
                {
                    comUIAutomationTextRange = comUIAutomationTextRangeArray!.GetElement(iTextRange);
                }
                catch (COMException ex)
                {
                    return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
                }
                // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
                if (comUIAutomationTextRange is null)
                {
                    Debug.Assert(false, "IUIAutomationTextRangeArray.GetElement should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
                    return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
                }
                //
                try
                {
                    var textRangeAsString = comUIAutomationTextRange!.GetText(-1);
                    if (textRangeAsString is null)
                    {
                        Debug.Assert(false, "IUIAutomationTextRange.GetText should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
                        return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.TextRangeIsNull());
                    }
                    selectedTextBuilder.Append(textRangeAsString);
                }
                finally
                {
                    if (comUIAutomationTextRange is not null)
                    {
                        Marshal.ReleaseComObject(comUIAutomationTextRange);
                        comUIAutomationTextRange = null;
                    }
                }
            }

            // return the selected text to the caller
            return MorphicResult.OkResult<string?>(selectedTextBuilder.ToString());
        }
        else
        {
            return MorphicResult.OkResult<string?>(null);
        }
    }
}
