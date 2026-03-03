#pragma once

#include "UIElement.hpp"
#include "UIManager.hpp"
#include "../CanvasExtensions.hpp"
#include "../Colors.hpp"
#include <functional>
#include <algorithm>
#include <cmath>
#include <string>
#include <sstream>
#include <iomanip>

namespace Adamantite::GFX::UI {

// Simple ADSR graph UI element.
// Values provided by callbacks so the graph reflects live changes.
class AdsrGraph : public UIElement {
public:
    std::function<float()> GetAttack;
    std::function<float()> GetDecay;
    std::function<float()> GetSustain;
    std::function<float()> GetRelease;

    AdsrGraph(Rect bounds,
              std::function<float()> getAttack,
              std::function<float()> getDecay,
              std::function<float()> getSustain,
              std::function<float()> getRelease)
        : UIElement(bounds),
          GetAttack(std::move(getAttack)), GetDecay(std::move(getDecay)),
          GetSustain(std::move(getSustain)), GetRelease(std::move(getRelease))
    {}

    void Draw(Canvas& canvas, Rect /*clip*/) override {
        // background
        canvas.DrawFilledRect(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, Color(16, 18, 24, 255));
        DrawOutlinedRect(canvas, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, Colors::DarkGray);

        float attack  = GetAttack ? GetAttack() : 0.0f;
        float decay   = GetDecay  ? GetDecay()  : 0.0f;
        float sustain = GetSustain ? std::max(0.0f, std::min(1.0f, GetSustain())) : 0.0f;
        float release = GetRelease ? GetRelease() : 0.0f;

        float tTotal = std::max(0.0001f, attack + decay + release);
        int left   = Bounds.X + 4;
        int right  = Bounds.X + Bounds.Width - 4;
        int top    = Bounds.Y + 6;
        int bottom = Bounds.Y + Bounds.Height - 6;
        float w = static_cast<float>(std::max(8, right - left));

        float attackFrac  = attack  / tTotal;
        float decayFrac   = decay   / tTotal;
        float releaseFrac = release / tTotal;

        float x0 = static_cast<float>(left);
        float x1 = x0 + attackFrac  * w;
        float x2 = x1 + decayFrac   * w;
        float x3 = x2 + 0.25f       * w; // sustain plateau
        float x4 = x3 + releaseFrac * w;
        if (x4 > right) x4 = static_cast<float>(right);

        float yBottom  = static_cast<float>(bottom);
        float yPeak    = static_cast<float>(top);
        float ySustain = yBottom - sustain * (yBottom - yPeak);

        int p0x = static_cast<int>(x0), p0y = static_cast<int>(yBottom);
        int p1x = static_cast<int>(x1), p1y = static_cast<int>(yPeak);
        int p2x = static_cast<int>(x2), p2y = static_cast<int>(ySustain);
        int p3x = static_cast<int>(x3), p3y = static_cast<int>(ySustain);
        int p4x = static_cast<int>(x4), p4y = static_cast<int>(yBottom);

        DrawLine(canvas, p0x, p0y, p1x, p1y, Colors::Yellow);
        DrawLine(canvas, p1x, p1y, p2x, p2y, Colors::Yellow);
        DrawLine(canvas, p2x, p2y, p3x, p3y, Colors::Yellow);
        DrawLine(canvas, p3x, p3y, p4x, p4y, Colors::Yellow);

        canvas.DrawFilledRect(p1x - 2, p1y - 2, 4, 4, Colors::Orange);
        canvas.DrawFilledRect(p2x - 2, p2y - 2, 4, 4, Colors::Orange);
        canvas.DrawFilledRect(p3x - 2, p3y - 2, 4, 4, Colors::Orange);

        // Numeric labels
        auto fmt3 = [](float v) -> std::string {
            std::ostringstream ss; ss << std::fixed << std::setprecision(3) << v; return ss.str();
        };
        auto fmt2 = [](float v) -> std::string {
            std::ostringstream ss; ss << std::fixed << std::setprecision(2) << v; return ss.str();
        };
        DrawText(canvas, Bounds.X + 6,               Bounds.Y + 2, "A:" + fmt3(attack),  Colors::Gray);
        DrawText(canvas, Bounds.X + 6 + 56,           Bounds.Y + 2, "D:" + fmt3(decay),   Colors::Gray);
        DrawText(canvas, Bounds.X + Bounds.Width - 56, Bounds.Y + 2, "S:" + fmt2(sustain), Colors::Gray);
    }
};

} // namespace Adamantite::GFX::UI
