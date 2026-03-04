#pragma once

namespace Adamantite::GFX {

/// Lightweight FPS counter helper.
/// Call Tick() with the elapsed seconds for the current frame.
/// The Fps() value is updated about once per second.
class FpsCounter {
public:
    FpsCounter() : _accumSeconds(0.0), _frames(0), _fps(0.0) {}

    double Fps() const { return _fps; }

    void Tick(double elapsedSeconds) {
        if (elapsedSeconds < 0) elapsedSeconds = 0;
        _accumSeconds += elapsedSeconds;
        _frames++;
        if (_accumSeconds >= 1.0) {
            _fps = _frames / _accumSeconds;
            _frames = 0;
            _accumSeconds = 0.0;
        }
    }

    void Reset() {
        _accumSeconds = 0.0;
        _frames = 0;
        _fps = 0.0;
    }

private:
    double _accumSeconds;
    int    _frames;
    double _fps;
};

} // namespace Adamantite::GFX
