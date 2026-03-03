#pragma once

#include <string>
#include <chrono>
#include <filesystem>

namespace Adamantite::VFS {

struct VfsFileInfo {
    std::string Path;
    bool        IsDirectory = false;
    int64_t     Length      = 0;
    std::chrono::system_clock::time_point LastModified;

    std::string Name() const {
        return std::filesystem::path(Path).filename().string();
    }

    VfsFileInfo() = default;
    VfsFileInfo(const std::string& path, bool isDir, int64_t length,
                std::chrono::system_clock::time_point modified)
        : Path(path), IsDirectory(isDir), Length(length), LastModified(modified)
    {}
};

} // namespace Adamantite::VFS
