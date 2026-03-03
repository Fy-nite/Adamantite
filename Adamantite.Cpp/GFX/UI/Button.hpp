#pragma once

#include "UIElement.hpp"
#include "UIManager.hpp"
#include "../CanvasTextHelper.hpp"
#include "../Colors.hpp"
#include <string>
#include <functional>

namespace Adamantite::GFX::UI {

class Button : public UIElement {
public:
    std::function<void()> Clicked;
    std::string Text;
    Color NormalColor;
    Color HoverColor;
    Color PressColor;
    bool IsPressed = false;

    Button(Rect bounds, const std::string& text)
        : UIElement(bounds), Text(text),
          NormalColor(Colors::Gray), HoverColor(Colors::DarkGray), PressColor(Colors::White)
    {}

    void Draw(Canvas& canvas, Rect /*clip*/) override {
        canvas.DrawFilledRect(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, NormalColor);
        CanvasTextHelper::Prin(canvas, Bounds.X + 4, Bounds.Y + 2, Text, Colors::White);
    }

    void TriggerClick() {
        try { if (Clicked) Clicked(); } catch (...) {}
    }
};

} // namespace Adamantite::GFX::UI
