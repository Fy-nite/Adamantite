#pragma once

#include "Canvas.hpp"

namespace Adamantite::GFX {

// Named color constants
struct Colors {
    static const Color White;
    static const Color Black;
    static const Color Gray;
    static const Color DarkGray;
    static const Color Cyan;
    static const Color Magenta;
    static const Color Yellow;
    static const Color Orange;
    static const Color Green;
    static const Color DarkGreen;
};

inline const Color Colors::White    {255, 255, 255, 255};
inline const Color Colors::Black    {  0,   0,   0, 255};
inline const Color Colors::Gray     {160, 160, 160, 255};
inline const Color Colors::DarkGray { 80,  80,  80, 255};
inline const Color Colors::Cyan     {  0, 255, 255, 255};
inline const Color Colors::Magenta  {255,   0, 255, 255};
inline const Color Colors::Yellow   {255, 255,   0, 255};
inline const Color Colors::Orange   {255, 165,   0, 255};
inline const Color Colors::Green    {  0, 128,   0, 255};
inline const Color Colors::DarkGreen{  0, 100,   0, 255};

} // namespace Adamantite::GFX
