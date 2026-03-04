#pragma once

#ifdef _WIN32
#  define ADAMANTITE_API __declspec(dllimport)
#else
#  define ADAMANTITE_API
#endif

#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef void* RendererHandle;

ADAMANTITE_API RendererHandle renderer_create();
ADAMANTITE_API void           renderer_destroy(RendererHandle handle);
ADAMANTITE_API bool           renderer_init(RendererHandle handle, int width, int height, const char* title);
ADAMANTITE_API void           renderer_begin_frame(RendererHandle handle);
ADAMANTITE_API void           renderer_end_frame(RendererHandle handle);
ADAMANTITE_API void           renderer_clear(RendererHandle handle, float r, float g, float b, float a);
ADAMANTITE_API void           renderer_shutdown(RendererHandle handle);
ADAMANTITE_API void           renderer_present_pixels(RendererHandle handle, const void* pixels, int width, int height);

#ifdef __cplusplus
}
#endif
