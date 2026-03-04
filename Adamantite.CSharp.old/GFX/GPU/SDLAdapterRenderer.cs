using System;
using Adamantite.Interop;
using VBlank.Abstractions;

namespace Adamantite.GPU
{
    public class SDLAdapterRenderer : IDisposable, VBlank.Abstractions.IRenderBackend
    {
        public Surface Target { get; }
        public BitmapFont Font { get; }
        public CommandBuffer CommandBuffer { get; }
        private readonly SimpleGPURunner _runner;
        private readonly SDLRendererNative _native;

        public SDLAdapterRenderer(Surface target, string windowTitle = "VBlank SDL")
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Font = new BitmapFont();
            CommandBuffer = new CommandBuffer();
            _runner = new SimpleGPURunner(Target);

            _native = new SDLRendererNative();
            if (!_native.Init(Target.Width, Target.Height, windowTitle))
            {
                throw new InvalidOperationException("Failed to initialize native SDL renderer.");
            }
        }

        // Present immediately (calls native submit)
        public void Present()
        {
            Submit();
        }

        // Initialization via abstraction interface
        public void Initialize(object engine, object canvas)
        {
            // No-op: constructor already initializes native renderer and target surface
        }

        // Pump events - SDL native handles its own event loop; return true to continue.
        public bool PumpEvents()
        {
            return true;
        }

        // Upload canvas pixels into the adapter's target surface. Regions parameter is
        // currently unused; we perform a full copy for simplicity.
        public void Upload(object canvasObj, System.Collections.Generic.List<VBlank.Abstractions.Rect> regions)
        {
            if (canvasObj is not Adamantite.GFX.Canvas canvas) return;
            int total = canvas.width * canvas.height;
            if (Target.Pixels.Length != total) return;
            var dst = Target.Pixels;
            var src = canvas.PixelData;
            for (int i = 0; i < total; i++)
            {
                var c = src[i];
                dst[i] = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
            }
        }

        public void Clear(uint color)
        {
            Target.Clear(color);
        }

        public void FillRect(int x, int y, int w, int h, uint color)
        {
            Target.FillRect(x, y, w, h, color);
        }

        public void DrawSurface(Surface src, int x, int y)
        {
            Target.Blit(src, x, y);
        }

        public void DrawQuad(Texture texture, int x, int y, int w, int h, uint tint)
        {
            CommandBuffer.Add(new DrawQuadCall(texture, x, y, w, h, tint));
        }

        public void Submit()
        {
            _runner.Execute(CommandBuffer);
            CommandBuffer.Clear();
            _native.BeginFrame();
            // Clear with transparent black; convert from uint color if needed
            _native.Clear(0f, 0f, 0f, 1f);
            _native.Present(Target);
            _native.EndFrame();
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

        public void Shutdown()
        {
            _native?.Shutdown();
        }

        public void Dispose()
        {
            Shutdown();
            _native?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
