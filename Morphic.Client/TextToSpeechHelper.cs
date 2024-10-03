using Morphic.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading.Tasks;

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

        public async Task<MorphicResult<MorphicUnit, MorphicUnit>> Say(string text)
        {
            Stop();

            var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
            try
            {
                Windows.Media.SpeechSynthesis.SpeechSynthesisStream stream;
                try
                {
                    stream = await synth.SynthesizeTextToStreamAsync(text);
                }
                catch (Exception ex)
                {
                    Debug.Assert(false, "Could not create speech synthesizer stream; ex: " + ex.Message);
                    return MorphicResult.ErrorResult();
                }
                //
                try
                {
                    // NOTE: we cannot use the speech synthesizer directly using SpeakAsync/SpeakAsyncCancelAll because Windows.Speech.SpeechSynthesis and these corresponding methods are not supported in .NET 5 (i.e. only in .NET Framework)
                    _speechPlayer.Stream = stream.AsStream();

                    // NOTE: Play() loads and plays the sound in a new thread asynchronously; we could await on LoadAsync and then call Play, but that could create a contention if
                    // Stop() was called before the LoadAsync callback completed and therefore before Play() got called 
                    _speechPlayer.Play();
                }
                catch (Exception ex)
                {
                    Debug.Assert(false, "Could not capture/play text stream; ex: " + ex.Message);
                    return MorphicResult.ErrorResult();
                }
                finally
                {
                    // NOTE: this 'always manually dispose' pattern may or may not be required; this is being kept based on the original design of this class.
                    stream.Dispose();
                }
            }
            finally
            {
                // NOTE: this 'always manually dispose' pattern may or may not be required; this is being kept based on the original design of this class.
                synth.Dispose();
            }

            return MorphicResult.OkResult();
        }

        public void Dispose()
        {
            Stop();

            _speechPlayer.Dispose();
        }
    }
}
