#pragma once

#include "ColorUtil.hpp"
#include <cstdint>
#include <stdexcept>
#include <algorithm>
#include <cmath>

namespace Adamantite::GPU {

// A software pixel surface. Pixel format: 0xAARRGGBB
class Surface {
public:
    int Width() const  { return _width; }
    int Height() const { return _height; }
    uint32_t* Pixels() { return _pixels.data(); }
    const uint32_t* Pixels() const { return _pixels.data(); }

    Surface(int width, int height)
        : _width(width), _height(height), _pixels(width * height, 0)
    {
        if (width <= 0)  throw std::invalid_argument("width");
        if (height <= 0) throw std::invalid_argument("height");
    }

    void Clear(uint32_t color) {
        for (auto& p : _pixels) p = color;
    }

    void SetPixel(int x, int y, uint32_t color) {
        if (x < 0 || x >= _width || y < 0 || y >= _height) return;
        int idx = y * _width + x;
        _pixels[idx] = ColorUtil::Blend(color, _pixels[idx]);
    }

    uint32_t GetPixel(int x, int y) const {
        if (x < 0 || x >= _width || y < 0 || y >= _height) return 0;
        return _pixels[y * _width + x];
    }

    void FillRect(int x, int y, int w, int h, uint32_t color) {
        int x0 = std::max(0, x);
        int y0 = std::max(0, y);
        int x1 = std::min(_width,  x + w);
        int y1 = std::min(_height, y + h);
        for (int yy = y0; yy < y1; yy++) {
            int base = yy * _width;
            for (int xx = x0; xx < x1; xx++) {
                _pixels[base + xx] = ColorUtil::Blend(color, _pixels[base + xx]);
            }
        }
    }

    void Blit(const Surface& src, int dstX, int dstY) {
        for (int sy = 0; sy < src._height; sy++) {
            int ty = dstY + sy;
            if (ty < 0 || ty >= _height) continue;
            int srcRow = sy * src._width;
            int dstRow = ty * _width;
            for (int sx = 0; sx < src._width; sx++) {
                int tx = dstX + sx;
                if (tx < 0 || tx >= _width) continue;
                uint32_t s = src._pixels[srcRow + sx];
                _pixels[dstRow + tx] = ColorUtil::Blend(s, _pixels[dstRow + tx]);
            }
        }
    }

    // Draw a textured quad using nearest sampling and tinting
    void DrawTexturedQuad(const Surface& texture, int dstX, int dstY, int dstW, int dstH, uint32_t tint) {
        for (int yy = 0; yy < dstH; yy++) {
            int ty = dstY + yy;
            if (ty < 0 || ty >= _height) continue;
            int dstRow = ty * _width;
            float v = (dstH == 0) ? 0.0f : (yy + 0.5f) / dstH;
            int srcY = std::max(0, std::min(static_cast<int>(v * texture._height), texture._height - 1));
            for (int xx = 0; xx < dstW; xx++) {
                int tx = dstX + xx;
                if (tx < 0 || tx >= _width) continue;
                float u = (dstW == 0) ? 0.0f : (xx + 0.5f) / dstW;
                int srcX = std::max(0, std::min(static_cast<int>(u * texture._width), texture._width - 1));
                uint32_t s = texture._pixels[srcY * texture._width + srcX];
                uint32_t tinted = ColorUtil::Multiply(s, tint);
                _pixels[dstRow + tx] = ColorUtil::Blend(tinted, _pixels[dstRow + tx]);
            }
        }
    }

private:
    int _width;
    int _height;
    std::vector<uint32_t> _pixels;
};

} // namespace Adamantite::GPU
