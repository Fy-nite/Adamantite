#pragma once

#include "../Helpers/SoundSystem.hpp"
#include "SimpleSynth.hpp"
#include <string>
#include <cmath>
#include <stdexcept>

namespace Adamantite::SFX {

// Plays a short synthesized click sound through the provided SoundSystem.
class Button {
public:
    static void PlayClick(Helpers::SoundSystem& soundSystem,
                          const std::string& bus = "Master",
                          float defaultPitch = 440.0f)
    {
        const int sampleRate  = 44100;
        const int channels    = 1;
        const float frequency = 1200.0f;
        const float duration  = 0.06f;

        std::string key = "button_click_synth_v1_" + std::to_string(static_cast<int>(frequency))
                        + "_" + std::to_string(duration)
                        + "_" + std::to_string(sampleRate)
                        + "_" + std::to_string(channels);

        const auto& pcmBuf = soundSystem.GetOrCreatePrecomputed(key, [&]() -> Helpers::PcmBuffer {
            SimpleSynth::MonoSynth synth(sampleRate, 8);

            // Convert frequency to MIDI note
            double midiFloat = 69.0 + 12.0 * std::log2(frequency / static_cast<double>(defaultPitch));
            int midiNote = std::max(0, std::min(127, static_cast<int>(std::round(midiFloat))));

            synth.NoteOn(midiNote, 1.0f);
            int holdFrames    = static_cast<int>(std::ceil(duration * sampleRate));
            int releaseFrames = static_cast<int>(std::ceil(0.2 * sampleRate));

            auto pcm  = synth.RenderPcm(holdFrames);
            synth.NoteOff(midiNote);
            auto tail = synth.RenderPcm(releaseFrames);

            std::vector<uint8_t> out;
            out.insert(out.end(), pcm.begin(), pcm.end());
            out.insert(out.end(), tail.begin(), tail.end());

            Helpers::PcmBuffer buf;
            buf.data       = std::move(out);
            buf.sampleRate = sampleRate;
            buf.channels   = channels;
            return buf;
        });

        soundSystem.PlayOneShot(pcmBuf, bus);
    }
};

} // namespace Adamantite::SFX
