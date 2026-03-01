using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using System.Threading.Tasks;
using System.Threading;

namespace ObjectIR.MonoGame.Helpers
{
    public class SoundSystem
    {
        private readonly Random _rng = new Random();
        // Post-processing pipeline for generated PCM bytes. Each processor receives raw 16-bit PCM
        // little-endian bytes and returns new PCM bytes (must keep same sampleRate/channels or convert accordingly).
        public delegate byte[] PostProcessor(string key, byte[] pcm, int sampleRate, AudioChannels channels);
        private readonly List<PostProcessor> _postProcessors = new();

        /// <summary>
        /// Register a global post processor that will be available to apply when precomputing PCM.
        /// Processors are invoked in the order they were added when used explicitly.
        /// </summary>
        public void RegisterPostProcessor(PostProcessor processor)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            _postProcessors.Add(processor);
        }

        /// <summary>
        /// Unregister a previously registered post processor.
        /// </summary>
        public void UnregisterPostProcessor(PostProcessor processor)
        {
            if (processor == null) return;
            _postProcessors.Remove(processor);
        }
        private readonly ContentManager? _content;
        private readonly Dictionary<string, SoundBus> _buses = new();
        private readonly Dictionary<string, SoundEffect> _precomputed = new();
        private readonly object _precomputedLock = new();
        // Track active SoundEffectInstances along with their base (per-play) volume and bus name
        // so bus volume changes can be applied without compounding.
        private readonly List<(SoundEffectInstance Instance, float BaseVolume, string BusName)> _activeInstances = new();
        private readonly object _activeInstancesLock = new();
        // Global multiplier applied to all buses (1.0 when focused, <1 when unfocused)
        private float _globalVolumeMultiplier = 1f;

        public SoundBus Master { get; } = new SoundBus("Master") { Volume = 1f };

        public SoundSystem()
        {
            _buses[Master.Name] = Master;
        }

        public SoundSystem(ContentManager content) : this()
        {
            _content = content;
        }

        /// <summary>
        /// Create or update a named bus with a given volume (0..1).
        /// </summary>
        public void CreateBus(string name, float volume = 1f)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name");
            if (_buses.TryGetValue(name, out var b))
            {
                b.Volume = Math.Clamp(volume, 0f, 1f);
                // also update MaxVolume to match when creating/updating
                if (b is SoundBus sb) sb.MaxVolume = Math.Clamp(volume, 0f, 1f);
            }
            else
            {
                var sb = new SoundBus(name) { Volume = Math.Clamp(volume, 0f, 1f) };
                // sb.MaxVolume = Math.Clamp(volume, 0f, 1f);
                _buses[name] = sb;
            }
        }
        // public float Volume { get; set; } = 1f;
        // /// <summary>
        // /// The desired max volume for this bus when global multiplier is 1.0.
        // /// </summary>
        // public float MaxVolume { get; set; } = 1f;
        public bool TryGetBus(string name, out SoundBus? bus) => _buses.TryGetValue(name, out bus);

        // Set the desired "max" volume for a bus (what it should be when focus multiplier is 1.0).
        // This caches the value and updates the effective current volume using the global multiplier.
        public void SetBusVolume(string name, float volume)
        {
            var v = Math.Clamp(volume, 0f, 1f);
            if (_buses.TryGetValue(name, out var b))
            {
                // store as MaxVolume when available
                if (b is SoundBus sb)
                {
                    sb.MaxVolume = v;
                    sb.Volume = sb.MaxVolume * _globalVolumeMultiplier;
                }
                else
                {
                    b.Volume = v * _globalVolumeMultiplier;
                }
            }
            else
            {
                // create bus with MaxVolume
                var sb = new SoundBus(name) { Volume = v * _globalVolumeMultiplier };
                sb.MaxVolume = v;
                _buses[name] = sb;
            }

            // Apply new bus volume to any currently playing instances that belong to this bus
            try
            {
                ApplyBusVolumeToActiveInstances(name);
            }
            catch { }
        }

        // Set the global multiplier applied to all buses (e.g., 1.0 focused, 0.3 unfocused)
        public void SetGlobalVolumeMultiplier(float multiplier)
        {
            _globalVolumeMultiplier = Math.Clamp(multiplier, 0f, 1f);
            lock (_precomputedLock)
            {
                foreach (var kv in _buses)
                {
                    var b = kv.Value;
                    if (b is SoundBus sb)
                    {
                        sb.Volume = sb.MaxVolume * _globalVolumeMultiplier;
                    }
                    else
                    {
                        b.Volume = b.Volume * _globalVolumeMultiplier;
                    }
                }
            }

            try
            {
                // update all active instances to match new bus volumes
                lock (_activeInstancesLock)
                {
                    for (int i = _activeInstances.Count - 1; i >= 0; i--)
                    {
                        var entry = _activeInstances[i];
                        var inst = entry.Instance;
                        if (inst == null)
                        {
                            _activeInstances.RemoveAt(i);
                            continue;
                        }
                        try
                        {
                            if (inst.State == SoundState.Stopped)
                            {
                                try { inst.Dispose(); } catch { }
                                _activeInstances.RemoveAt(i);
                                continue;
                            }
                            if (_buses.TryGetValue(entry.BusName, out var bus))
                            {
                                float busVol = bus.Volume;
                                inst.Volume = Math.Clamp(entry.BaseVolume * busVol, 0f, 1f);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private void ApplyBusVolumeToActiveInstances(string busName)
        {
            lock (_activeInstancesLock)
            {
                for (int i = _activeInstances.Count - 1; i >= 0; i--)
                {
                    var entry = _activeInstances[i];
                    var inst = entry.Instance;
                    if (inst == null)
                    {
                        _activeInstances.RemoveAt(i);
                        continue;
                    }
                    try
                    {
                        if (inst.State == SoundState.Stopped)
                        {
                            try { inst.Dispose(); } catch { }
                            _activeInstances.RemoveAt(i);
                            continue;
                        }

                        var busVol = _buses.TryGetValue(busName, out var b) ? b.Volume : 1f;
                        inst.Volume = Math.Clamp(entry.BaseVolume * busVol, 0f, 1f);
                    }
                    catch
                    {
                        // ignore individual instance errors
                    }
                }
            }
        }

        /// <summary>
        /// Load a SoundEffect using the ContentManager provided in the ctor.
        /// Throws if no ContentManager was provided.
        /// </summary>
        public SoundEffect Load(string assetName)
        {
            if (_content == null) throw new InvalidOperationException("ContentManager was not provided to SoundSystem.");
            return _content.Load<SoundEffect>(assetName);
        }

        /// <summary>
        /// Create a SoundEffect from a raw stream (wav).
        /// </summary>
        public SoundEffect FromStream(Stream stream)
        {
            return SoundEffect.FromStream(stream);
        }

        /// <summary>
        /// Play a SoundEffect asset by name and return the instance for further control.
        /// </summary>
        public SoundEffectInstance Play(string assetName, string busName = "Master", float volume = 1f, float pitch = 0f, float pan = 0f)
        {
            var se = Load(assetName);
            return Play(se, busName, volume, pitch, pan);
        }

        /// <summary>
        /// Play a provided SoundEffect and return the instance.
        /// </summary>
        public SoundEffectInstance Play(SoundEffect effect, string busName = "Master", float volume = 1f, float pitch = 0f, float pan = 0f)
        {
            var instance = effect.CreateInstance();
            var busVol = _buses.TryGetValue(busName, out var b) ? b.Volume : 1f;
            instance.Volume = Math.Clamp(volume * busVol, 0f, 1f);
            instance.Pitch = Math.Clamp(pitch, -1f, 1f);
            instance.Pan = Math.Clamp(pan, -1f, 1f);
            instance.Play();
            // Track instance and its base volume + bus name so future bus volume changes can update it
            try
            {
                lock (_activeInstancesLock)
                {
                    _activeInstances.Add((instance, volume, busName));
                }
            }
            catch { }
            return instance;
        }

        /// <summary>
        /// Play a provided SoundEffect for a specified duration (in seconds), then stop and dispose the instance.
        /// </summary>
        public SoundEffectInstance Play(SoundEffect effect, double durationSeconds, string busName = "Master", float volume = 1f, float pitch = 0f, float pan = 0f)
        {
            var instance = Play(effect, busName, volume, pitch, pan);
            if (durationSeconds > 0)
            {
                // Fire and forget: schedule stop/dispose after duration
                Task.Run(async () => {
                    try {
                        await Task.Delay((int)(durationSeconds * 1000));
                        try { instance.Stop(); } catch { }
                        try { instance.Dispose(); } catch { }
                    } catch { /* ignore */ }
                });
            }
            return instance;
        }

        /// <summary>
        /// Play a one-shot using SoundEffect.Play with bus volume applied.
        /// </summary>
        public void PlayOneShot(string assetName, string busName = "Master", float volume = 1f)
        {
            var se = Load(assetName);
            PlayOneShot(se, busName, volume);
        }

        public void PlayOneShot(SoundEffect effect, string busName = "Master", float volume = 1f)
        {
            var busVol = _buses.TryGetValue(busName, out var b) ? b.Volume : 1f;
            // Provide default values for pitch and pan as required by SoundEffect.Play(float, float, float)
            effect.Play(Math.Clamp(volume * busVol, 0f, 1f), 0f, 0f);
        }

        /// <summary>
        /// Play a SoundEffect once at a random pitch between minPitch and maxPitch.
        /// Pitch values are in the range [-1.0, 1.0].
        /// </summary>
        public void PlayOneShotWithRandomPitch(SoundEffect effect, float minPitch = -0.1f, float maxPitch = 0.1f, string busName = "Master", float volume = 1f)
        {
            if (effect == null) return;
            float pitch = (float)(_rng.NextDouble() * (maxPitch - minPitch) + minPitch);
            pitch = Math.Clamp(pitch, -1f, 1f);

            var inst = effect.CreateInstance();
            var busVol = _buses.TryGetValue(busName, out var b) ? b.Volume : 1f;
            inst.Volume = Math.Clamp(volume * busVol, 0f, 1f);
            inst.Pitch = pitch;
            inst.Pan = 0f;
            try
            {
                lock (_activeInstancesLock)
                {
                    _activeInstances.Add((inst, volume, busName));
                }
            }
            catch { }
            try { inst.Play(); } catch { }

            // Schedule stop/dispose after duration (best-effort)
            try
            {
                var ms = (int)Math.Ceiling(effect.Duration.TotalMilliseconds);
                if (ms <= 0) ms = 1000;
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(ms);
                        try { inst.Stop(); } catch { }
                        try { inst.Dispose(); } catch { }
                    }
                    catch { }
                });
            }
            catch { }
        }

        /// <summary>
        /// Create a SoundEffect from a WAV/PCM stream and play it once with random pitch.
        /// </summary>
        public void PlayOneShotFromStreamWithRandomPitch(Stream stream, float minPitch = -0.1f, float maxPitch = 0.1f, string busName = "Master", float volume = 1f)
        {
            if (stream == null) return;
            try
            {
                var se = FromStream(stream);
                if (se != null) PlayOneShotWithRandomPitch(se, minPitch, maxPitch, busName, volume);
            }
            catch { }
        }

        /// <summary>
        /// Play raw PCM bytes as a one-shot with a random pitch. Caller provides sampleRate and channels.
        /// </summary>
        public void PlayOneShotFromPcmWithRandomPitch(byte[] pcm, int sampleRate, AudioChannels channels = AudioChannels.Mono, float minPitch = -0.1f, float maxPitch = 0.1f, string busName = "Master", float volume = 1f)
        {
            if (pcm == null || pcm.Length == 0) return;
            try
            {
                var se = new SoundEffect(pcm, sampleRate, channels);
                PlayOneShotWithRandomPitch(se, minPitch, maxPitch, busName, volume);
            }
            catch { }
        }

        /// <summary>
        /// Get a precomputed SoundEffect by key, or create and cache it using the provided factory.
        /// Thread-safe.
        /// </summary>
        public SoundEffect GetOrCreatePrecomputed(string key, Func<SoundEffect> factory)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("key");
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            lock (_precomputedLock)
            {
                if (_precomputed.TryGetValue(key, out var existing)) return existing;
                var created = factory();
                _precomputed[key] = created;
                return created;
            }
        }

        /// <summary>
        /// Create a SoundEffect from PCM bytes provided by pcmProvider and cache by key.
        /// </summary>
        public SoundEffect PrecomputeFromPcm(string key, Func<byte[]> pcmProvider, int sampleRate, AudioChannels channels = AudioChannels.Mono)
        {
            if (pcmProvider == null) throw new ArgumentNullException(nameof(pcmProvider));
            return GetOrCreatePrecomputed(key, () =>
            {
                var pcm = pcmProvider();
                // allow registered post-processors to modify the PCM
                foreach (var pp in _postProcessors)
                {
                    try
                    {
                        pcm = pp(key, pcm, sampleRate, channels);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("PostProcessor error: " + ex.Message);
                    }
                }
                try
                {
                    int channelCount = channels == AudioChannels.Mono ? 1 : 2;
                    int bytesPerSample = 2; // 16-bit PCM
                    int frames = pcm.Length / (bytesPerSample * channelCount);
                    double seconds = frames / (double)sampleRate;
                    Console.WriteLine($"[SoundSystem] PrecomputeFromPcm key={key} frames={frames} seconds={seconds:0.###} sampleRate={sampleRate} channels={channelCount}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[SoundSystem] PrecomputeFromPcm logging error: " + ex.Message);
                }
                return new SoundEffect(pcm, sampleRate, channels);
            });
        }

        public bool TryGetPrecomputed(string key, out SoundEffect? effect)
        {
            lock (_precomputedLock)
            {
                return _precomputed.TryGetValue(key, out effect);
            }
        }

        public void RemovePrecomputed(string key)
        {
            lock (_precomputedLock)
            {
                if (_precomputed.TryGetValue(key, out var e))
                {
                    _precomputed.Remove(key);
                    e.Dispose();
                }
            }
        }

        public void ClearPrecomputed()
        {
            lock (_precomputedLock)
            {
                foreach (var e in _precomputed.Values) e.Dispose();
                _precomputed.Clear();
            }
        }

    }
}
