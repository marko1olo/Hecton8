cmake_minimum_required(VERSION 3.22.1)

project(HectonAndroidBridge LANGUAGES C CXX)

add_library(HectonAndroidBridge SHARED
    HectonAndroidAssetBridge.cpp
)

target_compile_features(HectonAndroidBridge PRIVATE cxx_std_17)

target_compile_options(HectonAndroidBridge PRIVATE
    -Wall
    -Wextra
    -Werror
    -fvisibility=hidden
    -ffunction-sections
    -fdata-sections
)

target_link_options(HectonAndroidBridge PRIVATE
    -Wl,--gc-sections
)

target_link_libraries(HectonAndroidBridge
    android
    log
)
