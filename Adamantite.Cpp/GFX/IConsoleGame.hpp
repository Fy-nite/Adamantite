#pragma once

#include "Canvas.hpp"

namespace Adamantite::GFX {

class IConsoleGame {
public:
    virtual ~IConsoleGame() = default;
    virtual void Init(Canvas& surface) = 0;
    virtual void Update(double deltaTime) = 0;
    virtual void Draw(Canvas& surface) = 0;
};

} // namespace Adamantite::GFX
