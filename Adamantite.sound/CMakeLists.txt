# CMakeList.txt : CMake project for Adamantite, include source and define
# project specific logic here.
#

# Add source to this project's executable.
add_library (Adamantite.sound "Adamantite.cpp" "Adamantite.h")

if (CMAKE_VERSION VERSION_GREATER 3.12)
  set_property(TARGET Adamantite.sound PROPERTY CXX_STANDARD 20)
endif()

target_link_libraries(Adamantite.sound PRIVATE Adamantite.core)

# TODO: Add tests and install targets if needed.
