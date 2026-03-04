#pragma once

#include <cstdint>
#include <algorithm>
#include <cmath>

namespace Adamantite::GPU {

class ColorUtil {
public:
    static uint32_t FromRgba(uint8_t r, uint8_t g, uint8_t b, uint8_t a = 255) {
        return (static_cast<uint32_t>(a) << 24)
             | (static_cast<uint32_t>(r) << 16)
             | (static_cast<uint32_t>(g) << 8)
             | static_cast<uint32_t>(b);
    }

    static uint8_t A(uint32_t argb) { return static_cast<uint8_t>(argb >> 24); }
    static uint8_t R(uint32_t argb) { return static_cast<uint8_t>(argb >> 16); }
    static uint8_t G(uint32_t argb) { return static_cast<uint8_t>(argb >> 8);  }
    static uint8_t B(uint32_t argb) { return static_cast<uint8_t>(argb);       }

    // Alpha blend src over dst (straight alpha)
    static uint32_t Blend(uint32_t src, uint32_t dst) {
        uint8_t sa = A(src);
        if (sa == 0)   return dst;
        if (sa == 255) return src;

        float alpha = sa / 255.0f;
        uint8_t sr = R(src), sg = G(src), sb = B(src);
        uint8_t dr = R(dst), dg = G(dst), db = B(dst);
        uint8_t da = A(dst);

        auto clamp8 = [](int v) -> uint8_t {
            return static_cast<uint8_t>(std::max(0, std::min(255, v)));
        };

        uint8_t outR = clamp8(static_cast<int>(sr * alpha + dr * (1 - alpha)));
        uint8_t outG = clamp8(static_cast<int>(sg * alpha + dg * (1 - alpha)));
        uint8_t outB = clamp8(static_cast<int>(sb * alpha + db * (1 - alpha)));
        uint8_t outA = clamp8(static_cast<int>(sa + da * (1 - alpha)));
        return FromRgba(outR, outG, outB, outA);
    }

    // Multiply color channels (including alpha) component-wise
    static uint32_t Multiply(uint32_t color, uint32_t tint) {
        uint8_t sa = A(color), sr = R(color), sg = G(color), sb = B(color);
        uint8_t ta = A(tint),  tr = R(tint),  tg = G(tint),  tb = B(tint);
        return FromRgba(
            static_cast<uint8_t>((sr * tr) / 255),
            static_cast<uint8_t>((sg * tg) / 255),
            static_cast<uint8_t>((sb * tb) / 255),
            static_cast<uint8_t>((sa * ta) / 255)
        );
    }
};

} // namespace Adamantite::GPU
