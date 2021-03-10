namespace Morphic.Settings.SettingsHandlers
{
    using Morphic.Core;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;

    /// <summary>
    /// A settings handler which uses methods decorated with [Setter] or [Getter] attributes to apply settings.
    /// </summary>
    public abstract class FixedSettingsHandler : SettingsHandler
    {
        protected delegate Task<object?> Getter(Setting setting);
        protected delegate Task<bool> Setter(Setting setting, object? newValue);
        protected delegate bool Listener(Setting setting, bool add);

        private readonly Dictionary<string, Setter> setters = new Dictionary<string, Setter>();
        private readonly Dictionary<string, Getter> getters = new Dictionary<string, Getter>();
        private readonly Dictionary<string, Listener> listeners = new Dictionary<string, Listener>();

        protected FixedSettingsHandler()
        {
            // Find the setting getter and setters.
            foreach (MethodInfo method in this.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                GetterAttribute? getAttr = method.GetCustomAttribute<GetterAttribute>();
                if (getAttr != null)
                {
                    if (Delegate.CreateDelegate(typeof(Getter), this, method, false) is Getter d)
                    {
                        this.getters.Add(getAttr.SettingName, d);
                    }
                    continue;
                }

                SetterAttribute? setAttr = method.GetCustomAttribute<SetterAttribute>();
                if (setAttr != null)
                {
                    if (Delegate.CreateDelegate(typeof(Setter), this, method, true) is Setter d)
                    {
                        this.setters.Add(setAttr.SettingName, d);
                    }
                    continue;
                }

                ListenerAttribute? listenAttr = method.GetCustomAttribute<ListenerAttribute>();
                if (listenAttr != null)
                {
                    if (Delegate.CreateDelegate(typeof(Listener), this, method, true) is Listener d)
                    {
                        this.listeners.Add(listenAttr.SettingName, d);
                    }
                }
            }
        }

        /// <summary>Gets the values of the given settings.</summary>
		// NOTE: we return both success/failure and a list of results so that we can return partial results in case of partial failure
        public override async Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;

            Values values = new Values();
            foreach (Setting setting in settings)
            {
                object? value;
                var getResult = await this.GetAsync(setting);
                if (getResult.IsSuccess == true)
                {
                    value = getResult.Value;

                    if (!(value is NoValue))
                    {
                        values.Add(setting, value);
                    }
                }
                else
                {
                    success = false;
                    // not captured; skip to the next setting
                    continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        /// <summary>Gets the value of a setting.</summary>
        public override async Task<IMorphicResult<object?>> GetAsync(Setting setting)
        {
            if (this.getters.TryGetValue(setting.Name, out Getter? getter))
            {
                try
                {
                    var result = await getter(setting);
                    return IMorphicResult<object?>.SuccessResult(result);
                }
                catch
                {
                    return IMorphicResult<object?>.ErrorResult();
                }
            }

            return IMorphicResult<object?>.SuccessResult(new NoValue());
        }

        /// <summary>Sets the values of settings.</summary>
        public override async Task<IMorphicResult> SetAsync(SettingGroup settingGroup, Values values)
        {
            bool success = true;
            foreach ((Setting? setting, object? value) in values)
            {
                var settingSetResult = await this.SetAsync(setting, value);
                success = success && settingSetResult.IsSuccess;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        /// <summary>Set the value of a setting.</summary>
        public override async Task<IMorphicResult> SetAsync(Setting setting, object? newValue)
        {
            var success = true; 

            if (this.setters.TryGetValue(setting.Name, out Setter? setter))
            {
                try
                {
                    // NOTE: setters return a bool; should we be capturing this as success/failure?  Moving forward, look at having them return IMorphicResult to be clear
                    await setter(setting, newValue);
                }
                catch
                {
                    success = false;
                }
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private bool HandleListener(Setting setting, bool add)
        {
            if (this.listeners.TryGetValue(setting.Name, out Listener? listener))
            {
                return listener(setting, add);
            }

            return false;
        }

        protected override bool OnSettingListenerRequired(Setting setting)
        {
            return this.HandleListener(setting, true);
        }

        protected override void OnSettingListenerNotRequired(Setting setting)
        {
            this.HandleListener(setting, false);
        }

        [AttributeUsage(AttributeTargets.Method)]
        protected class SetterAttribute : Attribute
        {
            public string SettingName { get; }

            public SetterAttribute(string settingName)
            {
                this.SettingName = settingName;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        protected class GetterAttribute : Attribute
        {
            public string SettingName { get; }

            public GetterAttribute(string settingName)
            {
                this.SettingName = settingName;
            }
        }

        [AttributeUsage(AttributeTargets.Method)]
        protected class ListenerAttribute : Attribute
        {
            public string SettingName { get; }

            public ListenerAttribute(string settingName)
            {
                this.SettingName = settingName;
            }
        }
    }
}
