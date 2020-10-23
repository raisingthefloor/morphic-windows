namespace Morphic.Settings.SolutionsRegistry.SettingsHandlers
{
    using System.Collections;
    using System.Collections.Generic;

    public class Values : IEnumerable<KeyValuePair<Setting, object>>
    {
        private Dictionary<Setting, object?> values = new Dictionary<Setting, object?>();

        public Values() {}

        public Values(Setting setting, object? value)
        {
            this.values.Add(setting, value);
        }

        public void Add(Setting setting, object? value)
        {
            this.values[setting] = value;
        }

        public object? Get(Setting setting)
        {
            return this.values[setting];
        }

        IEnumerator<KeyValuePair<Setting, object?>> IEnumerable<KeyValuePair<Setting, object?>>.GetEnumerator()
        {
            return this.values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.values).GetEnumerator();
        }
    }
}
