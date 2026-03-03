# CMakeLists.txt for Adamantite.Cpp
# C++ conversion of Adamantite.CSharp

cmake_minimum_required(VERSION 3.10)

# Collect all source files
set(ADAMANTITE_CPP_SOURCES
    GFX/Canvas.cpp
)

# Headers (listed for IDE support; not required by the build)
set(ADAMANTITE_CPP_HEADERS
    NativeConnectors.hpp
    GFX/Canvas.hpp
    GFX/CanvasExtensions.hpp
    GFX/CanvasTextHelper.hpp
    GFX/Colors.hpp
    GFX/FpsCounter.hpp
    GFX/IConsoleGame.hpp
    GFX/GPU/BitmapFont.hpp
    GFX/GPU/ColorUtil.hpp
    GFX/GPU/CommandBuffer.hpp
    GFX/GPU/IRenderBackend.hpp
    GFX/GPU/Renderer.hpp
    GFX/GPU/SDLAdapterRenderer.hpp
    GFX/GPU/SimpleGPURunner.hpp
    GFX/GPU/Surface.hpp
    GFX/GPU/Texture.hpp
    GFX/GPU/VirtualTerminal.hpp
    GFX/GPU/Interop/SDLRendererNative.hpp
    GFX/UI/UIElement.hpp
    GFX/UI/UIManager.hpp
    GFX/UI/Button.hpp
    GFX/UI/Label.hpp
    GFX/UI/AdsrGraph.hpp
    Helpers/SoundBus.hpp
    Helpers/SoundSystem.hpp
    SFX/SimpleSynth.hpp
    SFX/Button.hpp
    Util/DebugUtil.hpp
    Util/DiagnosticsProvider.hpp
    VFS/VfsFileInfo.hpp
    VFS/IFileSystem.hpp
    VFS/InMemoryFileSystem.hpp
    VFS/PhysicalFileSystem.hpp
    VFS/VfsManager.hpp
    VFS/VFSGlobal.hpp
    VFS/VfsFontLoader.hpp
)

add_library(Adamantite.Cpp STATIC ${ADAMANTITE_CPP_SOURCES} ${ADAMANTITE_CPP_HEADERS})

target_include_directories(Adamantite.Cpp PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})

if (CMAKE_VERSION VERSION_GREATER 3.12)
    set_property(TARGET Adamantite.Cpp PROPERTY CXX_STANDARD 20)
endif()

# Link against Adamantite.core for the SDL renderer C API used by SDLAdapterRenderer
target_link_libraries(Adamantite.Cpp PUBLIC Adamantite.core)
