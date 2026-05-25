cmake_minimum_required(VERSION 3.21)
project(DeepSeekQt VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

find_package(Qt6 6.6 REQUIRED COMPONENTS Widgets WebEngineWidgets WebChannel)

set(DS_QT_SOURCES
    src/main.cpp
    src/AssetUrlScheme.cpp
    src/BridgeClient.cpp
    src/DeepSeekHost.cpp
    src/WebViewPage.cpp
    src/MainWindow.cpp
)

add_executable(DeepSeek.Qt WIN32 ${DS_QT_SOURCES})

target_include_directories(DeepSeek.Qt PRIVATE src)

target_link_libraries(DeepSeek.Qt PRIVATE
    Qt6::Widgets
    Qt6::WebEngineWidgets
    Qt6::WebChannel
)

if(WIN32)
    set_target_properties(DeepSeek.Qt PROPERTIES OUTPUT_NAME "DeepSeek.Qt")
endif()
