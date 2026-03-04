using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Adamantite.GFX;
using VBlank.Abstractions;

namespace Adamantite.GPU
{
    public class Renderer
    {
        public Surface Target { get; }
        public BitmapFont Font { get; }
        public CommandBuffer CommandBuffer { get; }
        private readonly SimpleGPURunner _runner;

        public Renderer(Surface target)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Font = new BitmapFont();
            CommandBuffer = new CommandBuffer();
            _runner = new SimpleGPURunner(Target);
        }

        public void Clear(uint color) => Target.Clear(color);

        public void FillRect(int x, int y, int w, int h, uint color) => Target.FillRect(x, y, w, h, color);

        public void DrawSurface(Surface src, int x, int y) => Target.Blit(src, x, y);

        // Enqueue a draw quad call (texture can be null to draw nothing)
        public void DrawQuad(Texture texture, int x, int y, int w, int h, uint tint)
        {
            CommandBuffer.Add(new DrawQuadCall(texture, x, y, w, h, tint));
        }

        // Execute all queued commands immediately
        public void Submit()
        {
            _runner.Execute(CommandBuffer);
            CommandBuffer.Clear();
        }

        public void DrawText(string text, int x, int y, uint color)
        {
            if (string.IsNullOrEmpty(text)) return;
            int cx = x;
            foreach (char c in text)
            {
                DrawChar(c, cx, y, color);
                cx += Font.CharWidth;
            }
        }

        public void DrawChar(char c, int x, int y, uint color)
        {
            var glyph = Font.GetGlyph(c);
            for (int row = 0; row < Font.CharHeight; row++)
            {
                byte bits = glyph[row];
                for (int col = 0; col < Font.CharWidth; col++)
                {
                    if ((bits & (1 << (7 - col))) != 0)
                    {
                        Target.SetPixel(x + col, y + row, color);
                    }
                }
            }
        }
    }

    // MonoGame-based helper that the VBlank adapter can use to present via MonoGame.
    public class MonoGameRenderBackend : VBlank.Abstractions.IRenderBackend
    {
        private Adamantite.GFX.Canvas _canvas = null!;
        private SpriteBatch? _spriteBatch;
        private Texture2D? _texture;
        private GraphicsDevice? _graphicsDevice;

        public MonoGameRenderBackend()
        {
        }

        // Initialize with a GraphicsDevice and the canvas to upload
        public void Initialize(object engineObj, object canvasObj)
        {
            if (engineObj is Microsoft.Xna.Framework.Graphics.GraphicsDevice gd && canvasObj is Adamantite.GFX.Canvas c)
            {
                _graphicsDevice = gd;
                _canvas = c;
                _spriteBatch = new SpriteBatch(_graphicsDevice);
                _texture = new Texture2D(_graphicsDevice, Math.Max(1, _canvas.width), Math.Max(1, _canvas.height), false, SurfaceFormat.Color);
                try
                {
                    _texture.SetData(_canvas.PixelData);
                }
                catch { }
            }
        }

        public void Upload(object canvasObj, List<VBlank.Abstractions.Rect> regions)
        {
            if (canvasObj is not Adamantite.GFX.Canvas canvas || _graphicsDevice == null) return;
            try
            {
                _texture?.Dispose();
                _texture = new Texture2D(_graphicsDevice, Math.Max(1, canvas.width), Math.Max(1, canvas.height), false, SurfaceFormat.Color);
                try
                {
                    _texture.SetData(canvas.PixelData);
                }
                catch { }
            }
            catch { }
        }

        public void Present()
        {
            // Draw uploaded texture to the backbuffer using the backend's SpriteBatch.
            if (_graphicsDevice == null) return;
            try
            {
                if (_texture == null || _spriteBatch == null) return;
                
                var bw = _graphicsDevice.PresentationParameters.BackBufferWidth;
                var bh = _graphicsDevice.PresentationParameters.BackBufferHeight;
                
                // Clear to black first
                _graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
                
                _spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Opaque);
                _spriteBatch.Draw(_texture, new Microsoft.Xna.Framework.Rectangle(0, 0, Math.Max(1, bw), Math.Max(1, bh)), Microsoft.Xna.Framework.Color.White);
                _spriteBatch.End();
            }
            catch { }
        }

        public bool PumpEvents() => true;

        public void Dispose()
        {
            try { _texture?.Dispose(); } catch { }
            try { _spriteBatch?.Dispose(); } catch { }
        }
    }
}
