#pragma once

#include <cstdint>
#include <vector>
#include <algorithm>

namespace Adamantite::GFX {

// Simple RGBA color struct (replaces MonoGame Color)
struct Color {
    uint8_t r, g, b, a;

    Color() : r(0), g(0), b(0), a(255) {}
    Color(uint8_t r, uint8_t g, uint8_t b, uint8_t a = 255) : r(r), g(g), b(b), a(a) {}

    static const Color White;
    static const Color Black;
};

// Canvas: software pixel buffer with dirty-rectangle tracking
class Canvas {
public:
    int x;
    int y;
    int width;
    int height;

    std::vector<Color> PixelData;

    Canvas(int width, int height);

    void Clear(Color c);
    void SetPixel(int x, int y, Color c);
    void DrawFilledRect(int startX, int startY, int w, int h, Color c);

    // Helper: convert packed 0xAARRGGBB into a Color
    static Color ColorFromLong(long value);

    void MarkDirtyRect(int x, int y, int w, int h);
    void ClearDirty();

    bool IsDirty() const { return _dirty; }
    int DirtyX() const { return _dirtyX; }
    int DirtyY() const { return _dirtyY; }
    int DirtyWidth() const { return _dirtyW; }
    int DirtyHeight() const { return _dirtyH; }

private:
    int _dirtyX;
    int _dirtyY;
    int _dirtyW;
    int _dirtyH;
    bool _dirty;
};

} // namespace Adamantite::GFX
