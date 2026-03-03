#pragma once

#include <vector>

namespace VBlank::Abstractions {

struct Rect {
    int X, Y, W, H;
};

// Minimal abstraction for a render backend.
class IRenderBackend {
public:
    virtual ~IRenderBackend() = default;

    // Initialize backend with opaque engine and canvas objects.
    virtual void Initialize(void* engine, void* canvas) = 0;

    // Upload pixel data from the canvas.
    virtual void Upload(void* canvas, std::vector<Rect> regions) = 0;

    // Present the current frame to the screen.
    virtual void Present() = 0;

    // Handle any pending events; return false to indicate the app should exit.
    virtual bool PumpEvents() = 0;
};

} // namespace VBlank::Abstractions
