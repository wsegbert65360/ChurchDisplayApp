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

        public AmenResolveService(string soundFontPath)
        {
            _soundFontPath = soundFontPath;
        }

        private void Initialize()
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
                _sampleProvider = new SynthSampleProvider(_synth);
                
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_sampleProvider);
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize synthesizer with SoundFont at {Path}", _soundFontPath);
                _synth = null;
            }
        }

        public void Cancel()
        {
            _cts.Cancel();
            try { _waveOut?.Stop(); } catch { }
        }

        public async Task ExecuteResolveAsync(float volume = 1.0f)
        {
            if (!await _semaphore.WaitAsync(0))
            {
                Log.Information("Amen resolve already in progress, skipping");
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                Initialize();
                if (_synth == null || _waveOut == null) return;

                _waveOut.Volume = Math.Clamp(volume, 0.0f, 1.0f);

                int C2 = 36; int F2 = 41; int C3 = 48; int E3 = 52; int F3 = 53; int G3 = 55; int A3 = 57;
                int C4 = 60;

                Random rnd = new Random();

                _synth.ProcessMidiMessage(0, 0xB0, 64, 127);

                _synth.ProcessMidiMessage(0, 0x90, F2, 60);
                _synth.ProcessMidiMessage(0, 0x90, F3, 70 + rnd.Next(-5, 6));
                _synth.ProcessMidiMessage(0, 0x90, A3, 70 + rnd.Next(-5, 6));
                _synth.ProcessMidiMessage(0, 0x90, C4, 70 + rnd.Next(-5, 6));

                await Task.Delay(2000, token);

                _synth.ProcessMidiMessage(0, 0x80, F2, 0);
                _synth.ProcessMidiMessage(0, 0x80, F3, 0);
                _synth.ProcessMidiMessage(0, 0x80, A3, 0);
                _synth.ProcessMidiMessage(0, 0x80, C4, 0);

                _synth.ProcessMidiMessage(0, 0x90, C2, 65);
                _synth.ProcessMidiMessage(0, 0x90, C3, 85 + rnd.Next(-5, 6));
                _synth.ProcessMidiMessage(0, 0x90, E3, 85 + rnd.Next(-5, 6));
                _synth.ProcessMidiMessage(0, 0x90, G3, 85 + rnd.Next(-5, 6));
                _synth.ProcessMidiMessage(0, 0x90, C4, 85 + rnd.Next(-5, 6));

                await Task.Delay(2500, token);

                for (int vol = 100; vol >= 0; vol -= 5)
                {
                    token.ThrowIfCancellationRequested();
                    _synth.ProcessMidiMessage(0, 0xB0, 7, vol);
                    await Task.Delay(50, token);
                }

                _synth.ProcessMidiMessage(0, 0xB0, 64, 0);
                _synth.Reset();
                _synth.ProcessMidiMessage(0, 0xB0, 7, 100);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Amen resolve was cancelled");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Amen resolve execution failed");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _cts.Cancel();
                _cts.Dispose();

                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                }

                _semaphore.Dispose();
                
                // Synthesizer doesn't implement IDisposable in MeltySynth, 
                // but we null it out for safety.
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
            public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

            public SynthSampleProvider(MeltySynth.Synthesizer synthesizer)
            {
                _synth = synthesizer;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                float[] left = new float[count / 2];
                float[] right = new float[count / 2];
                
                lock (_synth)
                {
                    _synth.Render(left, right);
                }
                
                for (int i = 0; i < count / 2; i++)
                {
                    buffer[offset + i * 2] = left[i];
                    buffer[offset + i * 2 + 1] = right[i];
                }
                
                return count;
            }
        }
    }
}
