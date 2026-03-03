#pragma once

#include "Surface.hpp"
#include "Texture.hpp"
#include <vector>
#include <memory>
#include <stdexcept>

namespace Adamantite::GPU {

// Base command interface
struct ICommand {
    virtual ~ICommand() = default;
};

// Draw a textured quad with an optional tint
struct DrawQuadCall : public ICommand {
    Texture* texture;  // non-owning pointer
    int x, y, width, height;
    uint32_t tint;

    DrawQuadCall(Texture* tex, int x, int y, int w, int h, uint32_t tint)
        : texture(tex), x(x), y(y), width(w), height(h), tint(tint) {}
};

// Simple command buffer for GPU-style draw calls
class CommandBuffer {
public:
    void Clear() { _commands.clear(); }

    void Add(std::unique_ptr<ICommand> cmd) {
        if (!cmd) throw std::invalid_argument("cmd cannot be null");
        _commands.push_back(std::move(cmd));
    }

    const std::vector<std::unique_ptr<ICommand>>& Commands() const { return _commands; }

private:
    std::vector<std::unique_ptr<ICommand>> _commands;
};

} // namespace Adamantite::GPU
