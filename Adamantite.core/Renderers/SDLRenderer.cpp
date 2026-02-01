#include "RendererStubs.hpp"
#include "RendererCAPI.h"
#include <SDL.h>
#include <iostream>

class SDLRenderer : public IRenderer {
public:
	SDLRenderer() : window(nullptr), renderer(nullptr) {}
	~SDLRenderer() override { Shutdown(); }
    

	bool Init(int width, int height, const char* title) override {
		if (SDL_Init(SDL_INIT_VIDEO) != 0) {
			std::cerr << "SDL_Init Error: " << SDL_GetError() << std::endl;
			return false;
		}
		window = SDL_CreateWindow(title, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED, width, height, 0);
		if (!window) {
			std::cerr << "SDL_CreateWindow Error: " << SDL_GetError() << std::endl;
			return false;
		}
		renderer = SDL_CreateRenderer(window, -1, SDL_RENDERER_ACCELERATED | SDL_RENDERER_PRESENTVSYNC);
		if (!renderer) {
			std::cerr << "SDL_CreateRenderer Error: " << SDL_GetError() << std::endl;
			return false;
		}
		return true;
	}

	void BeginFrame() override {
		// No-op for SDL
	}

	void EndFrame() override {
		SDL_RenderPresent(renderer);
	}

	void Clear(float r, float g, float b, float a) override {
		SDL_SetRenderDrawColor(renderer, (Uint8)(r * 255), (Uint8)(g * 255), (Uint8)(b * 255), (Uint8)(a * 255));
		SDL_RenderClear(renderer);
	}

	void Shutdown() override {
		if (renderer) {
			SDL_DestroyRenderer(renderer);
			renderer = nullptr;
		}
		if (window) {
			SDL_DestroyWindow(window);
			window = nullptr;
		}
		if (texture) {
			SDL_DestroyTexture(texture);
			texture = nullptr;
		}
		SDL_Quit();
	}

private:
	SDL_Window* window;
	SDL_Renderer* renderer;
	SDL_Texture* texture = nullptr;
	int textureWidth = 0;
	int textureHeight = 0;
};

IRenderer* CreateSDLRenderer() {
	return new SDLRenderer();
}

// C API exports
#ifdef __cplusplus
extern "C" {
#endif

#ifdef _WIN32
#define API_EXPORT __declspec(dllexport)
#else
#define API_EXPORT
#endif

API_EXPORT RendererHandle renderer_create() {
	return (RendererHandle)CreateSDLRenderer();
}

API_EXPORT void renderer_destroy(RendererHandle h) {
	delete (IRenderer*)h;
}

API_EXPORT bool renderer_init(RendererHandle h, int width, int height, const char* title) {
	if (!h) return false;
	return ((IRenderer*)h)->Init(width, height, title);
}

API_EXPORT void renderer_begin_frame(RendererHandle h) {
	if (!h) return;
	((IRenderer*)h)->BeginFrame();
}

API_EXPORT void renderer_end_frame(RendererHandle h) {
	if (!h) return;
	((IRenderer*)h)->EndFrame();
}

API_EXPORT void renderer_clear(RendererHandle h, float r, float g, float b, float a) {
	if (!h) return;
	((IRenderer*)h)->Clear(r, g, b, a);
}

API_EXPORT void renderer_shutdown(RendererHandle h) {
	if (!h) return;
	((IRenderer*)h)->Shutdown();
}

API_EXPORT void renderer_present_pixels(RendererHandle h, const void* pixels, int width, int height) {
	if (!h) return;
	SDLRenderer* s = (SDLRenderer*)h;
	if (!s) return;

	// Create or recreate texture if needed
	if (!s->texture || s->textureWidth != width || s->textureHeight != height) {
		if (s->texture) SDL_DestroyTexture(s->texture);
		s->texture = SDL_CreateTexture(s->renderer, SDL_PIXELFORMAT_ABGR8888, SDL_TEXTUREACCESS_STREAMING, width, height);
		s->textureWidth = width;
		s->textureHeight = height;
	}

	// Update texture with pixel data (expected 0xAARRGGBB per pixel)
	SDL_UpdateTexture(s->texture, NULL, pixels, width * sizeof(uint32_t));

	// Render texture to full window
	SDL_RenderCopy(s->renderer, s->texture, NULL, NULL);
}

#ifdef __cplusplus
}
#endif