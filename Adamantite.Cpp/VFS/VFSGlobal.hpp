#pragma once

#include "VfsManager.hpp"

namespace Adamantite::VFS {

// Global singleton accessor for the VFS manager
class VFSGlobal {
public:
    static VfsManager* Manager() { return _manager; }
    static void SetManager(VfsManager* manager) { _manager = manager; }

    static void Initialize() {
        static VfsManager defaultManager;
        _manager = &defaultManager;
    }

private:
    inline static VfsManager* _manager = nullptr;
};

} // namespace Adamantite::VFS
