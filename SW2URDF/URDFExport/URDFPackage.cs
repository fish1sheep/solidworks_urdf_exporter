/*
Copyright (c) 2015 Stephen Brawner

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using SW2URDF.UI;
using System;
using System.IO;

namespace SW2URDF.URDFExport
{
    /// <summary>
    /// ROS2 URDF description package generator.
    /// Manages package directory structure, paths, and ROS2-specific build files
    /// (CMakeLists.txt, package.xml, joint_names YAML).
    ///
    /// Generated package structure:
    ///   &lt;PackageName&gt;/
    ///   ├── CMakeLists.txt              # ament_cmake build configuration
    ///   ├── package.xml                 # ROS2 package format 3 manifest
    ///   ├── config/
    ///   │   └── joint_names_&lt;name&gt;.yaml  # Joint name list
    ///   ├── launch/
    ///   │   └── display.launch.py       # RViz visualization launch file
    ///   ├── meshes/
    ///   │   └── *.STL / *.3dxml         # Mesh files
    ///   └── urdf/
    ///       └── &lt;name&gt;.urdf             # URDF robot description file
    /// </summary>
    public class URDFPackage
    {
        public static IMessageBox MessageBox = new MessageBoxHelper();

        /// <summary>
        /// ROS2 package name (e.g. "my_robot_description").
        /// </summary>
        public string PackageName { get; }

        // ---- package:// URI paths (used for intra-URDF references) ----

        /// <summary>
        /// Base package:// URI for the package (e.g. package://my_robot_description/).
        /// </summary>
        public string PackageDirectory { get; }

        /// <summary>
        /// package:// URI directory for mesh files (e.g. package://my_robot_description/meshes/).
        /// </summary>
        public string MeshesDirectory { get; }

        /// <summary>
        /// package:// URI directory for texture files.
        /// </summary>
        public string TexturesDirectory { get; }

        /// <summary>
        /// package:// URI directory for URDF files.
        /// </summary>
        public string RobotsDirectory { get; }

        /// <summary>
        /// package:// URI directory for launch files.
        /// </summary>
        public string LaunchDirectory { get; }

        /// <summary>
        /// package:// URI directory for configuration files.
        /// </summary>
        public string ConfigDirectory { get; }

        // ---- Windows filesystem absolute paths (used for actual file I/O) ----

        public string WindowsPackageDirectory { get; }
        public string WindowsMeshesDirectory { get; }
        public string WindowsTexturesDirectory { get; }
        public string WindowsRobotsDirectory { get; }
        public string WindowsLaunchDirectory { get; }
        public string WindowsConfigDirectory { get; }
        public string WindowsRvizDirectory { get; }
        public string WindowsRvizConfig { get; }
        public string WindowsCMakeLists { get; }
        public string WindowsConfigYAML { get; }

        /// <summary>
        /// Constructs a URDFPackage instance and initializes all path properties.
        /// </summary>
        /// <param name="name">ROS2 package name</param>
        /// <param name="dir">Export root directory (Windows absolute path)</param>
        public URDFPackage(string name, string dir)
        {
            PackageName = name;

            // Build package:// URI paths (used for mesh references inside URDF files)
            PackageDirectory = @"package://" + name + @"/";
            MeshesDirectory = PackageDirectory + @"meshes/";
            RobotsDirectory = PackageDirectory + @"urdf/";
            TexturesDirectory = PackageDirectory + @"textures/";
            LaunchDirectory = PackageDirectory + @"launch/";
            ConfigDirectory = PackageDirectory + @"config/";

            // Build Windows filesystem paths (used for actual file writing)
            char last = dir[dir.Length - 1];
            dir = (last == '\\') ? dir : dir + @"\";
            WindowsPackageDirectory = dir + name + @"\";
            WindowsMeshesDirectory = WindowsPackageDirectory + @"meshes\";
            WindowsRobotsDirectory = WindowsPackageDirectory + @"urdf\";
            WindowsTexturesDirectory = WindowsPackageDirectory + @"textures\";
            WindowsLaunchDirectory = WindowsPackageDirectory + @"launch\";
            WindowsConfigDirectory = WindowsPackageDirectory + @"config\";
            WindowsRvizDirectory = WindowsPackageDirectory + @"rviz\";
            WindowsRvizConfig = WindowsRvizDirectory + @"urdf.rviz";
            WindowsCMakeLists = WindowsPackageDirectory + @"CMakeLists.txt";
            WindowsConfigYAML = WindowsConfigDirectory + @"joint_names_" + name + ".yaml";
        }

        /// <summary>
        /// Creates the full package directory structure
        /// (package, meshes, urdf, textures, launch, config).
        /// Existing directories are skipped.
        /// </summary>
        public void CreateDirectories()
        {
            MessageBox.Show("Creating URDF Package \"" +
                PackageName + "\" at:\n" + WindowsPackageDirectory);
            if (!Directory.Exists(WindowsPackageDirectory))
            {
                Directory.CreateDirectory(WindowsPackageDirectory);
            }
            if (!Directory.Exists(WindowsMeshesDirectory))
            {
                Directory.CreateDirectory(WindowsMeshesDirectory);
            }
            if (!Directory.Exists(WindowsRobotsDirectory))
            {
                Directory.CreateDirectory(WindowsRobotsDirectory);
            }
            if (!Directory.Exists(WindowsTexturesDirectory))
            {
                Directory.CreateDirectory(WindowsTexturesDirectory);
            }
            if (!Directory.Exists(WindowsLaunchDirectory))
            {
                Directory.CreateDirectory(WindowsLaunchDirectory);
            }
            if (!Directory.Exists(WindowsConfigDirectory))
            {
                Directory.CreateDirectory(WindowsConfigDirectory);
            }
            if (!Directory.Exists(WindowsRvizDirectory))
            {
                Directory.CreateDirectory(WindowsRvizDirectory);
            }
        }

        /// <summary>
        /// Generates a ROS2 ament_cmake-style CMakeLists.txt file.
        ///
        /// This is a pure description package (no compiled code). It only installs
        /// the config, launch, meshes, and urdf directories under share/${PROJECT_NAME}
        /// so that resource files can be located via the ament resource index.
        ///
        /// Generated CMake structure:
        ///   - cmake_minimum_required: minimum CMake version
        ///   - project: project name declaration
        ///   - find_package(ament_cmake REQUIRED): locate the ament_cmake build system
        ///   - install(DIRECTORY ...): install resource directories to share/
        ///   - if(BUILD_TESTING): enable lint checks in CI test builds
        ///   - ament_package(): declare as an ament package
        /// </summary>
        public void CreateCMakeLists()
        {
            using (StreamWriter file = new StreamWriter(WindowsCMakeLists))
            {
                // CMake file header comment
                file.WriteLine("# Auto-generated CMakeLists.txt — created by SolidWorks URDF Exporter");
                file.WriteLine();

                // Minimum CMake version (ROS2 Humble+ recommends 3.8 or higher)
                file.WriteLine("cmake_minimum_required(VERSION 3.8)");
                file.WriteLine();

                // Project declaration
                file.WriteLine("project(" + PackageName + ")");
                file.WriteLine();

                // Find ament_cmake build system (required for ROS2)
                file.WriteLine("# Find the ament_cmake build system (required for ROS2)");
                file.WriteLine("find_package(ament_cmake REQUIRED)");
                file.WriteLine();

                // Install resource directories into the package's share directory.
                // After installation, mesh files can be resolved via package://PackageName/meshes/...
                file.WriteLine("# Install all resource directories into the package share directory");
                file.WriteLine("# These files are located at runtime via the ament resource index");
                file.WriteLine("install(");
                file.WriteLine("  DIRECTORY config launch meshes urdf rviz");
                file.WriteLine("  DESTINATION share/${PROJECT_NAME}");
                file.WriteLine(")");
                file.WriteLine();

                // Test build support (lint checks in CI)
                file.WriteLine("# Enable lint checks in test builds (e.g. CI)");
                file.WriteLine("if(BUILD_TESTING)");
                file.WriteLine("  find_package(ament_lint_auto REQUIRED)");
                file.WriteLine("  # Automatically discover linter packages declared as test_depend");
                file.WriteLine("  ament_lint_auto_find_test_dependencies()");
                file.WriteLine("endif()");
                file.WriteLine();

                // Declare as an ament package
                file.WriteLine("# Declare as an ament package (required for ROS2)");
                file.WriteLine("ament_package()");
            }
        }

        /// <summary>
        /// Generates a joint name configuration YAML file.
        ///
        /// Output format (ROS2-neutral, compatible with ros2_control):
        ///   # Joint name list — auto-generated by SolidWorks URDF Exporter
        ///   joint_names:
        ///     - joint_1
        ///     - joint_2
        ///     - ...
        ///
        /// This file can be used for:
        ///   - Joint ordering reference in controller configuration
        ///   - The joint field in ros2_control controller YAML
        ///   - Passing joint parameters in launch scripts
        /// </summary>
        /// <param name="jointNames">Array of joint names in URDF traversal order</param>
        public void CreateConfigYAML(String[] jointNames)
        {
            using (StreamWriter file = new StreamWriter(WindowsConfigYAML))
            {
                // YAML file header comment
                file.WriteLine("# Joint name list — auto-generated by SolidWorks URDF Exporter");
                file.WriteLine("# Can be used for controller configuration or joint ordering reference");
                file.WriteLine();

                // Use the ROS2-neutral "joint_names" key (replaces ROS1 "controller_joint_names")
                file.WriteLine("joint_names:");

                // Output each joint name as a YAML list item
                foreach (String name in jointNames)
                {
                    file.WriteLine("  - " + name);
                }
            }
        }

        /// <summary>
        /// Generates a minimal default RViz2 configuration file (urdf.rviz).
        ///
        /// Provides sensible defaults for visualizing the robot model:
        ///   - Fixed frame: base_link
        ///   - Grid display enabled
        ///   - RobotModel display enabled with default URDF path
        ///
        /// Users can customize the configuration by editing this file
        /// or saving a new configuration from within RViz2.
        /// </summary>
        public void CreateDefaultRvizConfig()
        {
            using (StreamWriter file = new StreamWriter(WindowsRvizConfig))
            {
                file.WriteLine("Panels:");
                file.WriteLine("  - Class: rviz_common/Displays");
                file.WriteLine("    Name: Displays");
                file.WriteLine("Visualization Manager:");
                file.WriteLine("  Class: \"\"");
                file.WriteLine("  Displays:");
                file.WriteLine("    - Class: rviz_default_plugins/Grid");
                file.WriteLine("      Name: Grid");
                file.WriteLine("    - Class: rviz_default_plugins/RobotModel");
                file.WriteLine("      Name: RobotModel");
                file.WriteLine("      Description Source: Topic");
                file.WriteLine("      Description Topic:");
                file.WriteLine("        Value: /robot_description");
                file.WriteLine("      Enabled: true");
                file.WriteLine("      Visual Enabled: true");
                file.WriteLine("  Enabled: true");
                file.WriteLine("  Global Options:");
                file.WriteLine("    Fixed Frame: base_link");
                file.WriteLine("  Tools:");
                file.WriteLine("    - Class: rviz_default_plugins/MoveCamera");
                file.WriteLine("    - Class: rviz_default_plugins/Select");
                file.WriteLine("    - Class: rviz_default_plugins/Interact");
                file.WriteLine("  Value: true");
                file.WriteLine("  Views:");
                file.WriteLine("    Current:");
                file.WriteLine("      Class: rviz_default_plugins/Orbit");
                file.WriteLine("      Name: Current View");
                file.WriteLine("      Target Frame: base_link");
            }
        }

        /// <summary>
        /// Returns the package:// URI path for a mesh file within this package.
        /// Format: package://&lt;PackageName&gt;/meshes/&lt;linkName&gt;.&lt;extension&gt;
        ///
        /// In ROS2, robot_state_publisher resolves package:// prefixes via the
        /// ament resource index. Therefore mesh files must be installed to
        /// share/&lt;PackageName&gt;/meshes/ via CMakeLists.txt.
        ///
        /// Forward slash characters '/' in link names are replaced with underscores '_'
        /// to avoid filesystem path conflicts.
        /// </summary>
        /// <param name="linkName">Link name (e.g. "base_link" or "arm/link_1")</param>
        /// <param name="extension">File extension with leading dot (e.g. ".STL" or ".3dxml")</param>
        /// <returns>package:// format mesh URI string</returns>
        public string GetMeshPackageUri(string linkName, string extension)
        {
            // Replace forward slashes with underscores to avoid illegal filename characters
            string sanitized = linkName.Replace('/', '_');
            return MeshesDirectory + sanitized + extension;
        }

        /// <summary>
        /// Returns the Windows absolute filesystem path for a mesh file.
        /// </summary>
        /// <param name="linkName">Link name (slashes replaced with underscores)</param>
        /// <param name="extension">File extension with leading dot (e.g. ".STL" or ".3dxml")</param>
        /// <returns>Windows absolute path string</returns>
        public string GetWindowsMeshPath(string linkName, string extension)
        {
            string sanitized = linkName.Replace('/', '_');
            return WindowsMeshesDirectory + sanitized + extension;
        }
    }
}
