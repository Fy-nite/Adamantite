#pragma once

#include "UIElement.hpp"
#include <vector>
#include <algorithm>
#include <stdexcept>

namespace Adamantite::GFX::UI {

class UIManager {
public:
    void Add(UIElement* e) {
        if (!e) return;
        for (auto* el : _elements) if (el == e) return;
        e->Manager = this;
        _elements.push_back(e);
        NotifyInvalid(e->Bounds);
    }

    void Remove(UIElement* e) {
        if (!e) return;
        _elements.erase(std::remove(_elements.begin(), _elements.end(), e), _elements.end());
        e->Manager = nullptr;
    }

    void NotifyInvalid(const Rect& r) {
        if (r.Width <= 0 || r.Height <= 0) return;
        _dirty.push_back(r);
    }

    // Draw only elements intersecting dirty regions; returns the dirty regions used
    std::vector<Rect> RenderDirty(Canvas& canvas) {
        auto coalesced = CoalesceDirty();
        if (coalesced.empty()) return coalesced;

        for (const auto& r : coalesced) {
            Rect clip = Rect::Intersect(Rect{0, 0, canvas.width, canvas.height}, r);
            if (clip.Width <= 0 || clip.Height <= 0) continue;
            for (auto* e : _elements) {
                if (!e->Visible) continue;
                if (!e->Bounds.Intersects(clip)) continue;
                e->Draw(canvas, clip);
            }
        }

        _dirty.clear();
        return coalesced;
    }

    // Basic input handling: detect left-button click releases on buttons
    // mx, my: mouse position in canvas-space pixels
    void ProcessInputAt(int mx, int my, bool mouseReleased);

private:
    std::vector<UIElement*> _elements;
    std::vector<Rect>       _dirty;

    std::vector<Rect> CoalesceDirty() {
        if (_dirty.empty()) return {};
        std::vector<Rect> list(_dirty);
        bool mergedAny;
        do {
            mergedAny = false;
            for (size_t i = 0; i < list.size(); i++) {
                for (size_t j = i + 1; j < list.size(); j++) {
                    Rect a = list[i], b = list[j];
                    Rect expanded = Rect::Union(a, b);
                    if (expanded.Width  <= a.Width  + b.Width  + 4 &&
                        expanded.Height <= a.Height + b.Height + 4) {
                        list[i] = expanded;
                        list.erase(list.begin() + j);
                        mergedAny = true;
                        break;
                    }
                }
                if (mergedAny) break;
            }
        } while (mergedAny);
        return list;
    }
};

// Implement UIElement::Invalidate here since it needs UIManager
inline void UIElement::Invalidate(Rect* area) {
    if (!Manager) return;
    Rect r = area ? *area : Bounds;
    r = Rect::Intersect(r, Bounds);
    if (r.Width <= 0 || r.Height <= 0) return;
    Manager->NotifyInvalid(r);
}

} // namespace Adamantite::GFX::UI
