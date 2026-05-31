// Copyright 2022-2026 Raising the Floor - US, Inc.
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
using System.Text;

namespace Morphic.WindowsNative.UIAutomation;

public class UIAutomationSelectedTextScripts
{
    // Optional diagnostic sink. This library is the lower layer and cannot reference an app-side
    // logger directly, so a consumer MAY inject an Action<string> here to trace, during a failed
    // capture, exactly which step returned "no selectable text". Currently UNWIRED, so the 
    // LogDiagnostic trace points below are inert and write nothing.
    public static System.Action<string>? DiagnosticLog;

    private static void LogDiagnostic(string message)
    {
        UIAutomationSelectedTextScripts.DiagnosticLog?.Invoke("ReadAloud/UIA: " + message);
    }

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
        // instantiate our UIAutomation root object
        // NOTE: CsWin32 surfaces CUIAutomation8 as a [ComImport] coclass, so 'new' triggers CoCreateInstance; we then cast (QueryInterface) to IUIAutomation, the base interface that exposes the focused-element and cache-request APIs we need. The CUIAutomation8 coclass also implements IUIAutomation2 through IUIAutomation6 (introduced in Windows 8, with IUIAutomation6 features requiring Windows 10 v1809+), but we don't require any of those newer capabilities here.
        Windows.Win32.UI.Accessibility.IUIAutomation? comUIAutomation = null;
        try
        {
            comUIAutomation = (Windows.Win32.UI.Accessibility.IUIAutomation)new Windows.Win32.UI.Accessibility.CUIAutomation8();
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
            // NOTE: at this point, we know that we have a COM-instantiated IUIAutomation instance

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
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomation);
                comUIAutomation = null;
            }
        }
    }

    //
    // NOTE: this is the window-targeted counterpart to GetSelectedText; instead of querying the system-wide focused element, it resolves the focused control inside a specific top-level window and captures from there
    // NOTE: this is what Read Selected uses: when the user clicks Play, the MorphicBar takes the foreground, so a system-wide focused-element query would return the bar (which has no selectable text); by targeting the user's previous foreground window (see LastForegroundWindowTracker), we capture the text they actually had selected
    // NOTE: this function returns an OkResult with a null string? value if the target window's focused control doesn't support selecting text
    // NOTE: when readFocusedControlTextWhenNoSelection is true and no selection is found anywhere in the window, this reads the full text of the control that holds keyboard focus instead (option #1 "read all text" fallback); it defaults to false so the no-selection case stays silent unless a caller opts in
    public static MorphicResult<string?, ICaptureSelectedTextError> GetSelectedTextForWindow(System.IntPtr windowHandle, bool readFocusedControlTextWhenNoSelection = false)
    {
        // instantiate our UIAutomation root object
        // NOTE: CsWin32 surfaces CUIAutomation8 as a [ComImport] coclass, so 'new' triggers CoCreateInstance; we then cast (QueryInterface) to IUIAutomation, the base interface that exposes the element-from-handle and cache-request APIs we need
        Windows.Win32.UI.Accessibility.IUIAutomation? comUIAutomation = null;
        try
        {
            comUIAutomation = (Windows.Win32.UI.Accessibility.IUIAutomation)new Windows.Win32.UI.Accessibility.CUIAutomation8();
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
            // NOTE: at this point, we know that we have a COM-instantiated IUIAutomation instance

            var getSelectedTextForWindowUsingUIAutomationResult = UIAutomationSelectedTextScripts.GetSelectedTextForWindowUsingUIAutomation(comUIAutomation!, windowHandle, readFocusedControlTextWhenNoSelection);
            if (getSelectedTextForWindowUsingUIAutomationResult.IsError == true)
            {
                return MorphicResult.ErrorResult(getSelectedTextForWindowUsingUIAutomationResult.Error!);
            }
            var selectedText = getSelectedTextForWindowUsingUIAutomationResult.Value;

            return MorphicResult.OkResult(selectedText);
        }
        finally
        {
            // manually release our COM UIAutomation object and set its manual wrapper reference to null
            if (comUIAutomation is not null)
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomation);
                comUIAutomation = null;
            }
        }
    }

    private static unsafe MorphicResult<string?, ICaptureSelectedTextError> GetSelectedTextForWindowUsingUIAutomation(Windows.Win32.UI.Accessibility.IUIAutomation comUIAutomation, System.IntPtr windowHandle, bool readFocusedControlTextWhenNoSelection)
    {
        // when we obtain our UI element, we need to make sure it supports the text pattern; we specify this via a cache request
        Windows.Win32.UI.Accessibility.IUIAutomationCacheRequest? comUIAutomationCacheRequest = null;
        try
        {
            comUIAutomationCacheRequest = comUIAutomation!.CreateCacheRequest();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
        }
        // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
        if (comUIAutomationCacheRequest is null)
        {
            System.Diagnostics.Debug.Assert(false, "CUIAutomation8.CreateCacheRequest should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
        }

        try
        {
            // NOTE: at this point, we know that we have a COM-instantiated IUIAutomationCacheRequest instance (which we can now populate with the cache request information we need to supply when we search for our focused element)

            // identify the Text control pattern in our cache request
            comUIAutomationCacheRequest!.AddPattern(Windows.Win32.UI.Accessibility.UIA_PATTERN_ID.UIA_TextPatternId);
            //
            // make sure that the Text control pattern is available for the element (in our cache request)
            comUIAutomationCacheRequest!.AddProperty(Windows.Win32.UI.Accessibility.UIA_PROPERTY_ID.UIA_IsTextPatternAvailablePropertyId);

            // resolve the focused control within the target window
            // NOTE: when a top-level window loses the foreground (e.g. because the user clicked the MorphicBar), the window's per-thread keyboard focus is retained, so GetGUIThreadInfo still reports the control the user was editing; we query UIA against that specific control's handle
            var targetWindowHandle = (Windows.Win32.Foundation.HWND)windowHandle;
            var focusedControlHandle = UIAutomationSelectedTextScripts.GetFocusedControlHandleForWindow(targetWindowHandle);

            // using our cache request properties (i.e. text element), find the element for the focused control's handle; this should be the actual element which we query to retrieve the selected text
            Windows.Win32.UI.Accessibility.IUIAutomationElement? comUIAutomationElement = null;
            try
            {
                comUIAutomationElement = comUIAutomation!.ElementFromHandleBuildCache(focusedControlHandle, comUIAutomationCacheRequest!);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
            }
            // NOTE: unlike GetFocusedElementBuildCache, ElementFromHandleBuildCache can legitimately return null when the handle doesn't map to an automation element; we treat that as "no selectable text" rather than as an error condition
            if (comUIAutomationElement is null)
            {
                UIAutomationSelectedTextScripts.LogDiagnostic("GetSelectedTextForWindowUsingUIAutomation: ElementFromHandleBuildCache returned null; treating as no selectable text.");
                return MorphicResult.OkResult<string?>(null);
            }
            //
            try
            {
                // Tier 1: query the focused-control element (or, when no focused control could be resolved, the top-level element) directly for a selection. This is the fast path for classic windows whose focused edit control is reported by GetGUIThreadInfo.
                var tier1QueryResult = UIAutomationSelectedTextScripts.QueryElementForSelectedText(comUIAutomationElement!);
                if (tier1QueryResult.IsError == true)
                {
                    return MorphicResult.ErrorResult(tier1QueryResult.Error!);
                }
                if (string.IsNullOrWhiteSpace(tier1QueryResult.Value) == false)
                {
                    UIAutomationSelectedTextScripts.LogDiagnostic("GetSelectedTextForWindowUsingUIAutomation: Tier 1 (focused/top-level element) captured a non-empty selection.");
                    return MorphicResult.OkResult<string?>(tier1QueryResult.Value);
                }
            }
            finally
            {
                if (comUIAutomationElement is not null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomationElement);
                    comUIAutomationElement = null;
                }
            }

            // Tier 2: the focused-control / top-level element had no usable selection. This is the common case for host-framed apps (e.g. modern Notepad, whose foreground window is an ApplicationFrameWindow with no focusable text child) and for any app that drops keyboard focus when it is deactivated (which it is here, because clicking the MorphicBar took the foreground). A text control keeps its selection even when it is not focused, and UIA can cross the frame-host boundary that GetGUIThreadInfo cannot, so we search the target window's element subtree for a text-pattern element that currently has a non-empty selection.
            UIAutomationSelectedTextScripts.LogDiagnostic("GetSelectedTextForWindowUsingUIAutomation: Tier 1 found no selection; starting Tier 2 bounded search.");
            return UIAutomationSelectedTextScripts.FindSelectedTextWithBoundedSearch(comUIAutomation!, targetWindowHandle, focusedControlHandle, comUIAutomationCacheRequest!, readFocusedControlTextWhenNoSelection);
        }
        finally
        {
            if (comUIAutomationCacheRequest is not null)
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomationCacheRequest);
                comUIAutomationCacheRequest = null;
            }
        }
    }

    // Resolve the control that currently holds keyboard focus within the supplied top-level window.
    // NOTE: a window's per-thread focus survives the loss of the system foreground, which is exactly why we can still find the user's editing control after our own UI has taken the foreground. If we can't resolve a focused control, we fall back to the window handle itself (which UIA can still map to the top-level element).
    // NOTE: this is internal (not private) so the SelectionReader orchestrator's clipboard-free and clipboard-based fallback tiers (Speech\SelectionReader.cs) can target the same focused control that the UI Automation tier targets.
    internal static unsafe Windows.Win32.Foundation.HWND GetFocusedControlHandleForWindow(Windows.Win32.Foundation.HWND windowHandle)
    {
        // identify the thread that owns the target window; its per-thread GUI info tells us which control currently holds keyboard focus
        var windowThreadId = Windows.Win32.PInvoke.GetWindowThreadProcessId(windowHandle);
        if (windowThreadId == 0)
        {
            // we couldn't resolve the owning thread (e.g. the window handle is no longer valid); fall back to the window handle itself
            UIAutomationSelectedTextScripts.LogDiagnostic("GetFocusedControlHandleForWindow: GetWindowThreadProcessId returned 0; falling back to the top-level window handle.");
            return windowHandle;
        }

        var guiThreadInfo = new Windows.Win32.UI.WindowsAndMessaging.GUITHREADINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.UI.WindowsAndMessaging.GUITHREADINFO>(),
        };
        bool getGuiThreadInfoSucceeded = Windows.Win32.PInvoke.GetGUIThreadInfo(windowThreadId, ref guiThreadInfo);
        if (getGuiThreadInfoSucceeded == false)
        {
            // GetGUIThreadInfo failed; fall back to the window handle itself
            UIAutomationSelectedTextScripts.LogDiagnostic("GetFocusedControlHandleForWindow: GetGUIThreadInfo failed (lastWin32Error=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error() + "); falling back to the top-level window handle.");
            return windowHandle;
        }
        if (guiThreadInfo.hwndFocus == Windows.Win32.Foundation.HWND.Null)
        {
            // the thread reports no focused control (e.g. focus is on the window frame itself); fall back to the window handle itself
            UIAutomationSelectedTextScripts.LogDiagnostic("GetFocusedControlHandleForWindow: hwndFocus is Null; falling back to the top-level window handle 0x" + ((System.IntPtr)windowHandle).ToString("X") + ".");
            return windowHandle;
        }

        UIAutomationSelectedTextScripts.LogDiagnostic("GetFocusedControlHandleForWindow: resolved focused control handle = 0x" + ((System.IntPtr)guiThreadInfo.hwndFocus).ToString("X") + " (top-level was 0x" + ((System.IntPtr)windowHandle).ToString("X") + ").");
        return guiThreadInfo.hwndFocus;
    }

    // Tier 2 capture: search the target window's UI Automation subtree for a text-pattern element that currently holds a non-empty selection, performed as a BOUNDED, PRUNING breadth-first walk rather than a single full-subtree FindAllBuildCache.
    //
    // WHY a bounded walk: FindAllBuildCache(TreeScope_Subtree) forces the provider to materialize its ENTIRE accessibility subtree in one call. For a classic app that is cheap, but a Chromium-based browser builds its accessibility tree lazily and on demand, so forcing the whole subtree on a large page (e.g. a single-page HTML specification) can hang for many seconds, which is the deal-killer this redesign removes.
    //
    // WHY pruning keeps it fast: a text-pattern element's own selection (TextPattern.GetSelection) already covers everything inside that element, so once the walk reaches a text provider it reads that provider's selection and does NOT descend into its children. In a browser the page content sits under a single Document text provider, so the walk stops at the Document and never expands the thousands of nodes beneath it; the browser chrome above the Document (toolbar, address bar, tab strip) is shallow and narrow, so reaching the Document stays well under the perf budget even on a cold tree.
    //
    // Ambiguity rule (unchanged in intent from the prior Tier 2): return a selection only when EXACTLY ONE text provider in the walk has one. Two or more is ambiguous (for example a page selection plus an address-bar selection), and we fall back rather than guess which the user meant; the walk stops the moment a second selection is found.
    //
    // Bounds: the walk also stops if it exceeds a wall-clock budget or a visited-node cap, so a pathological tree can never hang us. When a bound is hit we treat the result as "no unambiguous selection" and fall through to the optional read-all / nothing fallback. The diagnostic log records the node count, elapsed time, and whether the walk was truncated so the real-world cost can be measured.
    private static MorphicResult<string?, ICaptureSelectedTextError> FindSelectedTextWithBoundedSearch(Windows.Win32.UI.Accessibility.IUIAutomation comUIAutomation, Windows.Win32.Foundation.HWND windowHandle, Windows.Win32.Foundation.HWND focusedControlHandle, Windows.Win32.UI.Accessibility.IUIAutomationCacheRequest comUIAutomationCacheRequest, bool readFocusedControlTextWhenNoSelection)
    {
        // budget tuned from real-world logs: successful bounded searches finish well under 300ms, so 350ms keeps every observed UIA win while ending a doomed search hundreds of ms sooner (anything past it falls through to the clipboard tier, which still captures the text). The node cap guarantees we never approach the multi-second hang a full-subtree walk produced.
        const long timeBudgetMilliseconds = 350;
        const int maximumNodesToVisit = 5000;

        // resolve the top-level element for the window; its subtree contains the (possibly host-framed) text controls
        Windows.Win32.UI.Accessibility.IUIAutomationElement? comTopLevelElement = null;
        try
        {
            comTopLevelElement = comUIAutomation.ElementFromHandleBuildCache(windowHandle, comUIAutomationCacheRequest);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
        }
        if (comTopLevelElement is null)
        {
            UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithBoundedSearch: ElementFromHandleBuildCache(window) returned null; no selectable text.");
            return UIAutomationSelectedTextScripts.ReadFocusedControlTextIfRequested(comUIAutomation, focusedControlHandle, comUIAutomationCacheRequest, readFocusedControlTextWhenNoSelection);
        }

        try
        {
            // the control view collapses the noise of the raw view (omitting purely structural elements), giving the smallest tree that still contains every real control; we use it to enumerate children during the walk
            Windows.Win32.UI.Accessibility.IUIAutomationTreeWalker? comControlViewWalker = null;
            try
            {
                comControlViewWalker = comUIAutomation.ControlViewWalker;
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
            }
            if (comControlViewWalker is null)
            {
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
            }

            try
            {
                // the breadth-first frontier; the queue OWNS every element placed in it (each must be released exactly once: when it is dequeued and processed, or, for anything left over after an early stop, when the queue is drained in the finally below)
                var elementsToVisit = new System.Collections.Generic.Queue<Windows.Win32.UI.Accessibility.IUIAutomationElement>();
                try
                {
                    string? firstSelectionText = null;
                    var numberOfNonEmptySelections = 0;
                    var nodesVisited = 0;
                    var searchWasTruncated = false;
                    var searchStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    // Inspect one element. If it is a text provider, read its selection (counting non-empty ones) and PRUNE (do not enqueue its children, because its selection already covers them). Otherwise enqueue its control-view children so the walk keeps descending. Returns true when the search should stop immediately, which happens only when a second selection has been found (the result is already known to be ambiguous).
                    bool ProcessElement(Windows.Win32.UI.Accessibility.IUIAutomationElement comElement)
                    {
                        // read the cached IsTextPatternAvailable flag; a defensive pattern-match (rather than a hard (bool) cast) avoids an InvalidCastException if a provider ever yields an unexpected or absent value
                        var isTextProvider = false;
                        try
                        {
                            var cachedIsTextPatternAvailable = comElement.GetCachedPropertyValue(Windows.Win32.UI.Accessibility.UIA_PROPERTY_ID.UIA_IsTextPatternAvailablePropertyId);
                            isTextProvider = cachedIsTextPatternAvailable is bool isTextPatternAvailable && isTextPatternAvailable;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            // if even the cached availability flag cannot be read, treat this element as a non-provider and let the walk continue past it
                            isTextProvider = false;
                        }

                        if (isTextProvider == true)
                        {
                            // this element owns a TextPattern; its GetSelection already covers everything beneath it, so we read it and do NOT enqueue its children (this prune is what stops a browser's huge document subtree from being materialized)
                            var queryResult = UIAutomationSelectedTextScripts.QueryElementForSelectedText(comElement);
                            if (queryResult.IsError == false && string.IsNullOrWhiteSpace(queryResult.Value) == false)
                            {
                                numberOfNonEmptySelections += 1;
                                if (numberOfNonEmptySelections == 1)
                                {
                                    firstSelectionText = queryResult.Value;
                                }
                                else
                                {
                                    // a second selection means the result is ambiguous; there is no point continuing the walk
                                    UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithBoundedSearch: a second non-empty selection was found; the selection is ambiguous.");
                                    return true;
                                }
                            }
                            return false;
                        }

                        // not a text provider: enqueue its control-view children so the walk descends into them
                        try
                        {
                            var comChildElement = comControlViewWalker!.GetFirstChildElementBuildCache(comElement, comUIAutomationCacheRequest);
                            while (comChildElement is not null)
                            {
                                // ownership of comChildElement transfers to the queue here; we must NOT release it in this method
                                elementsToVisit.Enqueue(comChildElement);
                                comChildElement = comControlViewWalker!.GetNextSiblingElementBuildCache(comChildElement, comUIAutomationCacheRequest);
                            }
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            // if enumerating children fails partway through, stop descending from this element; anything already enqueued is still processed and released normally
                        }
                        return false;
                    }

                    // process the top-level element inline (it is released by the outer finally, never via the queue), then process the frontier breadth-first until it empties or a bound is hit
                    nodesVisited += 1;
                    var stopRequested = ProcessElement(comTopLevelElement!);
                    if (stopRequested == false)
                    {
                        while (elementsToVisit.Count > 0)
                        {
                            if (searchStopwatch.ElapsedMilliseconds > timeBudgetMilliseconds || nodesVisited >= maximumNodesToVisit)
                            {
                                searchWasTruncated = true;
                                break;
                            }

                            var comElement = elementsToVisit.Dequeue();
                            try
                            {
                                nodesVisited += 1;
                                if (ProcessElement(comElement) == true)
                                {
                                    break;
                                }
                            }
                            finally
                            {
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(comElement);
                            }
                        }
                    }
                    searchStopwatch.Stop();

                    UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithBoundedSearch: visited " + nodesVisited + " node(s) in " + searchStopwatch.ElapsedMilliseconds + "ms (truncated=" + searchWasTruncated + "); found " + numberOfNonEmptySelections + " non-empty selection(s).");

                    if (numberOfNonEmptySelections == 1)
                    {
                        // exactly one text provider had a selection: the unambiguous case we can safely read
                        return MorphicResult.OkResult<string?>(firstSelectionText);
                    }
                    else if (numberOfNonEmptySelections >= 2)
                    {
                        // more than one selection: ambiguous. Do NOT guess which one the user meant; fall back. (A future clipboard-based fallback can attempt a better recovery here.)
                        UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithBoundedSearch: multiple selections detected; falling back rather than reading an ambiguous selection.");
                        return MorphicResult.OkResult<string?>(null);
                    }
                    else
                    {
                        // no text provider in the walk had a selection (or the walk was truncated before reaching one); optionally read the full text of the focused control instead
                        UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithBoundedSearch: no text provider had a non-empty selection.");
                        return UIAutomationSelectedTextScripts.ReadFocusedControlTextIfRequested(comUIAutomation, focusedControlHandle, comUIAutomationCacheRequest, readFocusedControlTextWhenNoSelection);
                    }
                }
                finally
                {
                    // release any elements still queued (we may have stopped early on ambiguity or hit a bound); elements that were processed were already released as they were dequeued
                    while (elementsToVisit.Count > 0)
                    {
                        var leftoverElement = elementsToVisit.Dequeue();
                        if (leftoverElement is not null)
                        {
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(leftoverElement);
                        }
                    }
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comControlViewWalker);
                comControlViewWalker = null;
            }
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ReleaseComObject(comTopLevelElement);
            comTopLevelElement = null;
        }
    }

    // sentinel for the dormant full-subtree scan below: a maximumDepth of this value means "no depth limit" (descend the entire subtree, exactly like the original FindAllBuildCache full scan).
    private const int FULL_SUBTREE_SCAN_NO_DEPTH_LIMIT = int.MaxValue;

    /* backup strategy (may be pruned if not used) */
    // DORMANT / NOT WIRED INTO THE LIVE PATH. The active Tier 2 is FindSelectedTextWithBoundedSearch above, which PRUNES at every text provider and is what we ship. This method is retained only as a future option (an explicit request to keep the full-depth scan available behind a "depthmax" knob); no caller invokes it today.
    //
    // Unlike the pruning walk, this scan is EXHAUSTIVE: it descends PAST text providers into their children, so on a browser document it can visit the entire accessibility tree. With maximumDepth == FullSubtreeScanNoDepthLimit it runs the original FindAllBuildCache(TreeScope_Subtree) full scan in a single COM call. WARNING: on a large web page that no-limit path is the multi-second / hundreds-of-thousands-of-providers worst case that motivated the pruning rewrite, and because it descends past providers it can also surface the browser "false ambiguity" where a nested provider duplicates the document's selection. With a finite maximumDepth it instead performs a manual breadth-first walk that stops descending at that depth, which is the "only scan a few levels deep" option.
    //
    // The ambiguity rule matches the active path: exactly one non-empty selection is read; two or more is ambiguous and we fall back rather than guess; none optionally reads the focused control's full text.
    private static MorphicResult<string?, ICaptureSelectedTextError> FindSelectedTextWithFullSubtreeScan(Windows.Win32.UI.Accessibility.IUIAutomation comUIAutomation, Windows.Win32.Foundation.HWND windowHandle, Windows.Win32.Foundation.HWND focusedControlHandle, Windows.Win32.UI.Accessibility.IUIAutomationCacheRequest comUIAutomationCacheRequest, bool readFocusedControlTextWhenNoSelection, int maximumDepth)
    {
        // resolve the top-level element for the window; its subtree is what we scan
        Windows.Win32.UI.Accessibility.IUIAutomationElement? comTopLevelElement = null;
        try
        {
            comTopLevelElement = comUIAutomation.ElementFromHandleBuildCache(windowHandle, comUIAutomationCacheRequest);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
        }
        if (comTopLevelElement is null)
        {
            UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithFullSubtreeScan: ElementFromHandleBuildCache(window) returned null; no selectable text.");
            return UIAutomationSelectedTextScripts.ReadFocusedControlTextIfRequested(comUIAutomation, focusedControlHandle, comUIAutomationCacheRequest, readFocusedControlTextWhenNoSelection);
        }

        try
        {
            string? firstSelectionText = null;
            var numberOfNonEmptySelections = 0;

            // Inspect a single text-provider element: read its selection and count the non-empty ones. Returns true when scanning should stop immediately, which happens only once a second non-empty selection has been seen (the result is already known to be ambiguous).
            bool AccumulateSelectionAndReportStop(Windows.Win32.UI.Accessibility.IUIAutomationElement comProviderElement)
            {
                var queryResult = UIAutomationSelectedTextScripts.QueryElementForSelectedText(comProviderElement);
                if (queryResult.IsError == true || string.IsNullOrWhiteSpace(queryResult.Value) == true)
                {
                    return false;
                }
                numberOfNonEmptySelections += 1;
                if (numberOfNonEmptySelections == 1)
                {
                    firstSelectionText = queryResult.Value;
                    return false;
                }
                UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithFullSubtreeScan: a second non-empty selection was found; the selection is ambiguous.");
                return true;
            }

            if (maximumDepth == UIAutomationSelectedTextScripts.FULL_SUBTREE_SCAN_NO_DEPTH_LIMIT)
            {
                // no depth limit: the original full-subtree scan. FindAllBuildCache materializes every text-pattern provider in the subtree in one COM round-trip (efficient as a single call, but on a browser document this is the multi-second / hundreds-of-thousands-of-providers worst case).
                Windows.Win32.UI.Accessibility.IUIAutomationCondition? comTextPatternAvailableCondition = null;
                try
                {
                    comTextPatternAvailableCondition = comUIAutomation.CreatePropertyCondition(Windows.Win32.UI.Accessibility.UIA_PROPERTY_ID.UIA_IsTextPatternAvailablePropertyId, true);
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
                }
                if (comTextPatternAvailableCondition is null)
                {
                    return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
                }

                try
                {
                    Windows.Win32.UI.Accessibility.IUIAutomationElementArray? comTextElements = null;
                    try
                    {
                        comTextElements = comTopLevelElement!.FindAllBuildCache(Windows.Win32.UI.Accessibility.TreeScope.TreeScope_Subtree, comTextPatternAvailableCondition, comUIAutomationCacheRequest);
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
                    }
                    if (comTextElements is null)
                    {
                        return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
                    }

                    try
                    {
                        var numberOfTextElements = comTextElements.Length;
                        UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithFullSubtreeScan: found " + numberOfTextElements + " text-pattern element(s) in the subtree.");
                        for (var indexOfTextElement = 0; indexOfTextElement < numberOfTextElements; indexOfTextElement += 1)
                        {
                            Windows.Win32.UI.Accessibility.IUIAutomationElement? comTextElement = null;
                            try
                            {
                                comTextElement = comTextElements!.GetElement(indexOfTextElement);
                                if (comTextElement is not null && AccumulateSelectionAndReportStop(comTextElement!) == true)
                                {
                                    break;
                                }
                            }
                            catch (System.Runtime.InteropServices.COMException ex)
                            {
                                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
                            }
                            finally
                            {
                                if (comTextElement is not null)
                                {
                                    System.Runtime.InteropServices.Marshal.ReleaseComObject(comTextElement);
                                    comTextElement = null;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (comTextElements is not null)
                        {
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(comTextElements);
                            comTextElements = null;
                        }
                    }
                }
                finally
                {
                    if (comTextPatternAvailableCondition is not null)
                    {
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(comTextPatternAvailableCondition);
                        comTextPatternAvailableCondition = null;
                    }
                }
            }
            else
            {
                // finite depth: a manual breadth-first walk that descends no deeper than maximumDepth. Unlike the pruning walk it descends PAST providers (so it remains an exhaustive scan within the depth bound), which is the "only scan a few levels deep" option.
                Windows.Win32.UI.Accessibility.IUIAutomationTreeWalker? comControlViewWalker = null;
                try
                {
                    comControlViewWalker = comUIAutomation.ControlViewWalker;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
                }
                if (comControlViewWalker is null)
                {
                    return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
                }

                try
                {
                    // determine whether an element is a text provider (so we read its selection); a defensive pattern-match avoids an InvalidCastException if the cached value is ever absent or unexpected
                    bool ElementIsTextProvider(Windows.Win32.UI.Accessibility.IUIAutomationElement comElement)
                    {
                        try
                        {
                            var cachedIsTextPatternAvailable = comElement.GetCachedPropertyValue(Windows.Win32.UI.Accessibility.UIA_PROPERTY_ID.UIA_IsTextPatternAvailablePropertyId);
                            return cachedIsTextPatternAvailable is bool isTextPatternAvailable && isTextPatternAvailable;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    }

                    // the frontier OWNS every element placed in it (each released exactly once: when dequeued and processed, or, for leftovers after an early stop, when drained in the finally below). Each entry carries the depth at which its element sits.
                    var elementsToVisit = new System.Collections.Generic.Queue<(Windows.Win32.UI.Accessibility.IUIAutomationElement Element, int Depth)>();
                    try
                    {
                        // enqueue the control-view children of comParentElement at childDepth; ownership of each child transfers to the queue
                        void EnqueueChildren(Windows.Win32.UI.Accessibility.IUIAutomationElement comParentElement, int childDepth)
                        {
                            try
                            {
                                var comChildElement = comControlViewWalker!.GetFirstChildElementBuildCache(comParentElement, comUIAutomationCacheRequest);
                                while (comChildElement is not null)
                                {
                                    elementsToVisit.Enqueue((comChildElement, childDepth));
                                    comChildElement = comControlViewWalker!.GetNextSiblingElementBuildCache(comChildElement, comUIAutomationCacheRequest);
                                }
                            }
                            catch (System.Runtime.InteropServices.COMException)
                            {
                                // if enumerating children fails partway through, stop descending from this element; anything already enqueued is still processed and released normally
                            }
                        }

                        // process the top-level element at depth 0 (released by the outer finally, never via the queue), then descend up to maximumDepth
                        var stopRequested = false;
                        if (ElementIsTextProvider(comTopLevelElement!) == true)
                        {
                            stopRequested = AccumulateSelectionAndReportStop(comTopLevelElement!);
                        }
                        if (stopRequested == false && 0 < maximumDepth)
                        {
                            EnqueueChildren(comTopLevelElement!, 1);
                        }

                        while (stopRequested == false && elementsToVisit.Count > 0)
                        {
                            var entry = elementsToVisit.Dequeue();
                            try
                            {
                                if (ElementIsTextProvider(entry.Element) == true)
                                {
                                    stopRequested = AccumulateSelectionAndReportStop(entry.Element);
                                }
                                if (stopRequested == false && entry.Depth < maximumDepth)
                                {
                                    EnqueueChildren(entry.Element, entry.Depth + 1);
                                }
                            }
                            finally
                            {
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(entry.Element);
                            }
                        }
                    }
                    finally
                    {
                        // release any elements still queued (we may have stopped early on ambiguity)
                        while (elementsToVisit.Count > 0)
                        {
                            var leftoverEntry = elementsToVisit.Dequeue();
                            if (leftoverEntry.Element is not null)
                            {
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(leftoverEntry.Element);
                            }
                        }
                    }
                }
                finally
                {
                    if (comControlViewWalker is not null)
                    {
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(comControlViewWalker);
                        comControlViewWalker = null;
                    }
                }
            }

            UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithFullSubtreeScan: found " + numberOfNonEmptySelections + " non-empty selection(s) (maximumDepth=" + (maximumDepth == UIAutomationSelectedTextScripts.FULL_SUBTREE_SCAN_NO_DEPTH_LIMIT ? "unlimited" : maximumDepth.ToString()) + ").");

            if (numberOfNonEmptySelections == 1)
            {
                // exactly one text provider had a selection: the unambiguous case we can safely read
                return MorphicResult.OkResult<string?>(firstSelectionText);
            }
            else if (numberOfNonEmptySelections >= 2)
            {
                // more than one selection: ambiguous. Do NOT guess which one the user meant; fall back.
                UIAutomationSelectedTextScripts.LogDiagnostic("FindSelectedTextWithFullSubtreeScan: multiple selections detected; falling back rather than reading an ambiguous selection.");
                return MorphicResult.OkResult<string?>(null);
            }
            else
            {
                // no text provider in the scan had a selection; optionally read the full text of the focused control instead
                return UIAutomationSelectedTextScripts.ReadFocusedControlTextIfRequested(comUIAutomation, focusedControlHandle, comUIAutomationCacheRequest, readFocusedControlTextWhenNoSelection);
            }
        }
        finally
        {
            if (comTopLevelElement is not null)
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comTopLevelElement);
                comTopLevelElement = null;
            }
        }
    }

    // Option #1 fallback: when no selection was found anywhere in the window, optionally read the entire text of the control that currently holds keyboard focus. This is opt-in (readFocusedControlText) and defaults off so the no-selection case stays silent unless a caller explicitly wants the read-all behavior.
    // NOTE: this reads the focused control specifically (per the "control which has keyboard focus" requirement); for host-framed apps whose focus is reported as the frame, this may yield nothing, in which case a richer clipboard-based fallback (planned) would be the next tier.
    private static MorphicResult<string?, ICaptureSelectedTextError> ReadFocusedControlTextIfRequested(Windows.Win32.UI.Accessibility.IUIAutomation comUIAutomation, Windows.Win32.Foundation.HWND focusedControlHandle, Windows.Win32.UI.Accessibility.IUIAutomationCacheRequest comUIAutomationCacheRequest, bool readFocusedControlText)
    {
        if (readFocusedControlText == false)
        {
            return MorphicResult.OkResult<string?>(null);
        }

        UIAutomationSelectedTextScripts.LogDiagnostic("ReadFocusedControlTextIfRequested: no selection found; reading the full text of the focused control (read-all fallback).");

        Windows.Win32.UI.Accessibility.IUIAutomationElement? comFocusedControlElement = null;
        try
        {
            comFocusedControlElement = comUIAutomation.ElementFromHandleBuildCache(focusedControlHandle, comUIAutomationCacheRequest);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
        }
        if (comFocusedControlElement is null)
        {
            UIAutomationSelectedTextScripts.LogDiagnostic("ReadFocusedControlTextIfRequested: ElementFromHandleBuildCache(focusedControl) returned null; nothing to read.");
            return MorphicResult.OkResult<string?>(null);
        }

        try
        {
            return UIAutomationSelectedTextScripts.QueryElementForAllText(comFocusedControlElement!);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ReleaseComObject(comFocusedControlElement);
            comFocusedControlElement = null;
        }
    }

    // Read the entire text of an element via the Text pattern's DocumentRange (used by the opt-in read-all fallback). Returns an OkResult with a null value when the element doesn't support the Text pattern.
    private static MorphicResult<string?, ICaptureSelectedTextError> QueryElementForAllText(Windows.Win32.UI.Accessibility.IUIAutomationElement comUIAutomationElement)
    {
        var isTextPatternAvailable = (bool)comUIAutomationElement.GetCachedPropertyValue(Windows.Win32.UI.Accessibility.UIA_PROPERTY_ID.UIA_IsTextPatternAvailablePropertyId);
        UIAutomationSelectedTextScripts.LogDiagnostic("QueryElementForAllText: IsTextPatternAvailable = " + isTextPatternAvailable + ".");
        if (isTextPatternAvailable == false)
        {
            return MorphicResult.OkResult<string?>(null);
        }

        Windows.Win32.UI.Accessibility.IUIAutomationTextPattern? comUIAutomationTextPattern = null;
        try
        {
            comUIAutomationTextPattern = (Windows.Win32.UI.Accessibility.IUIAutomationTextPattern)comUIAutomationElement.GetCachedPattern(Windows.Win32.UI.Accessibility.UIA_PATTERN_ID.UIA_TextPatternId);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
        }
        if (comUIAutomationTextPattern is null)
        {
            System.Diagnostics.Debug.Assert(false, "IUIAutomationElement.GetCachedPattern should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
        }

        try
        {
            // the DocumentRange spans the control's entire text content (independent of any selection); GetText(-1) returns all of it with no length cap
            Windows.Win32.UI.Accessibility.IUIAutomationTextRange? comDocumentRange = null;
            try
            {
                comDocumentRange = comUIAutomationTextPattern!.DocumentRange;
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
            }
            if (comDocumentRange is null)
            {
                return MorphicResult.OkResult<string?>(null);
            }

            try
            {
                // NOTE: CsWin32 surfaces the COM BSTR result as a raw Windows.Win32.Foundation.BSTR (an unmanaged-pointer wrapper); the runtime does not auto-free it, so we copy it to a managed string (via ToString, which uses Marshal.PtrToStringBSTR) and then release the BSTR ourselves with SysFreeString (Marshal.FreeBSTR)
                var textAsBstr = comDocumentRange!.GetText(-1);
                string? textAsString;
                try
                {
                    textAsString = textAsBstr.ToString();
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeBSTR(textAsBstr);
                }
                return MorphicResult.OkResult<string?>(textAsString);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comDocumentRange);
                comDocumentRange = null;
            }
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomationTextPattern);
            comUIAutomationTextPattern = null;
        }
    }

    private static MorphicResult<string?, ICaptureSelectedTextError> GetSelectedTextUsingUIAutomation(Windows.Win32.UI.Accessibility.IUIAutomation comUIAutomation)
    {
        // when we obtain our UI element, we need to make sure it supports the text pattern; we specify this via a cache request
        Windows.Win32.UI.Accessibility.IUIAutomationCacheRequest? comUIAutomationCacheRequest = null;
        try
        {
            comUIAutomationCacheRequest = comUIAutomation!.CreateCacheRequest();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
        }
        // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
        if (comUIAutomationCacheRequest is null)
        {
            System.Diagnostics.Debug.Assert(false, "CUIAutomation8.CreateCacheRequest should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
        }

        try
        {
            // NOTE: at this point, we know that we have a COM-instantiated IUIAutomationCacheRequest instance (which we can now populate with the cache request information we need to supply when we search for our focused element)

            // identify the Text control pattern in our cache request
            // NOTE: although UIA_TextPattern2Id was introduced in Windows 8, we don't need any additional capabilities from this newer pattern; to avoid any (unlikely) app compatibility issues, we're using the earlier (non-extended) interface
            comUIAutomationCacheRequest!.AddPattern(Windows.Win32.UI.Accessibility.UIA_PATTERN_ID.UIA_TextPatternId);
            //
            // make sure that the Text control pattern is available for the hWnd's automation element (in our cache request)
            // NOTE: although UIA_IsTextPattern2AvailablePropertyId was introduced in Windows 8, we don't need any additional capabilities from this newer pattern; to avoid any (unlikely) app compatibility issues, we're using the earlier (non-extended) interface
            comUIAutomationCacheRequest!.AddProperty(Windows.Win32.UI.Accessibility.UIA_PROPERTY_ID.UIA_IsTextPatternAvailablePropertyId);

            // using our cache request properties (i.e. text element), find the focused element; this should be the actual element which we query to retrieve the selected text
            Windows.Win32.UI.Accessibility.IUIAutomationElement? comUIAutomationElement = null;
            try
            {
                comUIAutomationElement = comUIAutomation!.GetFocusedElementBuildCache(comUIAutomationCacheRequest!);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
            }
            // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
            // NOTE: in our testing, we once got back "null", but were unable to determine the cause; it _might_ be because we weren't focused on a window which has a document control; we may need to explore this scenario further, add warnings/errors/fallbacks etc.
            if (comUIAutomationElement is null)
            {
                System.Diagnostics.Debug.Assert(false, "CUIAutomationElement.GetFocusedElementBuildCache should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
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
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomationElement);
                    comUIAutomationElement = null;
                }
            }
        }
        finally
        {
            if (comUIAutomationCacheRequest is not null)
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomationCacheRequest);
                comUIAutomationCacheRequest = null;
            }
        }
    }

    private static MorphicResult<string?, ICaptureSelectedTextError> QueryElementForSelectedText(Windows.Win32.UI.Accessibility.IUIAutomationElement comUIAutomationElement)
    {
        // NOTE: at this point, we should have the handle of an element which we can query for selected text

        // determine if the child element supports the text pattern (so that we can query for selected text)
        var isTextPatternAvailable = (bool)comUIAutomationElement.GetCachedPropertyValue(Windows.Win32.UI.Accessibility.UIA_PROPERTY_ID.UIA_IsTextPatternAvailablePropertyId);
        UIAutomationSelectedTextScripts.LogDiagnostic("QueryElementForSelectedText: IsTextPatternAvailable = " + isTextPatternAvailable + ".");
        if (isTextPatternAvailable == false)
        {
            // if the focused element doesn't support the text pattern, return null
            return MorphicResult.OkResult<string?>(null);
        }

        // we now know that the control supports text selected; let's retrieve the selected text

        // create an IUIAutomationTextPattern-compatible object (as an IUnknown) for the element using the text pattern
        Windows.Win32.UI.Accessibility.IUIAutomationTextPattern? comUIAutomationTextPattern = null;
        try
        {
            comUIAutomationTextPattern = (Windows.Win32.UI.Accessibility.IUIAutomationTextPattern)comUIAutomationElement.GetCachedPattern(Windows.Win32.UI.Accessibility.UIA_PATTERN_ID.UIA_TextPatternId);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
        }
        // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
        if (comUIAutomationTextPattern is null)
        {
            System.Diagnostics.Debug.Assert(false, "IUIAutomationElement.GetCachedPattern should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
            return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
        }
        //
        try
        {
            // NOTE: at this point, we have a COM-instantiated IUIAutomationTextPattern-compatible object which we can use to capture text from the current selection
            Windows.Win32.UI.Accessibility.IUIAutomationTextRangeArray? comUIAutomationTextRangeArray = null;
            try
            {
                comUIAutomationTextRangeArray = comUIAutomationTextPattern!.GetSelection();
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
            }
            // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
            if (comUIAutomationTextRangeArray is null)
            {
                System.Diagnostics.Debug.Assert(false, "IUIAutomationTextPattern.GetSelection should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
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
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomationTextRangeArray);
                    comUIAutomationTextRangeArray = null;
                }
            }
        }
        finally
        {
            if (comUIAutomationTextPattern is not null)
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomationTextPattern);
                comUIAutomationTextPattern = null;
            }
        }
    }

    private static MorphicResult<string?, ICaptureSelectedTextError> ExtractStringFromTextRangeArray(Windows.Win32.UI.Accessibility.IUIAutomationTextRangeArray comUIAutomationTextRangeArray)
    {
        // NOTE: at this point, we have a COM-instantiated IUIAutomationTextRangeArray (text range array); now let's extract each text range from this array, convert it to a string, and then concatenate all those ranges together into a string to return to our caller

        StringBuilder selectedTextBuilder = new();
        var numberOfTextRanges = comUIAutomationTextRangeArray.Length;
        UIAutomationSelectedTextScripts.LogDiagnostic("ExtractStringFromTextRangeArray: selection range count = " + numberOfTextRanges + ".");
        if (numberOfTextRanges > 0)
        {
            for (var iTextRange = 0; iTextRange < numberOfTextRanges; iTextRange += 1)
            {
                Windows.Win32.UI.Accessibility.IUIAutomationTextRange? comUIAutomationTextRange = null;
                try
                {
                    comUIAutomationTextRange = comUIAutomationTextRangeArray!.GetElement(iTextRange);
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.Win32Error(ex.ErrorCode));
                }
                // if our COM instantiation failed (by returning null, without an exception) then return that error condition to the caller
                if (comUIAutomationTextRange is null)
                {
                    System.Diagnostics.Debug.Assert(false, "IUIAutomationTextRangeArray.GetElement should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
                    return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.ComInterfaceInstantiationFailed());
                }
                //
                try
                {
                    // NOTE: CsWin32 surfaces the COM BSTR result as a raw Windows.Win32.Foundation.BSTR (an unmanaged-pointer wrapper); unlike the tlbimp projection, the runtime does not auto-free it, so we copy it to a managed string (via ToString, which uses Marshal.PtrToStringBSTR) and then release the BSTR ourselves with SysFreeString (Marshal.FreeBSTR)
                    var textRangeAsBstr = comUIAutomationTextRange!.GetText(-1);
                    string? textRangeAsString;
                    try
                    {
                        textRangeAsString = textRangeAsBstr.ToString();
                    }
                    finally
                    {
                        System.Runtime.InteropServices.Marshal.FreeBSTR(textRangeAsBstr);
                    }
                    if (textRangeAsString is null)
                    {
                        System.Diagnostics.Debug.Assert(false, "IUIAutomationTextRange.GetText should not return null; investigate this unexpected condition (and, if it's an allowed scenario, then simply remove this assertion and return the ComInterfaceInstantiationFailed error.");
                        return MorphicResult.ErrorResult<ICaptureSelectedTextError>(new ICaptureSelectedTextError.TextRangeIsNull());
                    }
                    selectedTextBuilder.Append(textRangeAsString);
                }
                finally
                {
                    if (comUIAutomationTextRange is not null)
                    {
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(comUIAutomationTextRange);
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
