#pragma once

#include "VfsFileInfo.hpp"
#include <string>
#include <vector>
#include <cstdint>
#include <memory>

namespace Adamantite::VFS {

// Abstract file system interface
class IFileSystem {
public:
    virtual ~IFileSystem() = default;

    // Open a file for reading; throws on failure
    virtual std::vector<uint8_t> ReadAllBytes(const std::string& path) = 0;

    // Write bytes to a file
    virtual void WriteAllBytes(const std::string& path, const std::vector<uint8_t>& data) = 0;

    virtual bool Exists(const std::string& path) = 0;

    virtual std::vector<VfsFileInfo> Enumerate(const std::string& path) = 0;

    virtual VfsFileInfo* GetFileInfo(const std::string& path) = 0;

    virtual void CreateDirectory(const std::string& path) = 0;

    virtual void Delete(const std::string& path) = 0;
};

} // namespace Adamantite::VFS
