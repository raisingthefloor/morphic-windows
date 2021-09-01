using Morphic.Settings.SettingsHandlers;

namespace Morphic.ManualTester
{
    interface IManualControlEntry
    {
        public bool isChanged();
        public void CaptureSetting();
        public void SetLoading();
        public void ReadCapture(Values vals);
        public void ApplySetting();
    }
}
