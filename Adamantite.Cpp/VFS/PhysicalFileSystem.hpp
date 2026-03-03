#pragma once

#include "IFileSystem.hpp"
#include <filesystem>
#include <fstream>
#include <stdexcept>

namespace Adamantite::VFS {

// File system backed by the OS filesystem, rooted at a given path.
class PhysicalFileSystem : public IFileSystem {
public:
    explicit PhysicalFileSystem(const std::string& rootPath) {
        if (rootPath.empty()) throw std::invalid_argument("rootPath");
        _root = std::filesystem::canonical(std::filesystem::absolute(rootPath));
        if (!std::filesystem::exists(_root)) std::filesystem::create_directories(_root);
    }

    std::vector<uint8_t> ReadAllBytes(const std::string& path) override {
        auto phys = ToPhysical(path);
        if (!std::filesystem::is_regular_file(phys))
            throw std::runtime_error("File not found: " + path);
        std::ifstream f(phys, std::ios::binary);
        if (!f) throw std::runtime_error("Cannot open: " + path);
        return {std::istreambuf_iterator<char>(f), {}};
    }

    void WriteAllBytes(const std::string& path, const std::vector<uint8_t>& data) override {
        auto phys = ToPhysical(path);
        std::filesystem::create_directories(phys.parent_path());
        std::ofstream f(phys, std::ios::binary | std::ios::trunc);
        if (!f) throw std::runtime_error("Cannot write: " + path);
        f.write(reinterpret_cast<const char*>(data.data()), data.size());
    }

    bool Exists(const std::string& path) override {
        try { return std::filesystem::exists(ToPhysical(path)); } catch (...) { return false; }
    }

    std::vector<VfsFileInfo> Enumerate(const std::string& path) override {
        auto phys = ToPhysical(path);
        if (!std::filesystem::is_directory(phys)) return {};
        std::vector<VfsFileInfo> result;
        for (auto& entry : std::filesystem::directory_iterator(phys)) {
            bool isDir = entry.is_directory();
            int64_t len = isDir ? 0 : static_cast<int64_t>(entry.file_size());
            auto lwt = std::chrono::system_clock::now(); // approximate
            result.emplace_back(entry.path().filename().string(), isDir, len, lwt);
        }
        return result;
    }

    VfsFileInfo* GetFileInfo(const std::string& path) override {
        auto phys = ToPhysical(path);
        if (std::filesystem::is_directory(phys)) {
            _cachedInfo = VfsFileInfo(phys.filename().string(), true, 0,
                                      std::chrono::system_clock::now());
            return &_cachedInfo;
        }
        if (std::filesystem::is_regular_file(phys)) {
            _cachedInfo = VfsFileInfo(phys.filename().string(), false,
                static_cast<int64_t>(std::filesystem::file_size(phys)),
                std::chrono::system_clock::now());
            return &_cachedInfo;
        }
        return nullptr;
    }

    void CreateDirectory(const std::string& path) override {
        std::filesystem::create_directories(ToPhysical(path));
    }

    void Delete(const std::string& path) override {
        auto phys = ToPhysical(path);
        if (std::filesystem::exists(phys)) std::filesystem::remove_all(phys);
    }

private:
    std::filesystem::path _root;
    mutable VfsFileInfo   _cachedInfo;

    std::filesystem::path ToPhysical(const std::string& path) const {
        std::filesystem::path p = _root / path;
        // Prevent path traversal: check that the canonical path starts with root
        auto canonical = std::filesystem::weakly_canonical(p);
        auto rootStr   = _root.string();
        auto candStr   = canonical.string();
        if (candStr.rfind(rootStr, 0) != 0)
            throw std::runtime_error("Path escapes VFS root");
        return canonical;
    }
};

} // namespace Adamantite::VFS
