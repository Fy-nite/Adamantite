# CMakeLists.txt for Adamantite.Cpp
# C++ conversion of Adamantite.CSharp

cmake_minimum_required(VERSION 3.10)
file(GLOB_RECURSE SOURCES "*.cpp" "*.hpp")
add_library(Adamantite.Cpp STATIC ${SOURCES})

target_include_directories(Adamantite.Cpp PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})

if (CMAKE_VERSION VERSION_GREATER 3.12)
    set_property(TARGET Adamantite.Cpp PROPERTY CXX_STANDARD 20)
endif()

# Link against Adamantite.core for the SDL renderer C API used by SDLAdapterRenderer
target_link_libraries(Adamantite.Cpp PUBLIC Adamantite.core)
