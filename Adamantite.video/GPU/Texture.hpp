#pragma once

#include "Surface.hpp"
#include <memory>
#include <stdexcept>

namespace Adamantite::GPU {

// Wrapper for a source Surface used as a texture resource
class Texture {
public:
    Surface* SurfacePtr() const { return _surface.get(); }
    int Width() const  { return _surface->Width(); }
    int Height() const { return _surface->Height(); }

    explicit Texture(std::unique_ptr<Surface> surface)
        : _surface(std::move(surface))
    {
        if (!_surface) throw std::invalid_argument("surface cannot be null");
    }

private:
    std::unique_ptr<Surface> _surface;
};

} // namespace Adamantite::GPU
