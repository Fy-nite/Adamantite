#pragma once

#include "Canvas.hpp"
#include <string>
#include <cstdlib>

namespace Adamantite::GFX {

// Free functions that extend Canvas (replaces C# extension methods)

inline void DrawRect(Canvas& c, int x, int y, int w, int h, Color color) {
    c.DrawFilledRect(x, y, w, h, color);
}

inline void DrawLine(Canvas& c, int x0, int y0, int x1, int y1, Color color) {
    int dx = std::abs(x1 - x0);
    int dy = std::abs(y1 - y0);
    int sx = x0 < x1 ? 1 : -1;
    int sy = y0 < y1 ? 1 : -1;
    int err = dx - dy;

    while (true) {
        c.SetPixel(x0, y0, color);
        if (x0 == x1 && y0 == y1) break;
        int e2 = 2 * err;
        if (e2 > -dy) { err -= dy; x0 += sx; }
        if (e2 < dx)  { err += dx; y0 += sy; }
    }
}

inline void DrawOutlinedRect(Canvas& c, int x, int y, int w, int h, Color color) {
    if (w <= 0 || h <= 0) return;
    DrawLine(c, x, y, x + w - 1, y, color);
    DrawLine(c, x, y + h - 1, x + w - 1, y + h - 1, color);
    DrawLine(c, x, y, x, y + h - 1, color);
    DrawLine(c, x + w - 1, y, x + w - 1, y + h - 1, color);
}

// Forward declaration; defined in CanvasTextHelper.hpp
void DrawText(Canvas& c, int x, int y, const std::string& text, Color color);

} // namespace Adamantite::GFX
