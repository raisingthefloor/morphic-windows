// Copyright 2020-2026 Raising the Floor - US, Inc.
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
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Morphic.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace Morphic.MorphicBar.BarControls;

public sealed partial class BarMultiButtonControl : UserControl, IBarItemControl
{
    private BarMultiButtonData? _data;
    private readonly List<ButtonBase> _subButtons = new();
    private readonly HashSet<ButtonBase> _subButtonsWithActionInProgress = new();
    private bool _incDecShortcutsEnabled = false;
    private int _decrementButtonIndex = -1;
    private int _incrementButtonIndex = -1;
    private Orientation _orientation = Orientation.Horizontal;

    private XamlRoot? _subscribedXamlRoot;

    public BarMultiButtonControl()
    {
        this.InitializeComponent();
        this.Loaded += BarMultiButtonControl_Loaded;
        this.Unloaded += BarMultiButtonControl_Unloaded;
    }

    private void BarMultiButtonControl_Loaded(object sender, RoutedEventArgs e)
    {
        // re-run uniform-horizontal sizing now that we are in the visual tree and Measure will give
        // accurate per-button widths; ApplyData may have run while the control was still detached
        // from its parent, in which case its initial measurement pass returned zero/incomplete values
        this.RefreshUniformHorizontalSizing();

        // Re-run uniform sizing whenever the bar's rasterization scale changes. Font metrics
        // (and thus sub-button DesiredSize.Width) differ across scales due to per-scale subpixel
        // rounding -- a column width computed at 150% can be ~1 logical px too tight at 125%,
        // wrapping the widest button's label ("Show" splitting into "Sho" + "w"). XamlRoot.Changed
        // is the WinUI-native scale-change signal and fires reliably for both DPI changes on the
        // current monitor and the bar moving to a different-DPI monitor. We track the subscribed
        // XamlRoot so we can detach on Unload even if the control's XamlRoot has been cleared by
        // the time Unloaded fires.
        if (this.XamlRoot is not null)
        {
            this.XamlRoot.Changed += this.OnXamlRootChanged;
            _subscribedXamlRoot = this.XamlRoot;
        }
    }

    private void BarMultiButtonControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedXamlRoot is not null)
        {
            _subscribedXamlRoot.Changed -= this.OnXamlRootChanged;
            _subscribedXamlRoot = null;
        }
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        // No payload on XamlRootChangedEventArgs that distinguishes scale-only vs other root
        // changes; re-running uniform sizing is idempotent and cheap, so just run it on every
        // signal. Marshal to the dispatcher because XamlRoot.Changed can deliver before the
        // post-scale-change layout pass settles -- queueing lets the new measurements stabilize
        // before we re-snapshot widths.
        this.DispatcherQueue.TryEnqueue(() =>
        {
            this.RefreshUniformHorizontalSizing();
        });
    }

    //

    // Depth-first walk of the visual tree under `root` looking for the first TextBlock with the
    // given Name. Used after a templated control's Loaded fires so we can reach a named element
    // inside its ControlTemplate (which is not addressable via FindName from outside the
    // template). Returns null if no match is found.
    private static TextBlock? FindDescendantTextBlockByName(DependencyObject root, string name)
    {
        int childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock textBlock && textBlock.Name == name)
            {
                return textBlock;
            }
            var found = FindDescendantTextBlockByName(child, name);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    public BarMultiButtonData? Data
    {
        get => _data;
        set
        {
            _data = value;
            this.ApplyData();
        }
    }

    // Layout direction for the sub-button group.
    //   Horizontal: sub-buttons run left-to-right; first/last round their left/right corners.
    //   Vertical:   sub-buttons stack top-to-bottom; first/last round their top/bottom corners.
    // The header always sits above the sub-button group regardless of orientation.
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
            {
                return;
            }
            _orientation = value;

            // re-lay out the existing sub-button instances in the new orientation rather than
            // rebuilding them via ApplyData. Rebuilding would tear down the live ToggleButton
            // instances and lose their IsChecked state, their compound-state tracker (in-progress
            // visual, pointer state), event subscriptions, etc -- so rotating the bar mid-action
            // would visibly "cancel" the in-progress animation. ReorientButtonsContainer only
            // touches grid placement, margins, and corner radii (the only orientation-dependent
            // attributes). If the buttons don't exist yet (Data not yet assigned), fall back to
            // ApplyData since there's nothing to preserve.
            if (_subButtons.Count > 0)
            {
                this.ReorientButtonsContainer();
            }
            else
            {
                this.ApplyData();
            }
        }
    }

    // Returns the desired size this multi-button group would have if rendered with the specified
    // orientation. The live ButtonsContainer Grid is configured for the CURRENT orientation by
    // ApplyData (column vs row layout, per-button margins, corner radii), so a plain Measure on
    // 'this' only answers for the current orientation. Instead, we compose the size analytically
    // from per-child measurements which don't depend on the panel's orientation.
    //
    // Rule (matches v1.x):
    //   - Horizontal, AutoSize:         each sub-button at its own natural width
    //   - Horizontal, StretchToLargest: every sub-button equal to the widest's natural width
    //   - Vertical, any SizingMode:     every sub-button equal to the widest's natural width
    //                                   (the vertical rule overrides SizingMode)
    // The header always sits above the sub-buttons in both orientations.
    public Windows.Foundation.Size MeasureForOrientation(Windows.Foundation.Size availableSize, Orientation orientation)
    {
        if (_data is null || _subButtons.Count == 0)
        {
            this.Measure(availableSize);
            return this.DesiredSize;
        }

        // measure the header (HeaderTextBlock.Visibility was set by ApplyData based on whether
        // _data.Header is non-empty); DesiredSize already includes the TextBlock's own Margin
        double headerWidth = 0;
        double headerHeight = 0;
        if (this.HeaderTextBlock.Visibility == Microsoft.UI.Xaml.Visibility.Visible)
        {
            this.HeaderTextBlock.Measure(availableSize);
            headerWidth = this.HeaderTextBlock.DesiredSize.Width;
            headerHeight = this.HeaderTextBlock.DesiredSize.Height;
        }

        // measure each sub-button and back out the in-effect Margin so we work in natural
        // (margin-free) sizes; the live Margin that would be set by ApplyData for the _current_
		// orientation would otherwise pollute the composition for the _requested_ orientation.
        // Also track maxDesiredWidth (i.e. DesiredSize.Width INCLUDING margin) -- needed for the
        // StretchToLargest path below, which must mirror RefreshUniformHorizontalSizing's
        // slack-padded column formula so the reported group width matches the painted width.
        double maxNaturalWidth = 0;
        double maxNaturalHeight = 0;
        double maxDesiredWidth = 0;
        double sumNaturalWidth = 0;
        double sumNaturalHeight = 0;
        foreach (var subButton in _subButtons)
        {
            subButton.Measure(availableSize);
            var marginH = subButton.Margin.Left + subButton.Margin.Right;
            var marginV = subButton.Margin.Top + subButton.Margin.Bottom;
            var naturalW = System.Math.Max(0, subButton.DesiredSize.Width - marginH);
            var naturalH = System.Math.Max(0, subButton.DesiredSize.Height - marginV);
            if (naturalW > maxNaturalWidth) { maxNaturalWidth = naturalW; }
            if (naturalH > maxNaturalHeight) { maxNaturalHeight = naturalH; }
            if (subButton.DesiredSize.Width > maxDesiredWidth) { maxDesiredWidth = subButton.DesiredSize.Width; }
            sumNaturalWidth += naturalW;
            sumNaturalHeight += naturalH;
        }

        // gap contribution along the layout axis: ApplyData sets each end button to contribute
        // one BarControlMetrics.ButtonInnerMargin on its inner side, and each middle button to contribute
        // one on each side, so across n buttons the total margin extent is 2*(n-1)*BarControlMetrics.ButtonInnerMargin
        int n = _subButtons.Count;
        double gapContribution = 2 * (n - 1) * BarControlMetrics.ButtonInnerMargin;

        // AlwaysHorizontalSubButtons overrides the bar's requested orientation for the sub-button
        // arrangement (e.g. Text Size +/- stays side-by-side even in a vertical bar)
        var effectiveSubButtonOrientation = _data.AlwaysHorizontalSubButtons
            ? Orientation.Horizontal
            : orientation;

        double subButtonsWidth;
        double subButtonsHeight;
        if (effectiveSubButtonOrientation == Orientation.Horizontal)
        {
            subButtonsHeight = maxNaturalHeight;
            switch (_data.SizingMode)
            {
                case MultiButtonSizingMode.AutoSize:
                    subButtonsWidth = sumNaturalWidth + gapContribution;
                    break;
                case MultiButtonSizingMode.StretchToLargest:
                    // Mirror RefreshUniformHorizontalSizing's column-width formula EXACTLY:
                    // Math.Min(Math.Ceiling(maxDesiredWidth), BarControlMetrics.MaxSubButtonWidth). The
                    // painted ButtonsContainer width is n * that value; if we returned a
                    // different number the bar's outer width budget and the painted width would
                    // disagree (trailing items overflow off the right side of a horizontal bar).
                    subButtonsWidth = n * System.Math.Min(System.Math.Ceiling(maxDesiredWidth), BarControlMetrics.MaxSubButtonWidth);
                    break;
                default:
                    throw new MorphicUnhandledCaseException(_data.SizingMode);
            }
        }
        else
        {
            // vertical: SizingMode is overridden -- all sub-buttons get equal (max) width
            subButtonsWidth = maxNaturalWidth;
            subButtonsHeight = sumNaturalHeight + gapContribution;
        }

        // Returned width per BAR orientation (NOT the effective sub-button orientation):
        //   Horizontal bar: max(subButtonsWidth, headerWidth). If the header is wider than
        //                   the buttons (e.g. "Read Selected" header above narrow play/stop
        //                   glyph buttons), the group's reported width tracks the header so
        //                   the bar item spans its actual visible footprint -- otherwise the
        //                   header hangs over into the gap between groups and wastes
        //                   perceived space. The buttons stay at natural width and render
        //                   centered (ButtonsContainer is HorizontalAlignment=Center).
        //   Vertical bar:   buttons only. The reported width feeds the bar's THICKNESS
        //                   calculation (Pass 1 -- maxItemNaturalThickness across items),
        //                   so including a long header here would widen the entire vertical
        //                   bar; a header like "Read Selected" or "Contrast & Color" should
        //                   wrap (TextWrapping="Wrap" MaxLines="2") rather than push the bar
        //                   wider, and the bar's Pass 2 measure captures the wrapped height.
        // NOTE: this branches on the BAR orientation (the `orientation` parameter), not on
        // effectiveSubButtonOrientation. AlwaysHorizontalSubButtons groups (Text Size,
        // Read Selected) keep their sub-buttons side-by-side in a vertical bar, but the
        // group's reported WIDTH still needs to follow the vertical-bar-thickness rule.
        double returnedWidth = (orientation == Orientation.Horizontal)
            ? System.Math.Max(subButtonsWidth, headerWidth)
            : subButtonsWidth;
        return new Windows.Foundation.Size(
            returnedWidth,
            headerHeight + subButtonsHeight);
    }

    private void ApplyData()
    {
        // tear down any previously-built sub-buttons
        this.ButtonsContainer.Children.Clear();
        this.ButtonsContainer.ColumnDefinitions.Clear();
        this.ButtonsContainer.RowDefinitions.Clear();
        _subButtons.Clear();

        // if the inc/dec shortcuts were enabled for the previous data, strip them; the caller must re-enable them
        // after assigning new Data (the sub-button count could have changed)
        if (_incDecShortcutsEnabled)
        {
            this.KeyDown -= BarMultiButtonControl_KeyDown_IncDec;
            _incDecShortcutsEnabled = false;
            _decrementButtonIndex = -1;
            _incrementButtonIndex = -1;
        }

        if (_data is null)
        {
            this.HeaderTextBlock.Text = string.Empty;
            this.HeaderTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        // header (only shown when Header is non-empty)
        if (string.IsNullOrEmpty(_data.Header))
        {
            this.HeaderTextBlock.Text = string.Empty;
            this.HeaderTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            this.HeaderTextBlock.Text = _data.Header;
            this.HeaderTextBlock.Visibility = Visibility.Visible;
        }

        // validate SizingMode up front (defends against unknown values added in the future).
        // All sub-button column/row definitions are Auto-sized at this stage. Uniform-width
        // rendering is achieved via two different mechanisms:
        //   - horizontal + StretchToLargest: a separate measurement pass in
        //     RefreshUniformHorizontalSizing sets each Grid column to a fixed pixel width
        //     equal to the widest sub-button's natural width
        //   - any vertical: a single Auto-width column combined with the buttons'
        //     HorizontalAlignment=Stretch causes the Grid to size that column to the widest
        //     sub-button's natural width and all buttons to render at that uniform width
        //     (no measurement pass required)
        switch (_data.SizingMode)
        {
            case MultiButtonSizingMode.AutoSize:
            case MultiButtonSizingMode.StretchToLargest:
                break;
            default:
                throw new MorphicUnhandledCaseException(_data.SizingMode);
        }
        //
        // validate the orientation up front (defends against unknown values added in the future)
        switch (_orientation)
        {
            case Orientation.Horizontal:
            case Orientation.Vertical:
                break;
            default:
                throw new MorphicUnhandledCaseException(_orientation);
        }

        // AlwaysHorizontalSubButtons overrides the bar's orientation for sub-button arrangement
        // (Text Size +/- stays side-by-side even in a vertical bar). All sub-button layout decisions
        // below use this effective orientation, not the bar's orientation directly.
        var effectiveSubButtonOrientation = (_data.AlwaysHorizontalSubButtons)
            ? Orientation.Horizontal
            : _orientation;

        // vertical sub-button arrangement: add a single Auto-width column so the Grid sizes it to
        // the widest sub-button's natural width; buttons' HorizontalAlignment=Stretch (set in their
        // Style) then makes every button render at that uniform width
        if (effectiveSubButtonOrientation == Orientation.Vertical)
        {
            this.ButtonsContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        var plainStyle = (Style)this.Resources["SubButtonStyle"];
        var toggleStyle = (Style)this.Resources["SubToggleButtonStyle"];

        for (int i = 0; i < _data.Buttons.Count; i++)
        {
            var buttonData = _data.Buttons[i];

            // sub-buttons must be TextOnly; reject any other layout style
            switch (buttonData.LayoutStyle)
            {
                case BarButtonLayoutStyle.TextOnly:
                    break;
                default:
                    throw new ArgumentException("LayoutStyle must be 'TextOnly' for horizontal multi-button controls");
            }

            ButtonBase button;
            if (buttonData.IsToggle)
            {
                var toggleButton = new ToggleButton
                {
                    Style = toggleStyle,
                    IsChecked = buttonData.IsChecked,
                    IsEnabled = buttonData.IsEnabled,
                    Content = buttonData.Text,
                };
                // Intentionally NOT mirroring Checked/Unchecked back into buttonData.IsChecked. Data
                // updates happen only in SubButton_Click on action SUCCESS so that an in-flight
                // real-time event listener can update buttonData.IsChecked during the action without
                // our immediate-toggle handler clobbering the listener's value.
                //
                // The data is the source of truth: when buttonData.IsChecked or IsEnabled changes
                // (via the action's post-completion write, OR via an external listener writing
                // directly to the data), the PropertyChanged subscription below pulls the new
                // value into the live ToggleButton. Unsubscribed on Unloaded so the data doesn't
                // hold a reference to a discarded UI (the BarMultiButtonControl's Data setter
                // clears ButtonsContainer.Children, unloading the old toggles; rotation no longer
                // rebuilds, see ReorientButtonsContainer).
                //
                // Marshal through DispatcherQueue because the writer may be off the UI thread
                // (system event listeners typically fire on background threads). The setter's
                // equality short-circuit prevents a feedback loop if the data write originated
                // from the UI.
                var capturedToggleButton = toggleButton;
                var capturedData = buttonData;
                PropertyChangedEventHandler propertyChangedHandler = (_, args) =>
                {
                    if (args.PropertyName == nameof(BarButtonData.IsChecked))
                    {
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            capturedToggleButton.IsChecked = capturedData.IsChecked;
                        });
                    }
                    else if (args.PropertyName == nameof(BarButtonData.IsEnabled))
                    {
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            capturedToggleButton.IsEnabled = capturedData.IsEnabled;
                        });
                    }
                };
                capturedData.PropertyChanged += propertyChangedHandler;
                toggleButton.Unloaded += (_, _) => capturedData.PropertyChanged -= propertyChangedHandler;
                toggleButton.Click += SubButton_Click;
                ToggleButtonCompoundState.Wire(toggleButton);
                button = toggleButton;
            }
            else
            {
                var plainButton = new Button
                {
                    Style = plainStyle,
                    IsEnabled = buttonData.IsEnabled,
                    Content = buttonData.Text,
                };
                // Mirror BarButtonData.IsEnabled writes onto the live Button. Same rationale as
                // the toggle path's IsEnabled subscription -- the data is the source of truth and
                // external state-source bridges (e.g. the Text Size +/- factory's
                // recomputeState, which disables at min/max DPI) write to data, not to the
                // UI control. Marshal through DispatcherQueue because writers may be off the UI
                // thread; INPC equality short-circuit in BarButtonData prevents feedback loops.
                var capturedPlainButton = plainButton;
                var capturedPlainData = buttonData;
                PropertyChangedEventHandler plainPropertyChangedHandler = (_, args) =>
                {
                    if (args.PropertyName == nameof(BarButtonData.IsEnabled))
                    {
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            capturedPlainButton.IsEnabled = capturedPlainData.IsEnabled;
                        });
                    }
                };
                capturedPlainData.PropertyChanged += plainPropertyChangedHandler;
                plainButton.Unloaded += (_, _) => capturedPlainData.PropertyChanged -= plainPropertyChangedHandler;
                plainButton.Click += SubButton_Click;
                ButtonCompoundState.Wire(plainButton);
                button = plainButton;
            }

            // set the accessible (screen reader) name for the button; fall back to the text if no accessible name was specified
            AutomationProperties.SetName(button, buttonData.AccessibleName ?? buttonData.Text);
            if (this.HeaderTextBlock.Visibility == Visibility.Visible)
            {
                AutomationProperties.SetLabeledBy(button, this.HeaderTextBlock);
            }

            // Sub-button TextBlock configuration applied AFTER the button's template expands
            // (Loaded fires after the first layout pass, by which point the template TextBlock
            // exists in the visual tree and VisualTreeHelper can find it).
            //
            // Three things happen here:
            //   1. MaxLines: XAML default is 1; bump to 2 for groups WITHOUT a header so a button
            //      with explicit '\n' in its text can render two lines. With-header groups keep
            //      MaxLines=1 since the header carries the description and the button label is
            //      short.
            //   2. TextTrimming: set to WordEllipsis ONLY when the button's natural width is over
            //      BarControlMetrics.MaxSubButtonWidth. Set unconditionally, WinUI's TextTrimming would
            //      also paint "..." when the text is barely short (1 logical px of subpixel font
            //      drift between the infinity-Measure pass that sized the column and the actual
            //      constrained-layout pass) -- so a button like "Short" could render as "Shor..."
            //      even though it should fit. By gating on over-cap, under-cap buttons keep
            //      TextTrimming=None: a 1-px shortfall just clips the last fractional pixel of
            //      the rightmost glyph (imperceptible) instead of trimming visible characters.
            //   3. TextWrapping: XAML default is NoWrap. Flip to Wrap ONLY when over the cap AND
            //      MaxLines > 1. NoWrap stays the default for everything else so a column that's
            //      1 logical px tight under per-scale font metric drift doesn't silently wrap to
            //      two lines (the bug that triggered the wrap-fix work in the first place).
            //
            // Capture by value so the closure binds to THIS button instance, not the loop var.
            var capturedButtonForTextConfig = button;
            var effectiveMaxLines = string.IsNullOrEmpty(_data.Header) ? 2 : 1;
            RoutedEventHandler? textConfigLoadedHandler = null;
            textConfigLoadedHandler = (_, _) =>
            {
                capturedButtonForTextConfig.Loaded -= textConfigLoadedHandler;
                var subButtonText = FindDescendantTextBlockByName(capturedButtonForTextConfig, "SubButtonText");
                if (subButtonText is null) { return; }

                if (effectiveMaxLines != subButtonText.MaxLines)
                {
                    subButtonText.MaxLines = effectiveMaxLines;
                }

                // Use the button's DesiredSize.Width (post-Loaded the layout pass has run, so
                // this is the accurate natural width INCLUDING padding + border, which is what
                // BarControlMetrics.MaxSubButtonWidth is measured against -- it's the column-width cap,
                // not a content-width cap).
                if (capturedButtonForTextConfig.DesiredSize.Width > BarControlMetrics.MaxSubButtonWidth)
                {
                    subButtonText.TextTrimming = TextTrimming.WordEllipsis;
                    if (effectiveMaxLines > 1)
                    {
                        subButtonText.TextWrapping = TextWrapping.Wrap;
                    }
                }
            };
            button.Loaded += textConfigLoadedHandler;

            ToolTipService.SetToolTip(button, buttonData.Tooltip);

            // leave a tiny gap between adjacent sub-buttons along the layout axis while preserving rounded outer corners
            bool isFirst = (i == 0);
            bool isLast = (i == _data.Buttons.Count - 1);
            button.Margin = effectiveSubButtonOrientation == Orientation.Horizontal
                ? new Thickness(
                    isFirst ? 0 : BarControlMetrics.ButtonInnerMargin,
                    0,
                    isLast ? 0 : BarControlMetrics.ButtonInnerMargin,
                    0)
                : new Thickness(
                    0,
                    isFirst ? 0 : BarControlMetrics.ButtonInnerMargin,
                    0,
                    isLast ? 0 : BarControlMetrics.ButtonInnerMargin);

            // stash the sub-button data in the button's `Tag` property so the Click handler can retrieve it without a separate dictionary
            button.Tag = buttonData;

            if (effectiveSubButtonOrientation == Orientation.Horizontal)
            {
                this.ButtonsContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(button, i);
            }
            else // effectiveSubButtonOrientation == Orientation.Vertical
            {
                this.ButtonsContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(button, i);
            }
            this.ButtonsContainer.Children.Add(button);

            _subButtons.Add(button);
        }

        this.ApplyCornerRadii(effectiveSubButtonOrientation);
        this.RefreshUniformHorizontalSizing(effectiveSubButtonOrientation);
        this.ApplyButtonsContainerBottomMargin();

        // wire up inc/dec keyboard shortcuts if the data declared them
        if (_data.IncDecShortcuts is not null)
        {
            this.EnableIncDecKeyboardShortcuts(
                decrementButtonIndex: _data.IncDecShortcuts.DecrementButtonIndex,
                incrementButtonIndex: _data.IncDecShortcuts.IncrementButtonIndex);
        }
    }

    // Sets the ButtonsContainer's bottom margin based on the BAR's orientation. In horizontal
    // bar mode, a 3-logical-px bottom margin visually balances the font's reserved ascender
    // area at the top of the header (which we can't reduce without risking clipping of accent
    // marks like é, ü, ô, etc.) -- the pair keeps the item's content visually vertically
    // centered. In vertical bar mode, items stack vertically with BarItemsPanel.Spacing
    // between them; any per-item bottom margin there would multiply across stacked items and
    // push later items off the bar's available length, so we use 0.
    private void ApplyButtonsContainerBottomMargin()
    {
        this.ButtonsContainer.Margin = (_orientation == Orientation.Horizontal)
            ? BarControlMetrics.ButtonContainerBottomMargin_Horizontal
            : new Thickness(0);
    }

    // Re-lays out the EXISTING _subButtons in the current orientation without rebuilding them.
    // Touches only the orientation-dependent attributes: ColumnDefinitions/RowDefinitions on
    // ButtonsContainer, each child's Grid.Column/Grid.Row + Margin, and corner radii. Leaves
    // ToggleButton.IsChecked, the compound-state tracker (in-progress visual, pointer state),
    // event handlers, automation properties, and tooltips untouched -- so rotating the bar
    // mid-action preserves the in-progress visual on whichever button is currently running.
    // Precondition: _data is non-null and _subButtons matches the current ButtonsContainer
    // children (both are maintained by ApplyData on every rebuild).
    private void ReorientButtonsContainer()
    {
        if (_data is null || _subButtons.Count == 0)
        {
            return;
        }

        var effectiveSubButtonOrientation = _data.AlwaysHorizontalSubButtons
            ? Orientation.Horizontal
            : _orientation;

        // clear any Grid.Column / Grid.Row that was set in the previous orientation; each child
        // gets re-positioned below. Without this, a button that was previously at row N would
        // still report Grid.Row=N after we switch to horizontal layout (where rows are unused).
        foreach (var subButton in _subButtons)
        {
            Grid.SetColumn(subButton, 0);
            Grid.SetRow(subButton, 0);
        }
        this.ButtonsContainer.ColumnDefinitions.Clear();
        this.ButtonsContainer.RowDefinitions.Clear();

        // vertical: one Auto-width column so the Grid sizes to the widest sub-button; the
        // buttons' HorizontalAlignment=Stretch (set in their Style) then makes them render at
        // that uniform width. Mirrors the same line in ApplyData.
        if (effectiveSubButtonOrientation == Orientation.Vertical)
        {
            this.ButtonsContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (int i = 0; i < _subButtons.Count; i++)
        {
            var subButton = _subButtons[i];

            bool isFirst = (i == 0);
            bool isLast = (i == _subButtons.Count - 1);
            subButton.Margin = effectiveSubButtonOrientation == Orientation.Horizontal
                ? new Thickness(
                    isFirst ? 0 : BarControlMetrics.ButtonInnerMargin,
                    0,
                    isLast ? 0 : BarControlMetrics.ButtonInnerMargin,
                    0)
                : new Thickness(
                    0,
                    isFirst ? 0 : BarControlMetrics.ButtonInnerMargin,
                    0,
                    isLast ? 0 : BarControlMetrics.ButtonInnerMargin);

            if (effectiveSubButtonOrientation == Orientation.Horizontal)
            {
                this.ButtonsContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(subButton, i);
            }
            else
            {
                this.ButtonsContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(subButton, i);
            }
        }

        this.ApplyCornerRadii(effectiveSubButtonOrientation);
        this.RefreshUniformHorizontalSizing(effectiveSubButtonOrientation);
        this.ApplyButtonsContainerBottomMargin();

        // Queue a post-layout re-assertion of each sub-button's compound state. The orientation
        // change causes a layout pass that can synthetically clear IsPointerOver (and indirectly
        // wipe other CommonStates setters) via paths that don't fire routed PointerExited events;
        // the IsPressed DP callback in CompoundStatePointerWiring covers the IsPressed path, but
        // IsPointerOver-driven and other layout-driven resets need this fallback. Deferring via
        // TryEnqueue ensures the refresh runs after the layout pass settles -- calling it inline
        // would re-assert BEFORE the built-in stomp and leave the bug intact. Snapshot the button
        // list so a concurrent Data reassignment that clears _subButtons doesn't strand the
        // queued lambda iterating an empty list (and conversely doesn't try to refresh a button
        // that's already been replaced).
        var subButtonsSnapshot = _subButtons.ToArray();
        this.DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var subButton in subButtonsSnapshot)
            {
                CompoundStatePointerWiring.RefreshVisualState(subButton);
            }
        });
    }

    private void ApplyCornerRadii(Orientation effectiveSubButtonOrientation)
    {
        if (_subButtons.Count == 0)
        {
            return;
        }

        if (_subButtons.Count == 1)
        {
            // sole sub-button: round all 4 corners
            _subButtons[0].CornerRadius = new CornerRadius(BarControlMetrics.ButtonCornerRadius);
            return;
        }

		// NOTE: at this point, we know we have at least 2 sub-buttons

        // CornerRadius is (topLeft, topRight, bottomRight, bottomLeft) -- clockwise from top-left
        if (effectiveSubButtonOrientation == Orientation.Horizontal)
        {
            // first sub-button rounds its left (leading) corners
            _subButtons[0].CornerRadius = new CornerRadius(
                BarControlMetrics.ButtonCornerRadius, 0, 0, BarControlMetrics.ButtonCornerRadius);

            // middle sub-buttons: no rounding
            for (int i = 1; i < _subButtons.Count - 1; i++)
            {
                _subButtons[i].CornerRadius = new CornerRadius(0);
            }

            // last sub-button rounds its right (trailing) corners
            _subButtons[^1].CornerRadius = new CornerRadius(
                0, BarControlMetrics.ButtonCornerRadius, BarControlMetrics.ButtonCornerRadius, 0);
        }
        else
        {
            // first sub-button rounds its top corners
            _subButtons[0].CornerRadius = new CornerRadius(
                BarControlMetrics.ButtonCornerRadius, BarControlMetrics.ButtonCornerRadius, 0, 0);

            // middle sub-buttons: no rounding
            for (int i = 1; i < _subButtons.Count - 1; i++)
            {
                _subButtons[i].CornerRadius = new CornerRadius(0);
            }

            // last sub-button rounds its bottom corners
            _subButtons[^1].CornerRadius = new CornerRadius(
                0, 0, BarControlMetrics.ButtonCornerRadius, BarControlMetrics.ButtonCornerRadius);
        }
    }

    // For horizontal + StretchToLargest only: measures each sub-button to find the widest natural
    // width, then sets each Grid column to that fixed pixel width so all sub-buttons render at
    // uniform width. For horizontal + AutoSize or any vertical orientation, this is a no-op.
    // (Vertical's uniform-width behavior is provided by the single Auto-width column + buttons'
    // HorizontalAlignment=Stretch combination configured in ApplyData, with no measurement needed.)
    //
    // Idempotent; can be called multiple times. Called from ApplyData after all sub-buttons have
    // been added, and again from BarMultiButtonControl_Loaded once the control is in the visual
    // tree (in case the ApplyData-time measurements were inaccurate while the control was detached).
    private void RefreshUniformHorizontalSizing(Orientation? effectiveSubButtonOrientationOverride = null)
    {
        if (_data is null || _subButtons.Count == 0)
        {
            return;
        }
        // caller (ApplyData) supplies the effective orientation. When invoked from the Loaded
        // handler we don't have it cached on the control, so recompute it from _data + _orientation.
        var effectiveOrientation = effectiveSubButtonOrientationOverride
            ?? (_data.AlwaysHorizontalSubButtons ? Orientation.Horizontal : _orientation);
        if (effectiveOrientation != Orientation.Horizontal)
        {
            return;
        }
        if (_data.SizingMode != MultiButtonSizingMode.StretchToLargest)
        {
            return;
        }

        // measure each sub-button at its natural width and find the widest.
        // NOTE: use DesiredSize.Width as-is (which includes the button's Margin contribution).
        // The column gets this full width; when the button is placed inside, its margin reduces
        // the content area back to the natural content width -- exactly enough for the text.
        // If we subtracted the margin here, the column would be too small by `margin` logical px
        // and the text would wrap/clip just barely on the widest button.
        double maxButtonDesiredWidth = 0;
        foreach (var subButton in _subButtons)
        {
            subButton.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            if (subButton.DesiredSize.Width > maxButtonDesiredWidth) { maxButtonDesiredWidth = subButton.DesiredSize.Width; }
        }

        // set each column to that fixed pixel width so the Grid distributes them uniformly.
        // NOTE: we deliberately do NOT widen columns to match a wider header here -- the
        // sub-buttons stay at their natural width and render centered (ButtonsContainer is
        // HorizontalAlignment=Center). The header-driven group width is established at the
        // outer level via MeasureForOrientation; inside the group, narrow-glyph buttons
        // (e.g. Read Selected's ▶ ■) should look narrow, matching v1.x's appearance.
        //
        // Math.Ceiling handles fractional-pixel rounding -- raw DesiredSize.Width can be a
        // fractional logical-px value at non-100% scales, and a fractional GridLength causes
        // sub-pixel rendering artifacts. With TextWrapping="NoWrap" on the inner TextBlock,
        // there's no risk of wrap-induced height growth even if the column is exactly the
        // natural text width, so no slack is added here. MeasureForOrientation's StretchToLargest
        // branch uses the same Math.Min(Math.Ceiling, MaxSubButtonWidth) formula -- both sides
        // MUST match or the bar's reported width and painted width disagree (trailing items
        // overflow off the right side of a horizontal bar).
        //
        // Cap at BarControlMetrics.MaxSubButtonWidth so an unusually long label doesn't widen the entire
        // bar group indefinitely. When the cap kicks in for a button whose effective MaxLines>1
        // (no-header groups), ApplyData's Loaded handler flips that button's TextWrapping to Wrap
        // so the label wraps within the capped width; otherwise the label is clipped at the cap.
        double columnWidth = System.Math.Min(System.Math.Ceiling(maxButtonDesiredWidth), BarControlMetrics.MaxSubButtonWidth);
        foreach (var columnDefinition in this.ButtonsContainer.ColumnDefinitions)
        {
            columnDefinition.Width = new GridLength(columnWidth);
        }
    }

    private async void SubButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ButtonBase button)
        {
            return;
        }
        if (button.Tag is not BarButtonData subData)
        {
            return;
        }
        if (_subButtonsWithActionInProgress.Contains(button))
        {
            return;
        }

        var toggleButton = sender as ToggleButton;
        bool? postClickIsChecked = toggleButton?.IsChecked;

        var action = subData.Action;
        if (action is null)
        {
            return;
        }

        // re-entry during the action is prevented by _subButtonsWithActionInProgress (checked at
        // the top of this handler), so we don't gate clicks via IsHitTestVisible. Doing so would
        // suppress PointerEntered/PointerExited on the button for the duration of the action; if
        // the user moved the pointer off the button while it was running, tracker.IsPointerOver
        // would stay stale (true) and SetInProgressVisual(None) would compute "PointerOver"
        // instead of "Normal" -- leaving the button stuck in the hover background, visually
        // indistinguishable from the InProgress pressed background. While InProgressVisual ==
        // Visible, ComputeStateName always returns "InProgress" regardless of pointer state, so
        // letting pointer events flow during the action causes no visual flicker.
        _subButtonsWithActionInProgress.Add(button);

        bool actionSucceeded;
        try
        {
            var result = await DelayedInProgressVisual.RunAsync(button, () => action.Invoke(subData.ActionTag, postClickIsChecked));
            actionSucceeded = result.IsSuccess;
        }
        catch (Exception ex)
        {
            // an action throwing is treated as failure -- the system state didn't change in any
            // well-defined way, so the toggle (if any) should revert
            Debug.WriteLine($"[BarItem] {subData.ActionTag} threw: {ex}");
            actionSucceeded = false;
        }
        finally
        {
            _subButtonsWithActionInProgress.Remove(button);
        }

        // mirror the outcome onto the toggle and the backing data. Data updates happen only on
        // action completion (not on the immediate Checked/Unchecked event from the framework's
        // toggle) so that an in-flight real-time event listener -- e.g. one observing an external
        // dark-mode change -- can update BarButtonData.IsChecked during the action without us
        // blindly overwriting it. On success we write postClickIsChecked (our action committed
        // that state); on failure we revert the visual toggle and leave data alone (data was never
        // changed by this click, so it still reflects the unchanged system state). No-op for
        // non-toggle buttons (toggleButton == null).
        if (toggleButton is not null && postClickIsChecked.HasValue)
        {
            if (actionSucceeded)
            {
                subData.IsChecked = postClickIsChecked.Value;
            }
            else
            {
                toggleButton.IsChecked = !postClickIsChecked.Value;
            }
        }
    }

    /// <summary>
    /// Wires the minus/plus (and OemMinus/OemPlus) keys to invoke the sub-buttons at the given indices.
    /// Throws <see cref="InvalidOperationException"/> if the group does not contain exactly two sub-buttons,
    /// <see cref="ArgumentOutOfRangeException"/> if either index is outside the sub-button range, and
    /// <see cref="ArgumentException"/> if the two indices are equal.
    /// </summary>
    /// <param name="decrementButtonIndex">Index of the sub-button the minus key should invoke.</param>
    /// <param name="incrementButtonIndex">Index of the sub-button the plus key should invoke.</param>
    public void EnableIncDecKeyboardShortcuts(int decrementButtonIndex, int incrementButtonIndex)
    {
        if (_subButtons.Count != 2)
        {
            throw new InvalidOperationException(
                $"EnableIncDecKeyboardShortcuts requires exactly 2 sub-buttons; current count is {_subButtons.Count}.");
        }
        if (decrementButtonIndex < 0 || decrementButtonIndex >= _subButtons.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(decrementButtonIndex));
        }
        if (incrementButtonIndex < 0 || incrementButtonIndex >= _subButtons.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(incrementButtonIndex));
        }
        if (decrementButtonIndex == incrementButtonIndex)
        {
            throw new ArgumentException("decrementButtonIndex and incrementButtonIndex must differ.");
        }

        _decrementButtonIndex = decrementButtonIndex;
        _incrementButtonIndex = incrementButtonIndex;

        if (_incDecShortcutsEnabled)
        {
            return;
        }

        this.KeyDown += BarMultiButtonControl_KeyDown_IncDec;
        _incDecShortcutsEnabled = true;
    }

	// NOTE: This function is called for keydown events; it only handles IncDev operations
    private void BarMultiButtonControl_KeyDown_IncDec(object sender, KeyRoutedEventArgs e)
    {
        // as OemMinus and OemPlus are not named in Windows.System.VirtualKey, we import the raw values here
        const int VkOemMinus = 189;
        const int VkOemPlus = 187;

        int clickIndex = -1;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Subtract:
                clickIndex = _decrementButtonIndex;
                break;
            case Windows.System.VirtualKey.Add:
                clickIndex = _incrementButtonIndex;
                break;
            default:
                if ((int)e.Key == VkOemMinus)
                {
                    clickIndex = _decrementButtonIndex;
                }
                else if ((int)e.Key == VkOemPlus)
                {
                    clickIndex = _incrementButtonIndex;
                }
                break;
        }

        if (clickIndex < 0 || clickIndex >= _subButtons.Count)
        {
            return;
        }

        var target = _subButtons[clickIndex];

        // invoke the sub-button via its automation peer (trigging the Click event)
        AutomationPeer peer = target is ToggleButton toggleButton
            ? new ToggleButtonAutomationPeer(toggleButton)
            : new ButtonAutomationPeer((Button)target);

        (peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();

        e.Handled = true;
    }
}
