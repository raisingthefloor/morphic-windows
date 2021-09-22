namespace Morphic.Settings.SettingsHandlers
{
    using System.Collections;
    using System.Collections.Generic;

    public class Values : IEnumerable<KeyValuePair<Setting, object>>
    {
        private Dictionary<Setting, object?> values = new Dictionary<Setting, object?>();

        private Dictionary<Setting, ValueType> types = new Dictionary<Setting, ValueType>();

        public Values() {}

        public Values(Setting setting, object? value)
        {
            this.values.Add(setting, value);
            this.types.Add(setting, ValueType.Unknown);
        }

        public Values(Setting setting, object? value, ValueType type)
        {
            this.values.Add(setting, value);
            this.types.Add(setting, type);
        }

        public void Add(Setting setting, object? value)
        {
            this.values[setting] = value;
            this.types[setting] = ValueType.Unknown;
        }

        public void Add(Setting setting, object? value, ValueType type)
        {
            this.values[setting] = value;
            this.types[setting] = type;
        }

        public object? Get(Setting setting)
        {
            return this.values[setting];
        }

        public ValueType GetType(Setting setting)
        {
            return this.types[setting];
        }

        public bool Contains(Setting setting)
        {
            return this.values.ContainsKey(setting);
        }

        IEnumerator<KeyValuePair<Setting, object?>> IEnumerable<KeyValuePair<Setting, object?>>.GetEnumerator()
        {
            return this.values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.values).GetEnumerator();
        }

        public enum ValueType   //designates the nature of the value returned
        {
            UserSetting,    //setting saved to user data
            Hardcoded,  //setting that is a hardcoded default we located
            NotFound,   //setting we were unable to locate a value for (likely set to default)
            Unknown
        }
    }

    public struct NoValue
    {
    }
}
