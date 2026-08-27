cmake_minimum_required(VERSION 3.16)
project(fovium_lcms2_test_profiles C)

find_package(lcms2 CONFIG REQUIRED)
add_executable(fovium-lcms2-generate-profiles generate_profiles.c)
target_link_libraries(fovium-lcms2-generate-profiles PRIVATE lcms2::lcms2)
