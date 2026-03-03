#pragma once

#include "VfsManager.hpp"
#include <stdexcept>

namespace Adamantite::VFS {

// Font loading from VFS.
// Integrate with your rendering system by replacing the body of LoadFont.
class VfsFontLoader {
public:
    // Loads a font file from the VFS.
    // Returns raw font bytes; callers must integrate with their font renderer.
    static std::vector<uint8_t> LoadFont(VfsManager& vfs, const std::string& fontPath) {
        if (!vfs.FileExists(fontPath))
            throw std::runtime_error("Font not found in VFS: " + fontPath);
        return vfs.ReadAllBytes(fontPath);
    }
};

} // namespace Adamantite::VFS
