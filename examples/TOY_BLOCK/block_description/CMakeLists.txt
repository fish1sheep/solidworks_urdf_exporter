# Auto-generated CMakeLists.txt — created by SolidWorks URDF Exporter

cmake_minimum_required(VERSION 3.8)

project(block_description)

# Find the ament_cmake build system (required for ROS2)
find_package(ament_cmake REQUIRED)

# Install all resource directories into the package share directory
# These files are located at runtime via the ament resource index
install(
  DIRECTORY config launch meshes urdf
  DESTINATION share/${PROJECT_NAME}
)

# Enable lint checks in test builds (e.g. CI)
if(BUILD_TESTING)
  find_package(ament_lint_auto REQUIRED)
  ament_lint_auto_find_test_dependencies()
endif()

# Declare as an ament package (required for ROS2)
ament_package()
