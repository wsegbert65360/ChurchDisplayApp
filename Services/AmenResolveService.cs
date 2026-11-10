using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MeltySynth;
using NAudio.Wave;
using Serilog;

namespace ChurchDisplayApp.Services
{
    /// <summary>
    /// Provides a professional musical "Amen" resolve using MeltySynth and NAudio.
    /// This service plays a Plagal Cadence (IV-I) with a piano SoundFont.
    /// </summary>
    public class AmenResolveService : IDisposable
    {
        private readonly string _soundFontPath;
        private MeltySynth.Synthesizer? _synth;
        private WaveOutEvent? _waveOut;
        private SynthSampleProvider? _sampleProvider;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private CancellationTokenSource _cts = new();
        private bool _disposed = false;
        private readonly object _initLock = new();
        private readonly object _synthLock = new();
        private readonly object _ctsLock = new();

        public AmenResolveService(string soundFontPath)
        {
            _soundFontPath = soundFontPath;
        }

        private void Initialize()
        {
            // Thread-safe initialization using double-check locking pattern
            if (_synth != null) return;

            lock (_initLock)
            {
                if (_synth != null) return;

                if (!File.Exists(_soundFontPath))
                {
                    Log.Error("SoundFont file not found at {Path}. Amen resolve will be unavailable.", _soundFontPath);
                    return;
                }

                try
                {
                    var sf = new SoundFont(_soundFontPath);
                    _synth = new MeltySynth.Synthesizer(sf, 44100);
                    _sampleProvider = new SynthSampleProvider(_synth, _synthLock);
                
                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(_sampleProvider);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize synthesizer with SoundFont at {Path}", _soundFontPath);
                    _synth = null;
                }
            }
        }

        /// <summary>
        /// Thread-safe wrapper for sending MIDI messages to the synthesizer.
        /// Prevents audio glitches when the audio render thread and the MIDI
        /// command thread access the synthesizer concurrently.
        /// </summary>
        private void SafeMidi(int channel, int command, int data1, int data2)
        {
            var synth = _synth;
            if (synth == null) return;
            lock (_synthLock) { synth.ProcessMidiMessage(channel, command, data1, data2); }
        }

        private void SafeReset()
        {
            var synth = _synth;
            if (synth == null) return;
            lock (_synthLock) { synth.Reset(); }
        }

        public void Cancel()
        {
            lock (_ctsLock)
            {
                try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            }
            try { _waveOut?.Stop(); } catch { }
        }

        public async Task ExecuteResolveAsync(float volume = 1.0f)
        {
            if (_disposed) return;

            try
            {
                if (!await _semaphore.WaitAsync(0))
                {
                    Log.Information("Amen resolve already in progress, skipping");
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            CancellationToken token;
            lock (_ctsLock)
            {
                if (_disposed)
                {
                    try { _semaphore.Release(); } catch (ObjectDisposedException) { }
                    return;
                }

                try { _cts.Cancel(); } catch (ObjectDisposedException) { }
                try { _cts.Dispose(); } catch (ObjectDisposedException) { }
                _cts = new CancellationTokenSource();
                token = _cts.Token;
            }

            try
            {
                Initialize();
                if (_synth == null || _waveOut == null) return;

                _waveOut.Volume = Math.Clamp(volume, 0.0f, 1.0f);
                _waveOut.Play();

                int C2 = 36; int F2 = 41; int C3 = 48; int E3 = 52; int F3 = 53; int G3 = 55; int A3 = 57;
                int C4 = 60;

                Random rnd = new Random();

                SafeMidi(0, 0xB0, 64, 127);

                SafeMidi(0, 0x90, F2, 60);
                SafeMidi(0, 0x90, F3, 70 + rnd.Next(-5, 6));
                SafeMidi(0, 0x90, A3, 70 + rnd.Next(-5, 6));
                SafeMidi(0, 0x90, C4, 70 + rnd.Next(-5, 6));

                await Task.Delay(2000, token);

                SafeMidi(0, 0x80, F2, 0);
                SafeMidi(0, 0x80, F3, 0);
                SafeMidi(0, 0x80, A3, 0);
                SafeMidi(0, 0x80, C4, 0);

                SafeMidi(0, 0x90, C2, 65);
                SafeMidi(0, 0x90, C3, 85 + rnd.Next(-5, 6));
                SafeMidi(0, 0x90, E3, 85 + rnd.Next(-5, 6));
                SafeMidi(0, 0x90, G3, 85 + rnd.Next(-5, 6));
                SafeMidi(0, 0x90, C4, 85 + rnd.Next(-5, 6));

                await Task.Delay(2500, token);

                for (int vol = 100; vol >= 0; vol -= 5)
                {
                    token.ThrowIfCancellationRequested();
                    SafeMidi(0, 0xB0, 7, vol);
                    await Task.Delay(50, token);
                }

                SafeMidi(0, 0xB0, 64, 0);
                SafeReset();
                SafeMidi(0, 0xB0, 7, 100);

                _waveOut.Stop();
            }
            catch (OperationCanceledException)
            {
                Log.Information("Amen resolve was cancelled");
                try { SafeMidi(0, 0xB0, 7, 100); } catch { }
                try { SafeReset(); } catch { }
                try { _waveOut?.Stop(); } catch { }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Amen resolve execution failed");
                try { SafeReset(); } catch { }
                try { _waveOut?.Stop(); } catch { }
            }
            finally
            {
                try { _semaphore.Release(); } catch (ObjectDisposedException) { }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                lock (_ctsLock)
                {
                    try { _cts.Cancel(); } catch (ObjectDisposedException) { }
                    try { _cts.Dispose(); } catch (ObjectDisposedException) { }
                }

                if (_waveOut != null)
                {
                    try { _waveOut.Stop(); } catch { }
                    _waveOut.Dispose();
                    _waveOut = null;
                }

                _semaphore.Dispose();
                _synth = null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing AmenResolveService");
            }
        }

        private class SynthSampleProvider : ISampleProvider
        {
            private readonly MeltySynth.Synthesizer _synth;
            private readonly object _synthLock;
            private float[] _leftBuf = Array.Empty<float>();
            private float[] _rightBuf = Array.Empty<float>();
            public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

            public SynthSampleProvider(MeltySynth.Synthesizer synthesizer, object synthLock)
            {
                _synth = synthesizer;
                _synthLock = synthLock;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int half = count / 2;
                if (_leftBuf.Length != half)
                {
                    _leftBuf = new float[half];
                    _rightBuf = new float[half];
                }

                lock (_synthLock)
                {
                    _synth.Render(_leftBuf, _rightBuf);
                }
                
                for (int i = 0; i < half; i++)
                {
                    buffer[offset + i * 2] = _leftBuf[i];
                    buffer[offset + i * 2 + 1] = _rightBuf[i];
                }
                
                return count;
            }
        }
    }
}
