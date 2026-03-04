#pragma once

#include "Surface.hpp"
#include "Texture.hpp"
#include "BitmapFont.hpp"
#include "CommandBuffer.hpp"
#include "SimpleGPURunner.hpp"
#include "IRenderBackend.hpp"
#include "Interop/SDLRendererNative.hpp"
#include <string>
#include <memory>
#include <stdexcept>

namespace Adamantite::GPU {

class SDLAdapterRenderer : public VBlank::Abstractions::IRenderBackend {
public:
    Surface& Target() { return *_target; }
    const BitmapFont& Font() const { return _font; }
    CommandBuffer& CmdBuffer() { return _commandBuffer; }

    SDLAdapterRenderer(std::unique_ptr<Surface> target, const std::string& windowTitle = "VBlank SDL")
        : _target(std::move(target)), _runner(*_target), _nativeHandle(nullptr)
    {
        if (!_target) throw std::invalid_argument("target cannot be null");
        _nativeHandle = renderer_create();
        if (!_nativeHandle) throw std::runtime_error("Failed to create native renderer.");
        if (!renderer_init(_nativeHandle, _target->Width(), _target->Height(), windowTitle.c_str())) {
            renderer_destroy(_nativeHandle);
            _nativeHandle = nullptr;
            throw std::runtime_error("Failed to initialize native SDL renderer.");
        }
    }

    ~SDLAdapterRenderer() override { Dispose(); }

    // IRenderBackend
    void Initialize(void* /*engine*/, void* /*canvas*/) override {
        // No-op: constructor already initializes native renderer and target surface
    }

    void Upload(void* /*canvas*/, std::vector<VBlank::Abstractions::Rect> /*regions*/) override {
        // Full present via Submit(); region upload not separately tracked
    }

    void Present() override { Submit(); }

    bool PumpEvents() override { return true; }

    // Direct methods
    void Clear(uint32_t color) { _target->Clear(color); }

    void FillRect(int x, int y, int w, int h, uint32_t color) {
        _target->FillRect(x, y, w, h, color);
    }

    void DrawSurface(const Surface& src, int x, int y) {
        _target->Blit(src, x, y);
    }

    void DrawQuad(Texture* texture, int x, int y, int w, int h, uint32_t tint) {
        _commandBuffer.Add(std::make_unique<DrawQuadCall>(texture, x, y, w, h, tint));
    }

    void Submit() {
        _runner.Execute(_commandBuffer);
        _commandBuffer.Clear();
        if (_nativeHandle) {
            renderer_begin_frame(_nativeHandle);
            renderer_clear(_nativeHandle, 0.0f, 0.0f, 0.0f, 1.0f);
            renderer_present_pixels(_nativeHandle, _target->Pixels(),
                _target->Width(), _target->Height());
            renderer_end_frame(_nativeHandle);
        }
    }

    void DrawText(const std::string& text, int x, int y, uint32_t color) {
        if (text.empty()) return;
        int cx = x;
        for (char c : text) {
            DrawChar(c, cx, y, color);
            cx += _font.CharWidth();
        }
    }

    void DrawChar(char c, int x, int y, uint32_t color) {
        const uint8_t* glyph = _font.GetGlyph(c);
        for (int row = 0; row < _font.CharHeight(); row++) {
            uint8_t bits = glyph[row];
            for (int col = 0; col < _font.CharWidth(); col++) {
                if (bits & (1 << (7 - col))) {
                    _target->SetPixel(x + col, y + row, color);
                }
            }
        }
    }

    void Shutdown() {
        if (_nativeHandle) {
            renderer_shutdown(_nativeHandle);
        }
    }

    void Dispose() {
        Shutdown();
        if (_nativeHandle) {
            renderer_destroy(_nativeHandle);
            _nativeHandle = nullptr;
        }
    }

private:
    std::unique_ptr<Surface> _target;
    BitmapFont _font;
    CommandBuffer _commandBuffer;
    SimpleGPURunner _runner;
    RendererHandle _nativeHandle;
};

} // namespace Adamantite::GPU
