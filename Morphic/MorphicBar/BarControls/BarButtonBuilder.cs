// Copyright 2026 Raising the Floor - US, Inc.
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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Morphic.MorphicBar.BarControls;

// Builds the live button (plain Button or GuardedToggleButton) for a single BarButtonData and
// owns the click behavior shared by every bar button. Both the standalone BarButtonControl and
// each sub-button of BarMultiButtonControl route through here, so the two controls cannot drift
// apart in how they wire data binding, compound visual state, accessibility, or the click flow.
//
// The leaf data type is the same in both controls (BarButtonData), which is what makes a single
// builder possible: only the surrounding LAYOUT differs (a lone button vs a segmented group), and
// layout stays in the controls. Everything button-local lives here.
internal static class BarButtonBuilder
{
    // Per-button re-entry guard. Replaces BarButtonControl's instance bool and
    // BarMultiButtonControl's HashSet<ButtonBase>: pinning the flag onto each button collapses
    // both into one mechanism and naturally scopes re-entry to the individual button, so an
    // in-flight action on one sub-button never blocks a click on a sibling.
    private static readonly DependencyProperty IsActionInProgressProperty =
        DependencyProperty.RegisterAttached(
            "IsActionInProgress",
            typeof(bool),
            typeof(BarButtonBuilder),
            new PropertyMetadata(false));

    private static bool GetIsActionInProgress(DependencyObject target)
    {
        return (bool)target.GetValue(IsActionInProgressProperty);
    }

    private static void SetIsActionInProgress(DependencyObject target, bool value)
    {
        target.SetValue(IsActionInProgressProperty, value);
    }

    // Records whether the gesture about to raise this button's Click was a keyboard / assistive-
    // technology invocation (true) rather than a mouse / touch / pen one (false). RunClickAsync
    // reads it to publish the modality for modality-aware actions (see BarButtonInvocationContext).
    //
    // Defaults to true (keyboard): a programmatic Invoke (automation peer, with no preceding pointer
    // gesture) is treated as keyboard-style, which is the accessible default -- those invocations
    // keep focus on the bar. A PointerPressed flips it to false just before the click; RunClickAsync
    // resets it to true in its finally so the NEXT invocation starts from the keyboard default
    // unless a fresh pointer press sets it otherwise. That reset is what keeps a keyboard invocation
    // following a mouse click from inheriting the stale "pointer".
    private static readonly DependencyProperty InvokedViaKeyboardProperty =
        DependencyProperty.RegisterAttached(
            "InvokedViaKeyboard",
            typeof(bool),
            typeof(BarButtonBuilder),
            new PropertyMetadata(true));

    private static bool GetInvokedViaKeyboard(DependencyObject target)
    {
        return (bool)target.GetValue(InvokedViaKeyboardProperty);
    }

    private static void SetInvokedViaKeyboard(DependencyObject target, bool value)
    {
        target.SetValue(InvokedViaKeyboardProperty, value);
    }

    // Builds the button for `data` and wires everything that does NOT depend on the host control's
    // layout: data binding (the data is the source of truth, the live control follows via INPC),
    // compound visual state, accessibility name + tooltip, the click handler, and the Tag back-
    // reference the click handler reads. The caller passes the styles because they live in each
    // control's own XAML Resources and differ between the two controls (different named template
    // parts). Layout concerns the caller still owns after this returns: corner radii, margins, grid
    // placement, and (for the multi-button) the SubButtonText line configuration.
    public static ButtonBase CreateButton(BarButtonData data, Style plainStyle, Style toggleStyle)
    {
        ButtonBase button;
        if (data.IsToggle)
        {
            // GuardedToggleButton suppresses the framework's automatic IsChecked toggle on click.
            // HandleButtonClick computes the user's intent (!current), runs the action, and on
            // success writes the value to data, which propagates back to IsChecked via the
            // PropertyChanged subscription below. See GuardedToggleButton.cs for the full rationale.
            var toggleButton = new GuardedToggleButton
            {
                Style = toggleStyle,
                IsChecked = data.IsChecked,
                IsEnabled = data.IsEnabled,
                Content = data.Text,
            };
            // The data is the source of truth: when data.IsChecked or IsEnabled changes (via the
            // action's post-completion write, OR via an external listener writing directly to the
            // data) the live ToggleButton is pulled into line here. We deliberately do NOT mirror
            // Checked/Unchecked back into the data -- writes happen only in HandleButtonClick on
            // action SUCCESS, so an in-flight real-time listener can update data.IsChecked during
            // the action without the click handler clobbering it. Unsubscribed on Unloaded so the
            // data doesn't retain a discarded button (Data reassignment / rotation rebuilds tear
            // the old button down). Marshalled through the button's DispatcherQueue because the
            // writer may be off the UI thread (system event listeners typically fire on background
            // threads); the setter's equality short-circuit prevents a feedback loop.
            PropertyChangedEventHandler propertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(BarButtonData.IsChecked))
                {
                    _ = toggleButton.DispatcherQueue.TryEnqueue(() =>
                    {
                        toggleButton.IsChecked = data.IsChecked;
                    });
                }
                else if (args.PropertyName == nameof(BarButtonData.IsEnabled))
                {
                    _ = toggleButton.DispatcherQueue.TryEnqueue(() =>
                    {
                        toggleButton.IsEnabled = data.IsEnabled;
                    });
                }
            };
            data.PropertyChanged += propertyChangedHandler;
            toggleButton.Unloaded += (_, _) => data.PropertyChanged -= propertyChangedHandler;
            ToggleButtonCompoundState.Wire(toggleButton);
            button = toggleButton;
        }
        else
        {
            var plainButton = new Button
            {
                Style = plainStyle,
                IsEnabled = data.IsEnabled,
                Content = data.Text,
            };
            // Mirror data.IsEnabled writes onto the live Button. Same rationale as the toggle path's
            // IsEnabled subscription: the data is the source of truth and external state-source
            // bridges (e.g. the Text Size +/- factory disabling at min/max DPI) write to data, not
            // to the UI control. A plain button has no checked state, so only IsEnabled is mirrored.
            // Marshalled + unsubscribed as above.
            PropertyChangedEventHandler plainPropertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(BarButtonData.IsEnabled))
                {
                    _ = plainButton.DispatcherQueue.TryEnqueue(() =>
                    {
                        plainButton.IsEnabled = data.IsEnabled;
                    });
                }
            };
            data.PropertyChanged += plainPropertyChangedHandler;
            plainButton.Unloaded += (_, _) => data.PropertyChanged -= plainPropertyChangedHandler;
            ButtonCompoundState.Wire(plainButton);
            button = plainButton;
        }

        // Closure captures `data` directly so the click flow needs no back-reference channel on the
        // button (no button.Tag, no per-control dictionary). Keyboard-driven invokes (e.g.
        // BarMultiButtonControl's inc/dec shortcuts firing the automation peer's Invoke) raise this
        // same Click and therefore run through the identical flow. async lambda is the required shape
        // for a routed event handler; the awaited RunClickAsync isolates the actual work.
        button.Click += async (_, _) => await BarButtonBuilder.RunClickAsync(button, data);

        // Record a pointer-driven invocation so the click flow can distinguish it from a keyboard /
        // assistive-technology one (modality-aware focus policy; see RunClickAsync and
        // BarButtonInvocationContext). ButtonBase marks PointerPressed handled internally, so this
        // must register with handledEventsToo: true or the handler never runs. PointerPressed always
        // precedes the PointerReleased that raises Click, so the flag is set in time; keyboard and
        // programmatic invocations leave it at its keyboard-default true.
        button.AddHandler(
            Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => BarButtonBuilder.SetInvokedViaKeyboard(button, false)),
            handledEventsToo: true);

        // accessible (screen reader) name; resolve against the current checked state (a fixed name
        // ignores it), falling back to the visible text when no accessible name was specified.
        AutomationProperties.SetName(button, data.AccessibleName?.ResolveName(data.IsChecked) ?? data.Text);
        ToolTipService.SetToolTip(button, data.Tooltip);

        // Right-click / context-menu-key flyout, built from the button's data. Null when the button has no menu
        // items (e.g. Snip), so ContextFlyout stays unset and no menu appears. This covers single buttons AND
        // multi-button sub-buttons (both route through here); the bar's menu/close chrome are not built here, so
        // they get no context menu.
        var contextFlyout = BarButtonContextMenuBuilder.Build(data);
        if (contextFlyout is not null)
        {
            // ATTACH the flyout (do NOT set ContextFlyout): ContextFlyout would auto-show at the pointer, but we
            // want the logo-menu-style edge-anchored, away-from-bar placement. MorphicBarWindow handles
            // ContextRequested centrally and shows this attached flyout positioned (see OnBarItemContextRequested).
            Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase.SetAttachedFlyout(button, contextFlyout);
        }

        // Stash the hover Info content on the button so a sub-button shows its OWN info (e.g. each Contrast/Color/
        // Dark/Night toggle, or the +/- , Show/Hide, Play/Stop halves), resolved by MorphicBarWindow walking up from
        // the pointer -- the button is the NEAREST ancestor with content, so it wins over the group. Only when a
        // description is authored; a sub-button without one falls through to the group's Info content. The right-
        // click Settings hint is set as a separate footer line when this button has a Settings menu (SettingsPage,
        // possibly inherited).
        if (string.IsNullOrWhiteSpace(data.InfoSubtitle) == false)
        {
            var infoTitle = string.IsNullOrEmpty(data.InfoTitle)
                ? (string.IsNullOrEmpty(data.Header) ? data.Text : data.Header!)
                : data.InfoTitle!;
            var infoHint = Morphic.MorphicBar.Info.BarInfo.SettingsHint(data.SettingsPage is not null);
            Morphic.MorphicBar.Info.BarInfo.SetContent(button, new Morphic.MorphicBar.Info.BarInfoContent(infoTitle, data.InfoSubtitle, data.InfoDotsProvider, infoHint));
        }

        return button;
    }

    private static async Task RunClickAsync(ButtonBase button, BarButtonData data)
    {
        var action = data.Action;
        if (action is null)
        {
            return;
        }
        if (BarButtonBuilder.GetIsActionInProgress(button))
        {
            return;
        }

        // Compute the user's INTENT (the value the toggle should settle at). Because
        // GuardedToggleButton suppresses the framework's automatic IsChecked toggle on click, the
        // live IsChecked still holds the PRE-click value here, so flipping it yields the intended
        // new value. For non-toggle buttons intent is meaningless (null). A null IsChecked (three-
        // state) is treated as false for the flip; we don't use IsThreeState in this codebase, so
        // this just keeps the null-safety honest.
        var toggleButton = button as ToggleButton;
        bool? intendedIsChecked = toggleButton is null ? null : !(toggleButton.IsChecked == true);
        var actionTag = data.ActionTag;

        // Re-entry is prevented by the attached flag (checked above). We do NOT block input via
        // IsEnabled / IsHitTestVisible: GuardedToggleButton already prevents the framework's auto-
        // toggle from disturbing the visual, and re-clicks during the action are no-ops because
        // this guard catches them before the action is invoked again.
        BarButtonBuilder.SetIsActionInProgress(button, true);

        // Publish this click's gesture modality for the action delegate to read. The delegate's
        // fixed (actionTag, isChecked) signature has no room for it, so it travels through this
        // ambient UI-thread slot. Safe because DelayedInProgressVisual.RunAsync invokes the action
        // SYNCHRONOUSLY as its first statement (before any await) and modality-aware actions read
        // the slot at their top before their first await, so no other click can overwrite it in
        // between. Set for every button; only actions that care read it.
        BarButtonInvocationContext.SetInvokedViaKeyboard(BarButtonBuilder.GetInvokedViaKeyboard(button));

        bool actionSucceeded;
        try
        {
            var result = await DelayedInProgressVisual.RunAsync(button, () => action.Invoke(actionTag, intendedIsChecked));
            actionSucceeded = result.IsSuccess;
        }
        catch (Exception ex)
        {
            // an action throwing is treated as failure: the system state didn't change in any
            // well-defined way, so we don't write the intended value to data
            Debug.WriteLine($"[BarItem] {actionTag} threw: {ex}");
            actionSucceeded = false;
        }
        finally
        {
            BarButtonBuilder.SetIsActionInProgress(button, false);
            // Reset the modality flag to its keyboard default so the NEXT invocation of this button
            // doesn't inherit a stale "pointer" from this click. A fresh PointerPressed sets it back
            // to pointer in time for that click; a keyboard invocation correctly sees the default.
            BarButtonBuilder.SetInvokedViaKeyboard(button, true);
        }

        // Data is the single source of truth: on success we write the intended value, which
        // propagates back to the live IsChecked via the PropertyChanged subscription from
        // CreateButton (the visible flip happens then). On failure: no-op, because
        // GuardedToggleButton suppressed the auto-toggle, so the visual never moved.
        if (toggleButton is not null && intendedIsChecked.HasValue && actionSucceeded)
        {
            data.IsChecked = intendedIsChecked.Value;
        }
    }
}
