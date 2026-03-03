#pragma once

#include "SoundBus.hpp"
#include <string>
#include <unordered_map>
#include <vector>
#include <functional>
#include <stdexcept>
#include <algorithm>
#include <cmath>

namespace Adamantite::Helpers {

// Platform-agnostic sound system.
// Audio playback is delegated to a pluggable IAudioBackend so the
// core logic (buses, volume management, precomputed PCM cache) is
// independent of any particular audio library.

struct PcmBuffer {
    std::vector<uint8_t> data;
    int sampleRate = 44100;
    int channels   = 1; // 1 = mono, 2 = stereo
};

// Abstract audio backend
class IAudioBackend {
public:
    virtual ~IAudioBackend() = default;
    virtual void PlayOneShot(const PcmBuffer& pcm, float volume, float pitch, float pan) = 0;
};

// Post-processing pipeline type
using PostProcessor = std::function<std::vector<uint8_t>(
    const std::string& key, std::vector<uint8_t> pcm, int sampleRate, int channels)>;

class SoundSystem {
public:
    SoundBus Master{"Master"};

    SoundSystem() {
        Master.Volume = 1.0f;
        _buses[Master.Name] = &Master;
    }

    void SetAudioBackend(IAudioBackend* backend) { _backend = backend; }

    void RegisterPostProcessor(PostProcessor processor) {
        _postProcessors.push_back(std::move(processor));
    }

    // Create or update a named bus with a given volume (0..1).
    void CreateBus(const std::string& name, float volume = 1.0f) {
        if (name.empty()) throw std::invalid_argument("name");
        auto it = _ownedBuses.find(name);
        if (it != _ownedBuses.end()) {
            it->second.Volume    = Clamp(volume);
            it->second.MaxVolume = Clamp(volume);
        } else {
            auto& bus = _ownedBuses[name];
            bus.Name = name;
            bus.Volume = bus.MaxVolume = Clamp(volume);
            _buses[name] = &bus;
        }
    }

    bool TryGetBus(const std::string& name, SoundBus** out) {
        auto it = _buses.find(name);
        if (it == _buses.end()) { *out = nullptr; return false; }
        *out = it->second; return true;
    }

    void SetBusVolume(const std::string& name, float volume) {
        float v = Clamp(volume);
        auto it = _buses.find(name);
        if (it != _buses.end()) {
            it->second->MaxVolume = v;
            it->second->Volume    = v * _globalVolumeMultiplier;
        } else {
            auto& bus = _ownedBuses[name];
            bus.Name = name;
            bus.MaxVolume = v;
            bus.Volume    = v * _globalVolumeMultiplier;
            _buses[name]  = &bus;
        }
    }

    void SetGlobalVolumeMultiplier(float multiplier) {
        _globalVolumeMultiplier = Clamp(multiplier);
        for (auto& kv : _buses) {
            kv.second->Volume = kv.second->MaxVolume * _globalVolumeMultiplier;
        }
    }

    // Play raw PCM as a one-shot (delegates to audio backend if set)
    void PlayOneShot(const PcmBuffer& pcm, const std::string& busName = "Master",
                     float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f)
    {
        if (!_backend) return;
        float busVol = BusVolume(busName);
        _backend->PlayOneShot(pcm, Clamp(volume * busVol), pitch, pan);
    }

    // Get or create a precomputed PCM buffer by key.
    const PcmBuffer& GetOrCreatePrecomputed(const std::string& key,
                                             std::function<PcmBuffer()> factory)
    {
        if (key.empty()) throw std::invalid_argument("key");
        auto it = _precomputed.find(key);
        if (it != _precomputed.end()) return it->second;
        PcmBuffer created = factory();
        // apply post-processors
        for (auto& pp : _postProcessors) {
            try { created.data = pp(key, std::move(created.data), created.sampleRate, created.channels); }
            catch (...) {}
        }
        _precomputed[key] = std::move(created);
        return _precomputed[key];
    }

    const PcmBuffer* TryGetPrecomputed(const std::string& key) const {
        auto it = _precomputed.find(key);
        return it != _precomputed.end() ? &it->second : nullptr;
    }

    void RemovePrecomputed(const std::string& key) { _precomputed.erase(key); }
    void ClearPrecomputed() { _precomputed.clear(); }

private:
    IAudioBackend* _backend = nullptr;
    std::unordered_map<std::string, SoundBus*> _buses;
    std::unordered_map<std::string, SoundBus>  _ownedBuses;
    std::unordered_map<std::string, PcmBuffer> _precomputed;
    std::vector<PostProcessor> _postProcessors;
    float _globalVolumeMultiplier = 1.0f;

    static float Clamp(float v) { return std::max(0.0f, std::min(1.0f, v)); }

    float BusVolume(const std::string& name) const {
        auto it = _buses.find(name);
        return it != _buses.end() ? it->second->Volume : 1.0f;
    }
};

} // namespace Adamantite::Helpers
