#pragma once

#include "Canvas.hpp"
#include "CanvasExtensions.hpp"
#include <string>
#include <unordered_map>
#include <vector>
#include <cctype>

namespace Adamantite::GFX {

class CanvasTextHelper {
public:
    static constexpr int GlyphWidth  = 5;
    static constexpr int GlyphHeight = 7;
    static constexpr int GlyphSpacing = 1;

    static void Prin(Canvas& c, int x, int y, const std::string& text, Color color) {
        if (text.empty()) return;

        int cursorX = x;
        int cursorY = y;
        const auto& glyphs = GetGlyphs();

        for (char raw : text) {
            if (raw == '\n') {
                cursorX = x;
                cursorY += GlyphHeight + GlyphSpacing;
                continue;
            }
            char ch = static_cast<char>(std::toupper(static_cast<unsigned char>(raw)));
            auto it = glyphs.find(ch);
            const uint8_t* glyph = (it != glyphs.end()) ? it->second.data() : glyphs.at('?').data();

            for (int row = 0; row < GlyphHeight; row++) {
                uint8_t rowBits = glyph[row];
                for (int col = 0; col < GlyphWidth; col++) {
                    int mask = 1 << (GlyphWidth - 1 - col);
                    if (rowBits & mask) {
                        c.SetPixel(cursorX + col, cursorY + row, color);
                    }
                }
            }
            cursorX += GlyphWidth + GlyphSpacing;
        }
    }

private:
    using GlyphData = std::vector<uint8_t>;

    static const std::unordered_map<char, GlyphData>& GetGlyphs() {
        static const std::unordered_map<char, GlyphData> glyphs = {
            {'A', {0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001}},
            {'B', {0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110}},
            {'C', {0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110}},
            {'D', {0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110}},
            {'E', {0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111}},
            {'F', {0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000}},
            {'G', {0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01110}},
            {'H', {0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001}},
            {'I', {0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110}},
            {'J', {0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100}},
            {'K', {0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001}},
            {'L', {0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111}},
            {'M', {0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001}},
            {'N', {0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001}},
            {'O', {0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110}},
            {'P', {0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000}},
            {'Q', {0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101}},
            {'R', {0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001}},
            {'S', {0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110}},
            {'T', {0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100}},
            {'U', {0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110}},
            {'V', {0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100}},
            {'W', {0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010}},
            {'X', {0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001}},
            {'Y', {0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100}},
            {'Z', {0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111}},
            {'0', {0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110}},
            {'1', {0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110}},
            {'2', {0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111}},
            {'3', {0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110}},
            {'4', {0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010}},
            {'5', {0b11111, 0b10000, 0b10000, 0b11110, 0b00001, 0b00001, 0b11110}},
            {'6', {0b01110, 0b10000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110}},
            {'7', {0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000}},
            {'8', {0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110}},
            {'9', {0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00001, 0b01110}},
            {' ', {0, 0, 0, 0, 0, 0, 0}},
            {'.', {0, 0, 0, 0, 0, 0b00100, 0b00100}},
            {',', {0, 0, 0, 0, 0b00100, 0b00100, 0b01000}},
            {':', {0, 0b00100, 0b00100, 0, 0b00100, 0b00100, 0}},
            {';', {0, 0b00100, 0b00100, 0, 0b00100, 0b00100, 0b01000}},
            {'-', {0, 0, 0, 0b11111, 0, 0, 0}},
            {'=', {0, 0b11111, 0, 0b11111, 0, 0, 0}},
            {'/', {0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0, 0}},
            {'[', {0b01110, 0b01000, 0b01000, 0b01000, 0b01000, 0b01000, 0b01110}},
            {']', {0b01110, 0b00010, 0b00010, 0b00010, 0b00010, 0b00010, 0b01110}},
            {'(', {0b00010, 0b00100, 0b01000, 0b01000, 0b01000, 0b00100, 0b00010}},
            {')', {0b01000, 0b00100, 0b00010, 0b00010, 0b00010, 0b00100, 0b01000}},
            {'+', {0, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0}},
            {'!', {0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0, 0b00100}},
            {'?', {0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0, 0b00100}},
            {'_', {0, 0, 0, 0, 0, 0, 0b11111}},
            {'"', {0b01010, 0b01010, 0, 0, 0, 0, 0}},
            {'\\',{0b10000, 0b01000, 0b00100, 0b00010, 0b00001, 0, 0}},
        };
        return glyphs;
    }
};

// Implement the DrawText free function declared in CanvasExtensions
inline void DrawText(Canvas& c, int x, int y, const std::string& text, Color color) {
    CanvasTextHelper::Prin(c, x, y, text, color);
}

} // namespace Adamantite::GFX
