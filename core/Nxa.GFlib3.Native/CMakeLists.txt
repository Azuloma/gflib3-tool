cmake_minimum_required(VERSION 3.20)

project(Nxa.GFlib3.Native LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(OozDir "${CMAKE_CURRENT_SOURCE_DIR}/../../third_party/ooz")

add_library(Nxa.GFlib3.Native SHARED
    oodle_shim.cpp
    "${OozDir}/kraken.cpp"
    "${OozDir}/lzna.cpp"
    "${OozDir}/bitknit.cpp"
    "${OozDir}/stdafx.cpp"
)

target_include_directories(Nxa.GFlib3.Native PRIVATE "${OozDir}")
target_compile_definitions(Nxa.GFlib3.Native PRIVATE NXA_OOZ_NO_CLI)

if(MSVC)
    target_compile_options(Nxa.GFlib3.Native PRIVATE /W3 /permissive-)
    target_compile_definitions(Nxa.GFlib3.Native PRIVATE _CRT_SECURE_NO_WARNINGS)
    # ooz predates modern MSVC warnings and is vendored unchanged.
    target_compile_options(Nxa.GFlib3.Native PRIVATE /wd4244 /wd4267 /wd4996)
endif()

set_target_properties(Nxa.GFlib3.Native PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/bin"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_CURRENT_BINARY_DIR}/bin/Debug"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_CURRENT_BINARY_DIR}/bin/Release"
)
