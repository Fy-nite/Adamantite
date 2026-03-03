#pragma once

#include <string>
#include <vector>
#include <cmath>
#include <algorithm>
#include <stdexcept>
#include <cstdint>
#include <numbers>

namespace Adamantite::SFX {

/// Simple mono polyphonic synthesizer with ADSR envelopes.
/// Produces 16-bit PCM little-endian bytes.
class SimpleSynth {
public:
    enum class Waveform { Sine, Square, Triangle, Noise };

    // Legacy helper: one-shot sine wave with exponential decay
    static std::vector<uint8_t> GenerateSineWavePcm(float frequencyHz, float durationSeconds,
                                                      int sampleRate = 44100, float amplitude = 0.5f,
                                                      int channels = 1)
    {
        if (durationSeconds <= 0) return {};
        if (sampleRate <= 0) throw std::invalid_argument("sampleRate");
        if (channels < 1)   throw std::invalid_argument("channels");

        int frames  = static_cast<int>(std::ceil(durationSeconds * sampleRate));
        std::vector<uint8_t> bytes(frames * channels * 2);
        double twoPiF = 2.0 * std::numbers::pi * frequencyHz;

        for (int i = 0; i < frames; i++) {
            double t   = static_cast<double>(i) / sampleRate;
            double env = std::exp(-10.0 * t);
            float sample = static_cast<float>(amplitude * env * std::sin(twoPiF * t));
            int16_t s = static_cast<int16_t>(std::max(-32768, std::min(32767,
                static_cast<int>(sample * 32767))));
            for (int ch = 0; ch < channels; ch++) {
                int idx = (i * channels + ch) * 2;
                bytes[idx]     = static_cast<uint8_t>(s & 0xFF);
                bytes[idx + 1] = static_cast<uint8_t>((s >> 8) & 0xFF);
            }
        }
        return bytes;
    }

    static std::vector<uint8_t> GenerateWavePcm(float frequencyHz, float durationSeconds,
                                                  Waveform waveform, int sampleRate = 44100,
                                                  float amplitude = 0.5f, int channels = 1)
    {
        if (durationSeconds <= 0) return {};
        if (sampleRate <= 0) throw std::invalid_argument("sampleRate");
        if (channels < 1)   throw std::invalid_argument("channels");

        int frames = static_cast<int>(std::ceil(durationSeconds * sampleRate));
        std::vector<uint8_t> bytes(frames * channels * 2);
        double twoPiF = 2.0 * std::numbers::pi * frequencyHz;

        for (int i = 0; i < frames; i++) {
            double t = static_cast<double>(i) / sampleRate;
            float osc = 0.0f;
            switch (waveform) {
                case Waveform::Square:
                    osc = (std::sin(twoPiF * t) >= 0) ? 1.0f : -1.0f; break;
                case Waveform::Triangle:
                    osc = static_cast<float>(2.0 * std::abs(2.0 * (t * frequencyHz -
                          std::floor(t * frequencyHz + 0.5))) - 1.0); break;
                case Waveform::Noise:
                    osc = (static_cast<float>(rand()) / RAND_MAX) * 2.0f - 1.0f; break;
                default:
                    osc = static_cast<float>(std::sin(twoPiF * t)); break;
            }
            float sample = amplitude * osc;
            int16_t s = static_cast<int16_t>(std::max(-32768, std::min(32767,
                static_cast<int>(sample * 32767))));
            for (int ch = 0; ch < channels; ch++) {
                int idx = (i * channels + ch) * 2;
                bytes[idx]     = static_cast<uint8_t>(s & 0xFF);
                bytes[idx + 1] = static_cast<uint8_t>((s >> 8) & 0xFF);
            }
        }
        return bytes;
    }

    // Convert MIDI note (0-127) to frequency in Hz
    static double MidiNoteToFrequency(int note) {
        return 440.0 * std::pow(2.0, (note - 69) / 12.0);
    }

    // --- Polyphonic synthesizer ---

    class MonoSynth {
    public:
        float MasterVolume = 0.9f;

        MonoSynth(int sampleRate = 44100, int polyphony = 8)
            : _sampleRate(sampleRate > 0 ? sampleRate : 44100)
        {
            _voices.resize(std::max(1, polyphony));
        }

        void NoteOn(int midiNote, float velocity = 1.0f) {
            Voice* v = nullptr;
            for (auto& voice : _voices) {
                if (!voice.Active()) { v = &voice; break; }
            }
            if (!v) v = &_voices[0]; // steal oldest
            v->Start(midiNote, velocity, _sampleRate);
        }

        void NoteOff(int midiNote) {
            for (auto& v : _voices) {
                if (v.Active() && v.Note == midiNote) { v.ReleaseNote(); break; }
            }
        }

        std::vector<uint8_t> RenderPcm(int frames) {
            if (frames <= 0) return {};
            std::vector<uint8_t> bytes(frames * 2);
            double dt = 1.0 / _sampleRate;

            for (int i = 0; i < frames; i++) {
                double mixed = 0.0;
                for (auto& v : _voices) {
                    if (!v.Active()) continue;
                    v.AdvanceEnvelope(dt);
                    mixed += v.ProcessSample();
                }
                double s = std::max(-1.0, std::min(1.0, mixed * MasterVolume));
                int16_t s16 = static_cast<int16_t>(s * 32767);
                bytes[i * 2]     = static_cast<uint8_t>(s16 & 0xFF);
                bytes[i * 2 + 1] = static_cast<uint8_t>((s16 >> 8) & 0xFF);
            }
            return bytes;
        }

    private:
        int _sampleRate;

        enum class EnvState { Idle, Attack, Decay, Sustain, Release };

        struct Voice {
            int   Note     = -1;
            float Velocity = 1.0f;
            double Phase   = 0.0;
            double PhaseInc = 0.0;
            float Attack  = 0.01f;
            float Decay   = 0.05f;
            float Sustain = 0.7f;
            float Release = 0.1f;

            bool Active() const { return _state != EnvState::Idle; }

            void Start(int midiNote, float velocity, int sampleRate) {
                Note = midiNote;
                Velocity = std::max(0.0f, std::min(1.0f, velocity));
                Phase = 0.0;
                PhaseInc = MidiNoteToFrequency(midiNote) * (2.0 * std::numbers::pi) / sampleRate;
                _state = EnvState::Attack;
                _envLevel = 0.0f;
                _timeInState = 0.0;
            }

            void ReleaseNote() {
                if (_state != EnvState::Idle && _state != EnvState::Release) {
                    _state = EnvState::Release; _timeInState = 0.0;
                }
            }

            float ProcessSample() {
                if (_state == EnvState::Idle) return 0.0f;
                float sample = static_cast<float>(std::sin(Phase) * Velocity);
                Phase += PhaseInc;
                return sample * _envLevel;
            }

            void AdvanceEnvelope(double dt) {
                _timeInState += dt;
                switch (_state) {
                    case EnvState::Attack:
                        if (Attack <= 0) { _envLevel = 1.0f; _state = EnvState::Decay; _timeInState = 0; }
                        else {
                            _envLevel = static_cast<float>(std::min(1.0, _timeInState / Attack));
                            if (_envLevel >= 1.0f) { _state = EnvState::Decay; _timeInState = 0; }
                        }
                        break;
                    case EnvState::Decay:
                        if (Decay <= 0) { _envLevel = Sustain; _state = EnvState::Sustain; }
                        else {
                            float t = static_cast<float>(std::min(1.0, _timeInState / Decay));
                            _envLevel = 1.0f + (Sustain - 1.0f) * t;
                            if (t >= 1.0f) _state = EnvState::Sustain;
                        }
                        break;
                    case EnvState::Sustain:
                        _envLevel = Sustain; break;
                    case EnvState::Release:
                        if (Release <= 0) { _envLevel = 0; _state = EnvState::Idle; }
                        else {
                            float t = static_cast<float>(std::min(1.0, _timeInState / Release));
                            _envLevel *= (1.0f - t);
                            if (t >= 1.0f || _envLevel <= 1e-4f) {
                                _envLevel = 0; _state = EnvState::Idle;
                            }
                        }
                        break;
                    default: break;
                }
            }

        private:
            EnvState _state    = EnvState::Idle;
            float    _envLevel = 0.0f;
            double   _timeInState = 0.0;
        };

        std::vector<Voice> _voices;
    };
};

} // namespace Adamantite::SFX
