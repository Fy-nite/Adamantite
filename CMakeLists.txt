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