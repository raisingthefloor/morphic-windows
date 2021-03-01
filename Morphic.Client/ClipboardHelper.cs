using Morphic.Windows.Native.Speech;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Morphic.Client
{
    public class ClipboardHelper
    {
        public static Dictionary<string, object?>? GetClipboard()
        {
            var clipboardData = Clipboard.GetDataObject();

            if (clipboardData != null)
            {
                return clipboardData.GetFormats()
                    .ToDictionary(format => format,
                                  format => (object?)clipboardData.GetData(format, false));
            }

            return default;
        }

        public static async Task<string> GetSelectedText()
        {
            var clipboardData = GetClipboard();

            try
            {
                Clipboard.Clear();

                await SelectionReader.Default.GetSelectedText(System.Windows.Forms.SendKeys.SendWait);
                string text = Clipboard.GetText();

                return text;
            }
            finally
            {
                SetClipboard(clipboardData);
            }
        }

        public static void SetClipboard(Dictionary<string, object?>? clipboardData)
        {
            Clipboard.Clear();
            clipboardData?.Where(kv => kv.Value != null).ToList()
                .ForEach(kv => Clipboard.SetData(kv.Key, kv.Value));
        }
    }
}
