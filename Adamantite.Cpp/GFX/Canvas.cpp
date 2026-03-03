#include "Canvas.hpp"
#include <algorithm>
#include <cstdint>

namespace Adamantite::GFX {

const Color Color::White{255, 255, 255, 255};
const Color Color::Black{0, 0, 0, 255};

Canvas::Canvas(int width, int height)
    : x(0), y(0), width(width), height(height),
      PixelData(width * height),
      _dirtyX(0), _dirtyY(0), _dirtyW(0), _dirtyH(0), _dirty(false)
{
    MarkDirtyRect(0, 0, width, height);
}

void Canvas::Clear(Color c) {
    for (auto& p : PixelData) p = c;
    MarkDirtyRect(0, 0, width, height);
}

void Canvas::SetPixel(int px, int py, Color c) {
    if (px < 0 || px >= width || py < 0 || py >= height) return;
    PixelData[py * width + px] = c;
    MarkDirtyRect(px, py, 1, 1);
}

void Canvas::DrawFilledRect(int startX, int startY, int w, int h, Color c) {
    for (int yy = 0; yy < h; yy++) {
        int py = startY + yy;
        if (py < 0 || py >= height) continue;
        for (int xx = 0; xx < w; xx++) {
            int px = startX + xx;
            if (px < 0 || px >= width) continue;
            SetPixel(px, py, c);
        }
    }
    MarkDirtyRect(startX, startY, w, h);
}

Color Canvas::ColorFromLong(long value) {
    uint32_t v = static_cast<uint32_t>(value);
    uint8_t a = static_cast<uint8_t>(v >> 24);
    uint8_t r = static_cast<uint8_t>(v >> 16);
    uint8_t g = static_cast<uint8_t>(v >> 8);
    uint8_t b = static_cast<uint8_t>(v & 0xFF);
    return Color(r, g, b, a);
}

void Canvas::MarkDirtyRect(int rx, int ry, int rw, int rh) {
    if (rw <= 0 || rh <= 0) return;
    rx = std::max(0, std::min(rx, width));
    ry = std::max(0, std::min(ry, height));
    rw = std::max(0, std::min(rw, width - rx));
    rh = std::max(0, std::min(rh, height - ry));

    if (!_dirty) {
        _dirty = true;
        _dirtyX = rx; _dirtyY = ry; _dirtyW = rw; _dirtyH = rh;
        return;
    }

    int x0 = std::min(_dirtyX, rx);
    int y0 = std::min(_dirtyY, ry);
    int x1 = std::max(_dirtyX + _dirtyW, rx + rw);
    int y1 = std::max(_dirtyY + _dirtyH, ry + rh);
    _dirtyX = x0; _dirtyY = y0; _dirtyW = x1 - x0; _dirtyH = y1 - y0;
}

void Canvas::ClearDirty() {
    _dirty = false;
    _dirtyX = _dirtyY = _dirtyW = _dirtyH = 0;
}

} // namespace Adamantite::GFX
