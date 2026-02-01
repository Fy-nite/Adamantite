# CMakeList.txt : Top-level CMake project file, do global configuration
# and include sub-projects here.
#
cmake_minimum_required (VERSION 3.10)

# Enable Hot Reload for MSVC compilers if supported.
if (POLICY CMP0141)
  cmake_policy(SET CMP0141 NEW)
  set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
endif()

project ("Adamantite")

if (POLICY CMP0079)
  cmake_policy(SET CMP0079 NEW)
endif()

# Include sub-projects. Build libraries first so the runtime can link against them
add_subdirectory ("Adamantite.core")
add_subdirectory ("Adamantite.sound")
add_subdirectory ("Adamantite.html")
add_subdirectory ("Adamantite.video")
add_subdirectory ("Adamantite.runtime")
target_link_libraries(Adamantite.runtime PRIVATE Adamantite.core Adamantite.sound Adamantite.html Adamantite.video)

# Create an `out` folder at repo root and copy any built shared libraries into it after build
file(MAKE_DIRECTORY "${CMAKE_SOURCE_DIR}/out")

# Generate a small CMake script that will copy shared libraries from the build tree into ${CMAKE_SOURCE_DIR}/out
file(WRITE "${CMAKE_BINARY_DIR}/cmake_copy_shared_libs.cmake" "file(MAKE_DIRECTORY \"${CMAKE_SOURCE_DIR}/out\")\n")
file(GLOB_RECURSE _shared_libs "${CMAKE_BINARY_DIR}/*.dll" "${CMAKE_BINARY_DIR}/*.dll.a" "${CMAKE_BINARY_DIR}/*.lib" "${CMAKE_BINARY_DIR}/*.a" "${CMAKE_BINARY_DIR}/*.so" "${CMAKE_BINARY_DIR}/*.so.*" "${CMAKE_BINARY_DIR}/*.dylib" "${CMAKE_BINARY_DIR}/*.exe")
foreach(_lib IN LISTS _shared_libs)
  file(APPEND "${CMAKE_BINARY_DIR}/cmake_copy_shared_libs.cmake" "file(COPY \"${_lib}\" DESTINATION \"${CMAKE_SOURCE_DIR}/out\")\n")
endforeach()

# Add a custom target that runs the copy script after building the main library targets
add_custom_target(copy_shared_libs ALL
  COMMAND ${CMAKE_COMMAND} -P "${CMAKE_BINARY_DIR}/cmake_copy_shared_libs.cmake"
  COMMENT "Collecting shared libraries into ${CMAKE_SOURCE_DIR}/out"
)

# Ensure the copy step runs after these library targets are built
add_dependencies(copy_shared_libs Adamantite.core Adamantite.sound Adamantite.html Adamantite.video Adamantite.runtime)