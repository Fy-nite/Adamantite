#pragma once

#include "IFileSystem.hpp"
#include <unordered_map>
#include <mutex>
#include <stdexcept>
#include <algorithm>

namespace Adamantite::VFS {

// Case-insensitive in-memory file system
class InMemoryFileSystem : public IFileSystem {
public:
    InMemoryFileSystem() = default;

    std::vector<uint8_t> ReadAllBytes(const std::string& path) override {
        std::string norm = Normalize(path);
        norm = ResolveSymlink(norm);
        std::lock_guard<std::mutex> lock(_mutex);
        auto it = _files.find(norm);
        if (it == _files.end()) throw std::runtime_error("File not found: " + path);
        return it->second;
    }

    void WriteAllBytes(const std::string& path, const std::vector<uint8_t>& data) override {
        std::string norm = Normalize(path);
        std::lock_guard<std::mutex> lock(_mutex);
        _files[norm] = data;
    }

    bool Exists(const std::string& path) override {
        std::string norm = Normalize(path);
        std::lock_guard<std::mutex> lock(_mutex);
        return _files.count(norm) || _dirs.count(norm) || _symlinks.count(norm);
    }

    std::vector<VfsFileInfo> Enumerate(const std::string& path) override {
        std::string norm = Normalize(path);
        std::string prefix = norm.empty() ? "" : norm + "/";
        std::lock_guard<std::mutex> lock(_mutex);
        std::vector<VfsFileInfo> result;
        for (auto& kv : _dirs) {
            if (kv.first.rfind(prefix, 0) == 0 && kv.first != norm)
                result.emplace_back(kv.first.substr(prefix.size()), true, 0, kv.second);
        }
        for (auto& kv : _files) {
            if (kv.first.rfind(prefix, 0) == 0)
                result.emplace_back(kv.first.substr(prefix.size()), false,
                    static_cast<int64_t>(kv.second.size()), std::chrono::system_clock::now());
        }
        return result;
    }

    VfsFileInfo* GetFileInfo(const std::string& path) override {
        std::string norm = Normalize(path);
        std::lock_guard<std::mutex> lock(_mutex);
        auto fd = _dirs.find(norm);
        if (fd != _dirs.end()) {
            _cachedInfo = VfsFileInfo(norm, true, 0, fd->second);
            return &_cachedInfo;
        }
        auto ff = _files.find(norm);
        if (ff != _files.end()) {
            _cachedInfo = VfsFileInfo(norm, false, static_cast<int64_t>(ff->second.size()),
                                      std::chrono::system_clock::now());
            return &_cachedInfo;
        }
        return nullptr;
    }

    void CreateDirectory(const std::string& path) override {
        std::string norm = Normalize(path);
        std::lock_guard<std::mutex> lock(_mutex);
        _dirs[norm] = std::chrono::system_clock::now();
    }

    void Delete(const std::string& path) override {
        std::string norm = Normalize(path);
        std::lock_guard<std::mutex> lock(_mutex);
        _files.erase(norm);
        _dirs.erase(norm);
        _symlinks.erase(norm);
    }

    void CreateSymlink(const std::string& linkPath, const std::string& targetPath) {
        _symlinks[Normalize(linkPath)] = targetPath;
    }

private:
    mutable std::mutex _mutex;
    std::unordered_map<std::string, std::vector<uint8_t>>                  _files;
    std::unordered_map<std::string, std::chrono::system_clock::time_point> _dirs;
    std::unordered_map<std::string, std::string>                           _symlinks;
    mutable VfsFileInfo _cachedInfo;

    static std::string Normalize(const std::string& path) {
        if (path.empty()) return {};
        std::string p = path;
        std::replace(p.begin(), p.end(), '\\', '/');
        // strip leading slash
        size_t start = p.find_first_not_of('/');
        return start == std::string::npos ? std::string() : p.substr(start);
    }

    std::string ResolveSymlink(const std::string& path) const {
        auto it = _symlinks.find(path);
        if (it != _symlinks.end()) return Normalize(it->second);
        return path;
    }
};

} // namespace Adamantite::VFS
