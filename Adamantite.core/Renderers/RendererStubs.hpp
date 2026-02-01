#pragma once

// Abstract renderer interface for pluggable backends
class IRenderer {
public:
    virtual ~IRenderer() = default;
    virtual bool Init(int width, int height, const char* title) = 0;
    virtual void BeginFrame() = 0;
    virtual void EndFrame() = 0;
    virtual void Clear(float r, float g, float b, float a) = 0;
    // Add more rendering methods as needed (draw sprite, text, etc.)
    virtual void Shutdown() = 0;
};

// Factory function to create an SDL renderer
IRenderer* CreateSDLRenderer();
