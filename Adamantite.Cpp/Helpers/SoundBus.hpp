#pragma once

#include <string>

namespace Adamantite::Helpers {

class SoundBus {
public:
    std::string Name;
    /// Volume multiplier for this bus (0.0 - 1.0). Default is 1.0 (no change).
    float Volume    = 1.0f;
    /// The desired max volume for this bus when global multiplier is 1.0.
    float MaxVolume = 1.0f;

    SoundBus() = default;
    explicit SoundBus(const std::string& name) : Name(name) {}
};

} // namespace Adamantite::Helpers
