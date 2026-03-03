#pragma once

#include "../Canvas.hpp"

namespace Adamantite::GFX::UI {

// Simple rectangle struct (replaces XNA/MonoGame Rectangle)
struct Rect {
    int X, Y, Width, Height;

    Rect() : X(0), Y(0), Width(0), Height(0) {}
    Rect(int x, int y, int w, int h) : X(x), Y(y), Width(w), Height(h) {}

    int Right()  const { return X + Width;  }
    int Bottom() const { return Y + Height; }

    bool Intersects(const Rect& o) const {
        return X < o.Right() && Right() > o.X && Y < o.Bottom() && Bottom() > o.Y;
    }

    static Rect Intersect(const Rect& a, const Rect& b) {
        int x = std::max(a.X, b.X);
        int y = std::max(a.Y, b.Y);
        int r = std::min(a.Right(),  b.Right());
        int bot = std::min(a.Bottom(), b.Bottom());
        if (r <= x || bot <= y) return {x, y, 0, 0};
        return {x, y, r - x, bot - y};
    }

    static Rect Union(const Rect& a, const Rect& b) {
        int x = std::min(a.X, b.X);
        int y = std::min(a.Y, b.Y);
        int r = std::max(a.Right(),  b.Right());
        int bot = std::max(a.Bottom(), b.Bottom());
        return {x, y, r - x, bot - y};
    }
};

class UIManager;

// Abstract base class for UI elements
class UIElement {
public:
    Rect  Bounds;
    bool  Visible = true;
    UIManager* Manager = nullptr;

    explicit UIElement(Rect bounds) : Bounds(bounds) {}
    virtual ~UIElement() = default;

    // Called when the element should draw itself into the canvas
    virtual void Draw(Canvas& canvas, Rect clip) = 0;

    // Request the manager to invalidate this element
    void Invalidate(Rect* area = nullptr);
};

} // namespace Adamantite::GFX::UI
