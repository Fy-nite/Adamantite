#pragma once

#include "IFileSystem.hpp"
#include <string>
#include <unordered_map>
#include <memory>
#include <algorithm>
#include <stdexcept>
#include <vector>

namespace Adamantite::VFS {

// Manages multiple mounted file systems accessed through virtual paths.
class VfsManager {
public:
    void Mount(const std::string& mountPoint, std::unique_ptr<IFileSystem> fs) {
        if (!fs) throw std::invalid_argument("fs cannot be null");
        std::string mp = NormalizeMountPoint(mountPoint);
        _mounts[mp] = std::move(fs);
    }

    void Unmount(const std::string& mountPoint) {
        _mounts.erase(NormalizeMountPoint(mountPoint));
    }

    std::vector<uint8_t> ReadAllBytes(const std::string& path) {
        auto [fs, local] = Resolve(path);
        if (!fs) throw std::runtime_error("No filesystem mounted for path: " + path);
        return fs->ReadAllBytes(local);
    }

    void WriteAllBytes(const std::string& path, const std::vector<uint8_t>& data) {
        auto [fs, local] = Resolve(path);
        if (!fs) throw std::runtime_error("No filesystem mounted for path: " + path);
        fs->WriteAllBytes(local, data);
    }

    bool Exists(const std::string& path) {
        auto [fs, local] = Resolve(path);
        return fs && fs->Exists(local);
    }

    bool FileExists(const std::string& path) { return Exists(path); }

    std::vector<VfsFileInfo> Enumerate(const std::string& path) {
        auto [fs, local] = Resolve(path);
        if (!fs) return {};
        return fs->Enumerate(local);
    }

    VfsFileInfo* GetFileInfo(const std::string& path) {
        auto [fs, local] = Resolve(path);
        if (!fs) return nullptr;
        return fs->GetFileInfo(local);
    }

    void CreateDirectory(const std::string& path) {
        auto [fs, local] = Resolve(path);
        if (!fs) throw std::runtime_error("No filesystem mounted for path: " + path);
        fs->CreateDirectory(local);
    }

    void Delete(const std::string& path) {
        auto [fs, local] = Resolve(path);
        if (!fs) throw std::runtime_error("No filesystem mounted for path: " + path);
        fs->Delete(local);
    }

    void Remove(const std::string& path) { Delete(path); }

private:
    std::unordered_map<std::string, std::unique_ptr<IFileSystem>> _mounts;

    static std::string NormalizeMountPoint(const std::string& mp) {
        if (mp.empty()) return "/";
        std::string s = mp;
        std::replace(s.begin(), s.end(), '\\', '/');
        if (s[0] != '/') s = "/" + s;
        if (s.size() > 1 && s.back() == '/') s.pop_back();
        return s;
    }

    // Returns { filesystem, local path } for the given VFS path
    std::pair<IFileSystem*, std::string> Resolve(const std::string& path) {
        if (path.empty()) return {nullptr, path};
        std::string p = path;
        std::replace(p.begin(), p.end(), '\\', '/');

        // Sort mounts by length descending to match longest prefix first
        std::vector<std::pair<std::string, IFileSystem*>> sorted;
        sorted.reserve(_mounts.size());
        for (auto& kv : _mounts) sorted.emplace_back(kv.first, kv.second.get());
        std::sort(sorted.begin(), sorted.end(),
            [](const auto& a, const auto& b){ return a.first.size() > b.first.size(); });

        for (auto& [mp, fs] : sorted) {
            if (mp == "/") {
                return {fs, p.substr(p.find_first_not_of('/'))};
            }
            std::string prefix = mp + "/";
            if (p.rfind(prefix, 0) == 0) {
                return {fs, p.substr(prefix.size())};
            }
            if (p == mp) return {fs, ""};
        }
        return {nullptr, path};
    }
};

} // namespace Adamantite::VFS
