﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This file was generated by cswinrt.exe version 2.0.1.221115.1
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
namespace WinRT
{

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_object__string_ : EventSource<global::Windows.Foundation.TypedEventHandler<object, string>>
    {
        internal _EventSource_global__Windows_Foundation_TypedEventHandler_object__string_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, out WinRT.EventRegistrationToken, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<object, string> del) =>
        global::ABI.Windows.Foundation.TypedEventHandler<object, string>.CreateMarshaler2(del);

        protected override State CreateEventState() =>
        new EventState(_obj.ThisPtr, _index);

        private sealed class EventState : State
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override System.Delegate GetEventInvoke()
            {
                global::Windows.Foundation.TypedEventHandler<object, string> invoke = (object sender, string args) =>
                {
                    var localDel = (global::Windows.Foundation.TypedEventHandler<object, string>)del;
                    if (localDel == null)
                    {
                        return;
                    }
                    localDel.Invoke(sender, args);
                };
                return invoke;
            }
        }
    }

    internal sealed unsafe class _EventSource_global__SystemSettings_DataModel_SettingsEnvironmentChangedHandler : EventSource<global::SystemSettings.DataModel.SettingsEnvironmentChangedHandler>
    {
        internal _EventSource_global__SystemSettings_DataModel_SettingsEnvironmentChangedHandler(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, out WinRT.EventRegistrationToken, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
        }

        protected override ObjectReferenceValue CreateMarshaler(global::SystemSettings.DataModel.SettingsEnvironmentChangedHandler del) =>
        global::ABI.SystemSettings.DataModel.SettingsEnvironmentChangedHandler.CreateMarshaler2(del);

        protected override State CreateEventState() =>
        new EventState(_obj.ThisPtr, _index);

        private sealed class EventState : State
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override System.Delegate GetEventInvoke()
            {
                global::SystemSettings.DataModel.SettingsEnvironmentChangedHandler invoke = (global::SystemSettings.DataModel.ISettingsEnvironmentDatabase sender, string variableName) =>
                {
                    var localDel = (global::SystemSettings.DataModel.SettingsEnvironmentChangedHandler)del;
                    if (localDel == null)
                    {
                        return;
                    }
                    localDel.Invoke(sender, variableName);
                };
                return invoke;
            }
        }
    }

}