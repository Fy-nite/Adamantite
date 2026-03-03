#pragma once

#include <cstdlib>
#include <string>
#include <iostream>

namespace Adamantite::Util {

// Simple runtime debug helper - gates verbose logs behind ASMO_DEBUG env var
class DebugUtil {
public:
    static void Debug(const std::string& msg) {
        try {
            const char* val = std::getenv("ASMO_DEBUG");
            if (val && std::string(val) == "1") {
                std::cerr << msg << "\n";
            }
        } catch (...) {}
    }
};

} // namespace Adamantite::Util
