#pragma once

#include "Surface.hpp"
#include "CommandBuffer.hpp"

namespace Adamantite::GPU {

// Executes commands from a CommandBuffer onto a target Surface
class SimpleGPURunner {
public:
    explicit SimpleGPURunner(Surface& target) : _target(target) {}

    void Execute(const CommandBuffer& cb) {
        for (const auto& cmd : cb.Commands()) {
            if (const auto* dq = dynamic_cast<const DrawQuadCall*>(cmd.get())) {
                if (dq->texture && dq->texture->SurfacePtr()) {
                    _target.DrawTexturedQuad(*dq->texture->SurfacePtr(),
                        dq->x, dq->y, dq->width, dq->height, dq->tint);
                }
            }
        }
    }

private:
    Surface& _target;
};

} // namespace Adamantite::GPU
