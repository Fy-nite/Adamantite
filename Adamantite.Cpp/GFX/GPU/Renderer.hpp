#pragma once

#include "Surface.hpp"
#include "Texture.hpp"
#include "BitmapFont.hpp"
#include "CommandBuffer.hpp"
#include "SimpleGPURunner.hpp"
#include <string>
#include <memory>
#include <stdexcept>

namespace Adamantite::GPU {

class Renderer {
public:
    Surface& Target() { return *_target; }
    const BitmapFont& Font() const { return _font; }
    CommandBuffer& CmdBuffer() { return _commandBuffer; }

    explicit Renderer(std::unique_ptr<Surface> target)
        : _target(std::move(target)), _runner(*_target)
    {
        if (!_target) throw std::invalid_argument("target cannot be null");
    }

    void Clear(uint32_t color) { _target->Clear(color); }

    void FillRect(int x, int y, int w, int h, uint32_t color) {
        _target->FillRect(x, y, w, h, color);
    }

    void DrawSurface(const Surface& src, int x, int y) {
        _target->Blit(src, x, y);
    }

    // Enqueue a draw quad call
    void DrawQuad(Texture* texture, int x, int y, int w, int h, uint32_t tint) {
        _commandBuffer.Add(std::make_unique<DrawQuadCall>(texture, x, y, w, h, tint));
    }

    // Execute all queued commands
    void Submit() {
        _runner.Execute(_commandBuffer);
        _commandBuffer.Clear();
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

private:
    std::unique_ptr<Surface> _target;
    BitmapFont _font;
    CommandBuffer _commandBuffer;
    SimpleGPURunner _runner;
};

} // namespace Adamantite::GPU
