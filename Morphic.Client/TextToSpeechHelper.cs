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

            // NOTE: we cannot use the speech synthesizer directly using SpeakAsync/SpeakAsyncCancelAll because Windows.Speech.SpeechSynthesis and these corresponding methods are not supported in .NET 5 (i.e. only in .NET Framework)
            _speechPlayer.Stream = stream.AsStream();

            // NOTE: Play() loads and plays the sound in a new thread asynchronously; we could await on LoadAsync and then call Play, but that could create a contention if
            // Stop() was called before the LoadAsync callback completed and therefore before Play() got called 
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
