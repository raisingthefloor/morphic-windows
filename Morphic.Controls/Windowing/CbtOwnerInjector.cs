// Copyright 2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/main/LICENSE.txt
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

using System;
using System.Runtime.InteropServices;

namespace Morphic.Controls.Windowing;

// Installs a temporary, thread-local WH_CBT hook that injects an owner HWND into the next
// top-level window created on the calling thread. Used to give a WinUI 3 Window an owner
// at the moment of creation rather than after the fact.
//
// Why this exists: under UIPI (User Interface Privilege Isolation, which is enforced for
// uiaccess=true processes), the system blocks SetWindowLongPtr(GWLP_HWNDPARENT) on existing
// top-level windows with ERROR_INVALID_PARAMETER. The owner can only be set at the moment
// of CreateWindow*. WinUI 3's Window class doesn't expose a constructor parameter for the
// owner, so the workaround is to hook the CBT_CREATEWND notification and rewrite the
// CREATESTRUCT before the OS uses it.
//
// Usage:
//   var dummyOwner = new DummyWindow();
//   IntPtr dummyHwnd; unsafe { dummyHwnd = (IntPtr)dummyOwner.hwnd.Value; }
//   MorphicBarWindow bar;
//   using (CbtOwnerInjector.For(dummyHwnd))
//   {
//       bar = new MorphicBarWindow();   // CBT hook injects dummyHwnd as owner
//   }
//   // Keep dummyOwner alive as long as bar (the owner HWND must remain valid).
//
// The hook is thread-local and only modifies the FIRST top-level window (CREATESTRUCT with
// hwndParent == NULL) created during its lifetime; once that injection succeeds, subsequent
// CBT notifications pass through unchanged. This keeps the impact tightly scoped: nested
// WinUI infrastructure windows (child controls, XAML islands) and other unrelated windows
// created while the hook is installed are unaffected.
public sealed class CbtOwnerInjector : IDisposable
{
    private const int WH_CBT = 5;
    private const int HCBT_CREATEWND = 3;

    // CREATESTRUCTW from winuser.h. Layout must match exactly so unsafe pointer access
    // reads the right field offsets. Strings (lpszName, lpszClass) are LPCWSTR; we declare
    // them as IntPtr because we only need to read/write hwndParent here.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREATESTRUCTW
    {
        public IntPtr lpCreateParams;
        public IntPtr hInstance;
        public IntPtr hMenu;
        public IntPtr hwndParent;
        public int cy;
        public int cx;
        public int y;
        public int x;
        public int style;
        public IntPtr lpszName;
        public IntPtr lpszClass;
        public int dwExStyle;
    }

    // CBT_CREATEWNDW from winuser.h. lpcs is a pointer to the CREATESTRUCTW the OS is about
    // to use for CreateWindowEx; mutating it here changes the actual window created.
    [StructLayout(LayoutKind.Sequential)]
    private struct CBT_CREATEWNDW
    {
        public IntPtr lpcs;
        public IntPtr hwndInsertAfter;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
        {
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }
        else
        {
            return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
    }

    private const int GWLP_HWNDPARENT = -8;

    private readonly IntPtr _ownerHwnd;
    private readonly HookProc _hookDelegate;   // GC-pinned via instance field
    private IntPtr _hookHandle;
    private bool _injectionDone;

    private CbtOwnerInjector(IntPtr ownerHwnd)
    {
        _ownerHwnd = ownerHwnd;
        _hookDelegate = HookCallback;
        // dwThreadId = current thread => thread-local hook, doesn't affect other threads.
        // hMod = IntPtr.Zero is documented as valid when the hook proc is in the same module.
        _hookHandle = SetWindowsHookExW(WH_CBT, _hookDelegate, IntPtr.Zero, GetCurrentThreadId());
    }

    // Installs the hook for the duration of the returned IDisposable. Caller is responsible
    // for keeping the owner HWND valid for as long as it's set as a window's owner (typically
    // the lifetime of the owned window).
    public static CbtOwnerInjector For(IntPtr ownerHwnd)
    {
        return new CbtOwnerInjector(ownerHwnd);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private static string GetClassNameFromPointer(IntPtr lpszClass)
    {
        if (((long)lpszClass & ~0xFFFFL) == 0)
        {
            return $"Atom:#{(ushort)lpszClass}";
        }
        return Marshal.PtrToStringUni(lpszClass) ?? "";
    }

    private static string GetWindowNameFromPointer(IntPtr lpszName)
    {
        if (lpszName == IntPtr.Zero)
        {
            return "";
        }
        return Marshal.PtrToStringUni(lpszName) ?? "";
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HCBT_CREATEWND)
        {
            unsafe
            {
                CBT_CREATEWNDW* pCbt = (CBT_CREATEWNDW*)lParam;
                CREATESTRUCTW* pCs = (CREATESTRUCTW*)pCbt->lpcs;
                
                string className = GetClassNameFromPointer(pCs->lpszClass);
                string windowName = GetWindowNameFromPointer(pCs->lpszName);
                
                System.Diagnostics.Debug.WriteLine($"[CbtOwnerInjector] HCBT_CREATEWND: Class='{className}', Name='{windowName}', hwndParent={pCs->hwndParent:X}, style={pCs->style:X}, dwExStyle={pCs->dwExStyle:X}, injectionDone={_injectionDone}");

                if (!_injectionDone && className == "WinUIDesktopWin32WindowClass" && pCs->hwndParent == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"[CbtOwnerInjector] Calling SetWindowLongPtr(GWLP_HWNDPARENT) to set owner HWND {_ownerHwnd:X} on window handle {wParam:X}");
                    
                    Marshal.SetLastPInvokeError(0);
                    IntPtr prevOwner = SetWindowLongPtr(wParam, GWLP_HWNDPARENT, _ownerHwnd);
                    int win32Error = Marshal.GetLastWin32Error();
                    
                    System.Diagnostics.Debug.WriteLine($"[CbtOwnerInjector] SetWindowLongPtr result: prevOwner={prevOwner:X}, GetLastError={win32Error}");
                    
                    _injectionDone = true;
                }
            }
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
