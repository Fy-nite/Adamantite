using System;
using Adamantite.GFX;

namespace VBlank
{
    // Local stub interface so render backends in this project can compile
    // without depending on the VBlank project assembly. This mirrors the
    // canonical `VBlank.IRenderBackend` but lives here as a compatibility shim.
    public partial interface IRenderBackendStubs : IDisposable
    {
        // Use object parameters to avoid depending on the engine assembly.
        void Initialize(object engine, Canvas canvas);
        void Present();
        bool PumpEvents();
    }

    // This file intentionally provides no AsmoGameEngine partial type to avoid
    // creating duplicate definitions across assemblies. Backend wiring is
    // handled in the VBlank project (Game1.cs) which creates adapters that use
    // the renderer implementations from this assembly.
}
