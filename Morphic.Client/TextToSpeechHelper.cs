using Morphic.Core;
using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;

namespace Morphic.Client
{
    public class TextToSpeechHelper : IDisposable
    {
        private static readonly Lazy<TextToSpeechHelper> _lazyTextToSpeechHelper = new Lazy<TextToSpeechHelper>(() => new TextToSpeechHelper());
        public static TextToSpeechHelper Instance => _lazyTextToSpeechHelper.Value;

        private readonly SoundPlayer _speechPlayer;

        private TextToSpeechHelper() 
        {
            _speechPlayer = new SoundPlayer();
        }

        public void Stop()
        {
            _speechPlayer.Stop();
        }

        public async Task<IMorphicResult> Say(string text)
        {
            Stop();

            using var synth = new SpeechSynthesizer();
            using var stream = await synth.SynthesizeTextToStreamAsync(text);

            _speechPlayer.Stream = stream.AsStream();

            _speechPlayer.Play();

            return IMorphicResult.SuccessResult;
        }

        public void Dispose()
        {
            Stop();

            _speechPlayer.Dispose();
        }
    }
}
