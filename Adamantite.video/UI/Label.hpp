#pragma once

#include "UIElement.hpp"
#include "../CanvasTextHelper.hpp"
#include <string>

namespace Adamantite::GFX::UI {

class Label : public UIElement {
public:
    std::string Text;
    Color LabelColor;

    Label(Rect bounds, const std::string& text, Color color)
        : UIElement(bounds), Text(text), LabelColor(color)
    {}

    void Draw(Canvas& canvas, Rect /*clip*/) override {
        CanvasTextHelper::Prin(canvas, Bounds.X, Bounds.Y, Text, LabelColor);
    }
};

} // namespace Adamantite::GFX::UI
