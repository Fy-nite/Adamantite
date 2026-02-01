#pragma once

#ifdef _WIN32
#define RENDERER_API __declspec(dllexport)
#else
#define RENDERER_API
#endif

#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef void* RendererHandle;

RENDERER_API RendererHandle renderer_create();
RENDERER_API void renderer_destroy(RendererHandle handle);
RENDERER_API bool renderer_init(RendererHandle handle, int width, int height, const char* title);
RENDERER_API void renderer_begin_frame(RendererHandle handle);
RENDERER_API void renderer_end_frame(RendererHandle handle);
RENDERER_API void renderer_clear(RendererHandle handle, float r, float g, float b, float a);
RENDERER_API void renderer_shutdown(RendererHandle handle);
// Present raw 32-bit ARGB pixels to the renderer. Pixels expected as 0xAARRGGBB.
RENDERER_API void renderer_present_pixels(RendererHandle handle, const void* pixels, int width, int height);

#ifdef __cplusplus
}
#endif
