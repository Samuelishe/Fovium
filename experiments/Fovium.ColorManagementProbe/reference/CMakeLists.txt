cmake_minimum_required(VERSION 3.20)
project(FoviumLcmsReference C)

if(NOT DEFINED LCMS_SOURCE_DIR OR NOT DEFINED LCMS_LIBRARY)
  message(FATAL_ERROR "Pass LCMS_SOURCE_DIR and LCMS_LIBRARY from an ignored Little CMS build.")
endif()

add_executable(lcms_compare lcms_compare.c)
target_include_directories(lcms_compare PRIVATE "${LCMS_SOURCE_DIR}/include")
target_link_libraries(lcms_compare PRIVATE "${LCMS_LIBRARY}")
