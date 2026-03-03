#pragma once

#include "Surface.hpp"
#include "BitmapFont.hpp"
#include "../Canvas.hpp"
#include <string>
#include <vector>
#include <functional>
#include <algorithm>
#include <stdexcept>
#include <sstream>

namespace Adamantite::GPU {

// Minimal Virtual Terminal (TTY) implementation.
// Provides a character grid with foreground/background colors and
// rendering into a Surface using the built-in BitmapFont.
class VirtualTerminal {
public:
    // Options controlling text layout/rendering
    struct TextRenderOptions {
        int CharSpacing = 1;
        int LineSpacing  = 10;
        int PaddingX     = 1;
        int PaddingY     = 1;

        static TextRenderOptions Default() { return {}; }
    };

    TextRenderOptions RenderOptions;
    bool FillScreen = true;

    int Columns() const { return _columns; }
    int Rows()    const { return _rows; }
    int CursorX() const { return _cursorX; }
    int CursorY() const { return _cursorY; }

    uint32_t DefaultForeground = 0xFFFFFFFF; // white
    uint32_t DefaultBackground = 0xFF000000; // black

    // Enter event callback: called with the current line content on Enter key
    std::function<void(const std::string&)> OnEnter;

    VirtualTerminal(int columns, int rows)
        : _columns(columns), _rows(rows), _cursorX(0), _cursorY(0)
    {
        if (columns <= 0) throw std::invalid_argument("columns");
        if (rows <= 0)    throw std::invalid_argument("rows");
        _chars.assign(rows * columns, ' ');
        _fg.assign(rows * columns, DefaultForeground);
        _bg.assign(rows * columns, DefaultBackground);
    }

    void Clear() {
        std::fill(_chars.begin(), _chars.end(), ' ');
        std::fill(_fg.begin(), _fg.end(), DefaultForeground);
        std::fill(_bg.begin(), _bg.end(), DefaultBackground);
        _cursorX = 0; _cursorY = 0;
    }

    void Resize(int columns, int rows) {
        if (columns <= 0 || rows <= 0) return;
        std::vector<char>     nc(rows * columns, ' ');
        std::vector<uint32_t> nf(rows * columns, DefaultForeground);
        std::vector<uint32_t> nb(rows * columns, DefaultBackground);
        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < columns; c++) {
                if (r < _rows && c < _columns) {
                    nc[r * columns + c] = _chars[r * _columns + c];
                    nf[r * columns + c] = _fg[r * _columns + c];
                    nb[r * columns + c] = _bg[r * _columns + c];
                }
            }
        }
        _chars = std::move(nc); _fg = std::move(nf); _bg = std::move(nb);
        _columns = columns; _rows = rows;
        _cursorX = std::min(_cursorX, _columns - 1);
        _cursorY = std::min(_cursorY, _rows - 1);
    }

    void Write(const std::string& text) {
        if (text.empty()) return;
        for (char ch : text) {
            switch (ch) {
                case '\r': _cursorX = 0; break;
                case '\n': _cursorX = 0; _cursorY++; break;
                case '\b':
                    if (_cursorX > 0) { _cursorX--; _chars[_cursorY * _columns + _cursorX] = ' '; }
                    break;
                case '\t': {
                    int spaces = 4 - (_cursorX % 4);
                    for (int i = 0; i < spaces; i++) PutChar(' ');
                    break;
                }
                default: PutChar(ch); break;
            }
            if (_cursorY >= _rows) { ScrollUp(1); _cursorY = _rows - 1; }
        }
    }

    void WriteLine(const std::string& text) {
        Write(text);
        _cursorX = 0; _cursorY++;
        if (_cursorY >= _rows) { ScrollUp(1); _cursorY = _rows - 1; }
    }

    void PutChar(char c) {
        if (c == '\0') return;
        if (_cursorX < 0) _cursorX = 0;
        if (_cursorX >= _columns) { _cursorX = 0; _cursorY++; }
        if (_cursorY < 0) _cursorY = 0;
        if (_cursorY >= _rows) { ScrollUp(_cursorY - _rows + 1); _cursorY = _rows - 1; }
        int idx = _cursorY * _columns + _cursorX;
        _chars[idx] = c;
        _fg[idx] = DefaultForeground;
        _bg[idx] = DefaultBackground;
        _cursorX++;
    }

    void PutStringAt(const std::string& text, int x, int y, uint32_t fg, uint32_t bg) {
        if (y < 0 || y >= _rows) return;
        int cx = std::max(0, std::min(x, _columns - 1));
        for (char ch : text) {
            if (cx >= _columns) break;
            int idx = y * _columns + cx;
            _chars[idx] = ch; _fg[idx] = fg; _bg[idx] = bg;
            cx++;
        }
    }

    std::string GetCurrentLine() const {
        if (_cursorY < 0 || _cursorY >= _rows) return {};
        std::string result;
        for (int x = 0; x < _columns; x++) {
            char ch = _chars[_cursorY * _columns + x];
            if (ch == '\0') break;
            result += ch;
        }
        // trim trailing spaces
        while (!result.empty() && result.back() == ' ') result.pop_back();
        return result;
    }

    // Render the terminal into the provided Surface
    void RenderToSurface(Surface& surface) {
        int charW = _font.CharWidth();
        int charH = _font.CharHeight();
        int cellW = charW + RenderOptions.CharSpacing;
        int cellH = charH + RenderOptions.LineSpacing;
        int vtW = _columns * cellW + RenderOptions.PaddingX * 2;
        int vtH = _rows    * cellH + RenderOptions.PaddingY * 2;

        int scaleX = surface.Width()  / std::max(1, vtW);
        int scaleY = surface.Height() / std::max(1, vtH);
        int scale = 1, offsX = 0, offsY = 0;
        int perX = std::max(1, scaleX), perY = std::max(1, scaleY);

        if (!FillScreen) {
            scale = std::max(1, std::min(scaleX, scaleY));
            offsX = (surface.Width()  - vtW * scale) / 2;
            offsY = (surface.Height() - vtH * scale) / 2;
        }

        for (int row = 0; row < _rows; row++) {
            for (int col = 0; col < _columns; col++) {
                int baseX, baseY;
                if (FillScreen) {
                    baseX = offsX + RenderOptions.PaddingX + col * cellW * perX;
                    baseY = offsY + RenderOptions.PaddingY + row * cellH * perY;
                    surface.FillRect(baseX, baseY, cellW * perX, cellH * perY, _bg[row * _columns + col]);
                } else {
                    baseX = offsX + RenderOptions.PaddingX + col * cellW * scale;
                    baseY = offsY + RenderOptions.PaddingY + row * cellH * scale;
                    surface.FillRect(baseX, baseY, cellW * scale, cellH * scale, _bg[row * _columns + col]);
                }

                char ch = _chars[row * _columns + col];
                uint32_t fg = _fg[row * _columns + col];
                const uint8_t* glyph = _font.GetGlyph(ch);
                for (int gy = 0; gy < charH; gy++) {
                    uint8_t bits = glyph[gy];
                    for (int gx = 0; gx < charW; gx++) {
                        if (bits & (1 << (7 - gx))) {
                            if (FillScreen)
                                surface.FillRect(baseX + gx * perX, baseY + gy * perY, perX, perY, fg);
                            else
                                surface.FillRect(baseX + gx * scale, baseY + gy * scale, scale, scale, fg);
                        }
                    }
                }
            }
        }
    }

    // Render into a GFX Canvas
    void RenderToCanvas(GFX::Canvas& canvas) {
        int charW = _font.CharWidth();
        int charH = _font.CharHeight();
        int cellW = charW + RenderOptions.CharSpacing;
        int cellH = charH + RenderOptions.LineSpacing;
        int vtW = _columns * cellW + RenderOptions.PaddingX * 2;
        int vtH = _rows    * cellH + RenderOptions.PaddingY * 2;

        int scaleX = canvas.width  / std::max(1, vtW);
        int scaleY = canvas.height / std::max(1, vtH);
        int scale = 1, offsX = 0, offsY = 0;
        int perX = std::max(1, scaleX), perY = std::max(1, scaleY);

        if (!FillScreen) {
            scale = std::max(1, std::min(scaleX, scaleY));
            offsX = (canvas.width  - vtW * scale) / 2;
            offsY = (canvas.height - vtH * scale) / 2;
        }

        for (int row = 0; row < _rows; row++) {
            for (int col = 0; col < _columns; col++) {
                int baseX = offsX + RenderOptions.PaddingX + col * cellW * scale;
                int baseY = offsY + RenderOptions.PaddingY + row * cellH * scale;

                auto bgColor = GFX::Canvas::ColorFromLong(static_cast<long>(_bg[row * _columns + col]));
                if (FillScreen) {
                    canvas.DrawFilledRect(baseX, baseY, cellW * perX, cellH * perY, bgColor);
                } else {
                    canvas.DrawFilledRect(baseX, baseY, cellW * scale, cellH * scale, bgColor);
                }

                char ch = _chars[row * _columns + col];
                uint32_t fg = _fg[row * _columns + col];
                auto fgColor = GFX::Canvas::ColorFromLong(static_cast<long>(fg));
                const uint8_t* glyph = _font.GetGlyph(ch);
                for (int gy = 0; gy < charH; gy++) {
                    uint8_t bits = glyph[gy];
                    for (int gx = 0; gx < charW; gx++) {
                        if (bits & (1 << (7 - gx))) {
                            if (FillScreen) {
                                int px = baseX + gx * perX;
                                int py = baseY + gy * perY;
                                canvas.DrawFilledRect(px, py, perX, perY, fgColor);
                            } else {
                                canvas.DrawFilledRect(baseX + gx * scale, baseY + gy * scale, scale, scale, fgColor);
                            }
                        }
                    }
                }
            }
        }
    }

private:
    int _columns, _rows;
    int _cursorX, _cursorY;
    std::vector<char>     _chars;
    std::vector<uint32_t> _fg;
    std::vector<uint32_t> _bg;
    BitmapFont _font;

    void ScrollUp(int lines) {
        if (lines <= 0) return;
        if (lines >= _rows) { Clear(); return; }
        for (int r = 0; r < _rows - lines; r++) {
            for (int c = 0; c < _columns; c++) {
                int dst = r * _columns + c, src = (r + lines) * _columns + c;
                _chars[dst] = _chars[src]; _fg[dst] = _fg[src]; _bg[dst] = _bg[src];
            }
        }
        for (int r = _rows - lines; r < _rows; r++) {
            for (int c = 0; c < _columns; c++) {
                int idx = r * _columns + c;
                _chars[idx] = ' '; _fg[idx] = DefaultForeground; _bg[idx] = DefaultBackground;
            }
        }
    }
};

} // namespace Adamantite::GPU
