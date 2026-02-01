using System;
using Adamantite.GPU;
using Adamantite.GFX;
using Microsoft.Xna.Framework.Input;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace Adamantite.GPU
{
    // Minimal Virtual Terminal (TTY) implementation.
    // Provides a character grid with foreground/background colors and
    // rendering into a Surface using the built-in BitmapFont.
    public class VirtualTerminal
    {
        // Options controlling text layout/rendering (spacing, padding)
        public struct TextRenderOptions
        {
            // Extra blank pixels added horizontally between characters
            public int CharSpacing { get; set; }
            // Extra blank pixels added vertically between lines
            public int LineSpacing { get; set; }
            // Padding in pixels applied around the terminal content when not FillScreen
            public int PaddingX { get; set; }
            public int PaddingY { get; set; }

            public static TextRenderOptions Default => new TextRenderOptions
            {
                CharSpacing = 1,
                LineSpacing = 10,
                PaddingX = 1,
                PaddingY = 1
            };
        }

        // Publicly accessible render options. Modify to change spacing/padding behavior.
        public TextRenderOptions RenderOptions { get; set; } = TextRenderOptions.Default;

        public int Columns { get; private set; }
        public int Rows { get; private set; }

        public BitmapFont Font { get; }

        // Buffers for characters and colors
        private char[,] _chars;
        private uint[,] _fg;
        private uint[,] _bg;

        // Cursor
        public int CursorX { get; private set; }
        public int CursorY { get; private set; }

        // Default colors (0xAARRGGBB)
        public uint DefaultForeground { get; set; } = 0xFFFFFFFF; // white
        public uint DefaultBackground { get; set; } = 0xFF000000; // black

        // Enter event for shells
        public event Action<string>? OnEnter;

        // Scaling behavior: when true, the VT will scale to fill the entire target
        // surface/canvas (separate integer X/Y scaling) and align to top-left. When
        // false the VT performs a uniform integer scale and centers the content.
        public bool FillScreen { get; set; } = true;

        public VirtualTerminal(int columns, int rows)
        {
            if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));

            Columns = columns;
            Rows = rows;

            Font = new BitmapFont();

            _chars = new char[Rows, Columns];
            _fg = new uint[Rows, Columns];
            _bg = new uint[Rows, Columns];

            Clear();
        }

        public void Clear()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    _chars[r, c] = ' ';
                    _fg[r, c] = DefaultForeground;
                    _bg[r, c] = DefaultBackground;
                }
            }
            CursorX = 0;
            CursorY = 0;
        }

        public void Resize(int columns, int rows)
        {
            if (columns <= 0 || rows <= 0) return;
            var newChars = new char[rows, columns];
            var newFg = new uint[rows, columns];
            var newBg = new uint[rows, columns];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (r < Rows && c < Columns)
                    {
                        newChars[r, c] = _chars[r, c];
                        newFg[r, c] = _fg[r, c];
                        newBg[r, c] = _bg[r, c];
                    }
                    else
                    {
                        newChars[r, c] = ' ';
                        newFg[r, c] = DefaultForeground;
                        newBg[r, c] = DefaultBackground;
                    }
                }
            }

            _chars = newChars;
            _fg = newFg;
            _bg = newBg;
            Columns = columns;
            Rows = rows;

            CursorX = Math.Min(CursorX, Columns - 1);
            CursorY = Math.Min(CursorY, Rows - 1);
        }

        // Basic write: handles \n, \r, \b and tabs; no ANSI sequences yet.
        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char ch in text)
            {
                switch (ch)
                {
                    case '\r':
                        CursorX = 0; break;
                    case '\n':
                        CursorX = 0; CursorY++; break;
                    case '\b':
                        if (CursorX > 0) CursorX--; _chars[CursorY, CursorX] = ' '; break;
                    case '\t':
                        int spaces = 4 - (CursorX % 4);
                        for (int i = 0; i < spaces; i++) PutChar(' ');
                        break;
                    default:
                        PutChar(ch); break;
                }

                if (CursorY >= Rows)
                {
                    ScrollUp(1);
                    CursorY = Rows - 1;
                }
            }
        }

        public void WriteLine(string text)
        {
            Write(text);
            // move to next line
            CursorX = 0;
            CursorY++;
            if (CursorY >= Rows)
            {
                ScrollUp(1);
                CursorY = Rows - 1;
            }
        }

        public void PutChar(char c)
        {
            if (c == '\0') return;
            if (CursorX < 0) CursorX = 0;
            if (CursorX >= Columns)
            {
                CursorX = 0;
                CursorY++;
            }
            if (CursorY < 0) CursorY = 0;
            if (CursorY >= Rows)
            {
                ScrollUp(CursorY - Rows + 1);
                CursorY = Rows - 1;
            }

            _chars[CursorY, CursorX] = c;
            _fg[CursorY, CursorX] = DefaultForeground;
            _bg[CursorY, CursorX] = DefaultBackground;
            CursorX++;
        }

        public void PutStringAt(string text, int x, int y, uint fg, uint bg)
        {
            if (y < 0 || y >= Rows) return;
            int cx = Math.Clamp(x, 0, Columns - 1);
            foreach (char ch in text)
            {
                if (cx >= Columns) break;
                _chars[y, cx] = ch;
                _fg[y, cx] = fg;
                _bg[y, cx] = bg;
                cx++;
            }
        }

        private void ScrollUp(int lines)
        {
            if (lines <= 0) return;
            if (lines >= Rows)
            {
                Clear();
                return;
            }

            for (int r = 0; r < Rows - lines; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    _chars[r, c] = _chars[r + lines, c];
                    _fg[r, c] = _fg[r + lines, c];
                    _bg[r, c] = _bg[r + lines, c];
                }
            }

            // Clear bottom lines
            for (int r = Rows - lines; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    _chars[r, c] = ' ';
                    _fg[r, c] = DefaultForeground;
                    _bg[r, c] = DefaultBackground;
                }
            }
        }

        // Return the current line under the cursor (used by shell)
        public string GetCurrentLine()
        {
            var sb = new StringBuilder();
            if (CursorY < 0 || CursorY >= Rows) return string.Empty;
            for (int x = 0; x < Columns; x++)
            {
                var ch = _chars[CursorY, x];
                if (ch == '\0') break;
                sb.Append(ch);
            }
            return sb.ToString().TrimEnd();
        }

        // Handle key input (basic mapping)
        public void HandleKey(Keys key, bool shift)
        {
            if (key == Keys.Back)
            {
                if (CursorX > 0)
                {
                    CursorX--;
                    _chars[CursorY, CursorX] = ' ';
                }
                return;
            }

            if (key == Keys.Enter)
            {
                OnEnter?.Invoke(GetCurrentLine());
                // newline
                CursorX = 0;
                CursorY++;
                if (CursorY >= Rows)
                {
                    ScrollUp(1);
                    CursorY = Rows - 1;
                }
                return;
            }

            if (key == Keys.Tab)
            {
                Write("    ");
                return;
            }

            if (TryMapKey(key, shift, out var ch))
            {
                PutChar(ch);
            }
        }

        static bool TryMapKey(Keys key, bool shift, out char ch)
        {
            ch = '\0';

            if (key >= Keys.A && key <= Keys.Z)
            {
                ch = (char)('A' + (key - Keys.A));
                if (!shift) ch = char.ToLowerInvariant(ch);
                return true;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                int d = key - Keys.D0;
                string normal = "0123456789";
                string shifted = ")!@#$%^&*(";
                ch = shift ? shifted[d] : normal[d];
                return true;
            }

            switch (key)
            {
                case Keys.Space: ch = ' '; return true;
                case Keys.OemPeriod: ch = shift ? '>' : '.'; return true;
                case Keys.OemComma: ch = shift ? '<' : ','; return true;
                case Keys.OemMinus: ch = shift ? '_' : '-'; return true;
                case Keys.OemPlus: ch = shift ? '+' : '='; return true;
                case Keys.OemQuestion: ch = shift ? '?' : '/'; return true;
                case Keys.OemSemicolon: ch = shift ? ':' : ';'; return true;
                case Keys.OemQuotes: ch = shift ? '"' : '\''; return true;
                case Keys.OemOpenBrackets: ch = shift ? '{' : '['; return true;
                case Keys.OemCloseBrackets: ch = shift ? '}' : ']'; return true;
                case Keys.OemPipe: ch = shift ? '|' : '\\'; return true;
                case Keys.OemTilde: ch = shift ? '~' : '`'; return true;
            }

            return false;
        }

        // Render the terminal into the provided Surface. The surface must be at least
        // Columns*Font.CharWidth by Rows*Font.CharHeight. Rendering draws background then glyphs.
        public void RenderToSurface(Surface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            // compute integer scale to fit terminal into surface while preserving aspect
            int charW = Font.CharWidth;
            int charH = Font.CharHeight;
            int cellW = charW + RenderOptions.CharSpacing;
            int cellH = charH + RenderOptions.LineSpacing;
            int vtW = Columns * cellW + RenderOptions.PaddingX * 2;
            int vtH = Rows * cellH + RenderOptions.PaddingY * 2;

            int scaleX = surface.Width / Math.Max(1, vtW);
            int scaleY = surface.Height / Math.Max(1, vtH);
            int scale;
            int offsX = 0;
            int offsY = 0;
            // per-axis integer scales for fill path
            int perX = Math.Max(1, scaleX);
            int perY = Math.Max(1, scaleY);
            if (FillScreen)
            {
                // use separate integer scales per-axis to fill the entire surface and align top-left
                scale = 1; // uniform scale unused in FillScreen path
            }
            else
            {
                // uniform integer scale, centered
                scale = Math.Max(1, Math.Min(scaleX, scaleY));
                offsX = (surface.Width - vtW * scale) / 2;
                offsY = (surface.Height - vtH * scale) / 2;
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int baseX;
                    int baseY;
                    if (FillScreen)
                    {
                        baseX = offsX + RenderOptions.PaddingX + col * cellW * perX;
                        baseY = offsY + RenderOptions.PaddingY + row * cellH * perY;
                    }
                    else
                    {
                        baseX = offsX + RenderOptions.PaddingX + col * cellW * scale;
                        baseY = offsY + RenderOptions.PaddingY + row * cellH * scale;
                    }

                    uint bg = _bg[row, col];
                    // fill bg rect (scaled)
                    if (FillScreen)
                    {
                        int sw = cellW * perX;
                        int sh = cellH * perY;
                        surface.FillRect(baseX, baseY, sw, sh, bg);
                    }
                    else
                    {
                        surface.FillRect(baseX, baseY, cellW * scale, cellH * scale, bg);
                    }

                    char ch = _chars[row, col];
                    uint fg = _fg[row, col];
                    var glyph = Font.GetGlyph(ch);
                    for (int gy = 0; gy < charH; gy++)
                    {
                        byte bits = glyph[gy];
                        for (int gx = 0; gx < charW; gx++)
                        {
                            if ((bits & (1 << (7 - gx))) != 0)
                            {
                                if (FillScreen)
                                {
                                    int px = baseX + gx * perX;
                                    int py = baseY + gy * perY;
                                    surface.FillRect(px, py, perX, perY, fg);
                                }
                                else
                                {
                                    // draw scaled pixel as a filled rect
                                    surface.FillRect(baseX + gx * scale, baseY + gy * scale, scale, scale, fg);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Render into Adamantite.GFX.Canvas so the runtime can draw the VT directly.
        public void RenderToCanvas(Canvas canvas)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));

            // compute integer scale to fit terminal into canvas while preserving aspect
            int charW = Font.CharWidth;
            int charH = Font.CharHeight;
            int cellW = charW + RenderOptions.CharSpacing;
            int cellH = charH + RenderOptions.LineSpacing;
            int vtW = Columns * cellW + RenderOptions.PaddingX * 2;
            int vtH = Rows * cellH + RenderOptions.PaddingY * 2;

            int scaleX = canvas.width / Math.Max(1, vtW);
            int scaleY = canvas.height / Math.Max(1, vtH);
            int scale;
            int offsX = 0;
            int offsY = 0;
            int perX = Math.Max(1, scaleX);
            int perY = Math.Max(1, scaleY);
            if (FillScreen)
            {
                // fill screen per-axis
                scale = 1;
            }
            else
            {
                scale = Math.Max(1, Math.Min(scaleX, scaleY));
                offsX = (canvas.width - vtW * scale) / 2;
                offsY = (canvas.height - vtH * scale) / 2;
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int baseX = offsX + RenderOptions.PaddingX + col * cellW * scale;
                    int baseY = offsY + RenderOptions.PaddingY + row * cellH * scale;

                    uint bg = _bg[row, col];
                    if (FillScreen)
                    {
                        int sw = cellW * perX;
                        int sh = cellH * perY;
                        canvas.DrawFilledRect(baseX, baseY, sw, sh, Canvas.ColorFromLong(bg));
                    }
                    else
                    {
                        canvas.DrawFilledRect(baseX, baseY, cellW * scale, cellH * scale, Canvas.ColorFromLong(bg));
                    }

                    char ch = _chars[row, col];
                    uint fg = _fg[row, col];
                    var glyph = Font.GetGlyph(ch);
                    for (int gy = 0; gy < charH; gy++)
                    {
                        byte bits = glyph[gy];
                        for (int gx = 0; gx < charW; gx++)
                        {
                            if ((bits & (1 << (7 - gx))) != 0)
                            {
                                if (FillScreen)
                                {
                                    int px = baseX + gx * perX;
                                    int py = baseY + gy * perY;
                                    canvas.DrawFilledRect(px, py, perX, perY, Canvas.ColorFromLong(fg));
                                }
                                else
                                {
                                    // draw scaled pixel
                                    canvas.DrawFilledRect(baseX + gx * scale, baseY + gy * scale, scale, scale, Canvas.ColorFromLong(fg));
                                }
                            }
                        }
                    }
                }
            }
        }

        // Render a rectangular region (in pixel coordinates) into the Canvas. Used by runtime for partial redraws.
        public void RenderRegionToCanvas(Canvas canvas, int rectX, int rectY, int rectW, int rectH)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (rectW <= 0 || rectH <= 0) return;

            // compute scale/offset as in full render so region maps correctly
            int charW = Font.CharWidth;
            int charH = Font.CharHeight;
            int vtW = Columns * charW;
            int vtH = Rows * charH;

            int scaleX = canvas.width / Math.Max(1, vtW);
            int scaleY = canvas.height / Math.Max(1, vtH);
            int scale = Math.Max(1, Math.Min(scaleX, scaleY));
            int offsX = (canvas.width - vtW * scale) / 2;
            int offsY = (canvas.height - vtH * scale) / 2;

            int startCol = Math.Max(0, (rectX - offsX) / (charW * scale));
            int endCol = Math.Min(Columns - 1, (rectX + rectW - 1 - offsX) / (charW * scale));
            int startRow = Math.Max(0, (rectY - offsY) / (charH * scale));
            int endRow = Math.Min(Rows - 1, (rectY + rectH - 1 - offsY) / (charH * scale));

            for (int row = startRow; row <= endRow; row++)
            {
                for (int col = startCol; col <= endCol; col++)
                {
                    int px = col * Font.CharWidth;
                    int py = row * Font.CharHeight;

                    // Clip to requested rect
                    int clipX0 = Math.Max(rectX, px);
                    int clipY0 = Math.Max(rectY, py);
                    int clipX1 = Math.Min(rectX + rectW, px + Font.CharWidth);
                    int clipY1 = Math.Min(rectY + rectH, py + Font.CharHeight);
                    if (clipX1 <= clipX0 || clipY1 <= clipY0) continue;

                    uint bg = _bg[row, col];
                    canvas.DrawFilledRect(clipX0, clipY0, clipX1 - clipX0, clipY1 - clipY0, Canvas.ColorFromLong(bg));

                    char ch = _chars[row, col];
                    uint fg = _fg[row, col];
                    var glyph = Font.GetGlyph(ch);
                    for (int y = 0; y < Font.CharHeight; y++)
                    {
                        int pyRow = py + y;
                        if (pyRow < clipY0 || pyRow >= clipY1) continue;
                        byte bits = glyph[y];
                        for (int x = 0; x < Font.CharWidth; x++)
                        {
                            int pxCol = px + x;
                            if (pxCol < clipX0 || pxCol >= clipX1) continue;
                            if ((bits & (1 << (7 - x))) != 0)
                            {
                                canvas.SetPixel(pxCol, pyRow, Canvas.ColorFromLong(fg));
                            }
                        }
                    }
                }
            }
        }

        public void Draw(SpriteBatch sb, SpriteFont font, int x, int y, float scale)
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    char c = _chars[row, col];
                    if (c != ' ')
                    {
                        Color fgColor = new Color(_fg[row, col]);
                        Vector2 position = new Vector2(x + col * font.MeasureString(" ").X * scale, y + row * font.LineSpacing * scale);
                        sb.DrawString(font, c.ToString(), position, fgColor, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
                    }
                }
            }
        }
    }
}
