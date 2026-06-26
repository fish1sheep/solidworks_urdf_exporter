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

using System.IO;

namespace SW2URDF.ROS
{
    /// <summary>
    /// Gazebo simulation launch file generator.
    /// Note: WriteFile() is currently a placeholder. SDF and launch file generation
    /// will be added when the ROS2 Gazebo (Ignition/Gazebo Sim) ecosystem matures.
    /// </summary>
    public class Gazebo
    {
        private readonly string package;
        private readonly string robotURDF;
        private readonly string model;

        public Gazebo(string modelName, string packageName, string URDFName)
        {
            model = modelName;
            package = packageName;
            robotURDF = URDFName;
        }

        /// <summary>
        /// Generates the Gazebo simulation launch file.
        /// Currently a placeholder, reserved for future ROS2 Gazebo (Ignition) support.
        /// </summary>
        /// <param name="dir">Launch file output directory</param>
        public void WriteFile(string dir)
        {
            // TODO: Implement ROS2 Gazebo (Ignition) launch file and SDF file generation
        }
    }

    /// <summary>
    /// RViz visualization launch file generator.
    /// Generates a ROS2 Python launch file (display.launch.py) for visualizing
    /// the exported robot URDF model in RViz2.
    /// </summary>
    public class Rviz
    {
        private readonly string package;
        private readonly string robotURDF;

        public Rviz(string packageName, string URDFName)
        {
            package = packageName;
            robotURDF = URDFName;
        }

        /// <summary>
        /// Generates the ROS2 Python launch file (display.launch.py).
        /// This launch file is used to visualize the robot model in RViz2.
        /// </summary>
        /// <param name="dir">Launch file output directory</param>
        public void WriteFiles(string dir)
        {
            // Build the ROS2 Python launch script content
            string content = BuildLaunchFileContent();

            // Write to display.launch.py in the launch directory
            string path = Path.Combine(dir, "display.launch.py");
            File.WriteAllText(path, content);
        }

        /// <summary>
        /// Builds the Python script content for display.launch.py.
        ///
        /// Configurable launch arguments:
        ///   - urdf_path:    Absolute path to the URDF model file
        ///                   (defaults to urdf/&lt;package_name&gt;.urdf in the package)
        ///   - use_sim_time: Whether to use simulation time
        ///                   (set to true when using Gazebo, default false)
        ///   - use_gui:      Whether to enable the joint_state_publisher GUI panel
        ///                   (default false)
        ///   - rviz_config:  Path to RViz2 configuration file
        ///                   (optional, uses rviz2 defaults if not provided)
        ///
        /// Note: URDF files are loaded via the xacro command.
        /// Even plain URDF files without xacro macros are passed through unchanged.
        /// xacro is a standard dependency of robot_state_publisher.
        /// </summary>
        /// <returns>The complete Python launch script as a string</returns>
        private string BuildLaunchFileContent()
        {
            // C# raw interpolated string for readability and maintainability.
            // Note: {{ and }} in the Python code produce literal { and } in the output.
            return $@"# Auto-generated ROS2 launch file — created by SolidWorks URDF Exporter
# Used for visualizing the exported robot model in RViz2

import os
from ament_index_python.packages import get_package_share_directory
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.conditions import IfCondition, UnlessCondition
from launch.substitutions import Command, LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description():
    # ============================================================
    # Locate the package share directory and build default paths
    # ============================================================
    pkg_share = get_package_share_directory('{package}')

    default_urdf_path = os.path.join(pkg_share, 'urdf', '{robotURDF}')
    default_rviz_config = os.path.join(pkg_share, 'rviz', 'urdf.rviz')

    # ============================================================
    # Declare launch arguments
    # ============================================================
    urdf_path_arg = DeclareLaunchArgument(
        name='urdf_path',
        default_value=default_urdf_path,
        description='Absolute path to the URDF model file'
    )

    use_sim_time_arg = DeclareLaunchArgument(
        name='use_sim_time',
        default_value='false',
        description='Use simulation (Gazebo) clock if true'
    )

    use_gui_arg = DeclareLaunchArgument(
        name='use_gui',
        default_value='false',
        description='Enable the joint_state_publisher GUI panel'
    )

    rviz_config_arg = DeclareLaunchArgument(
        name='rviz_config',
        default_value=default_rviz_config,
        description='Path to the RViz2 configuration file'
    )

    # ============================================================
    # robot_state_publisher node: publishes the robot TF tree
    # and joint states.
    #
    # Uses Command + xacro to load the URDF file:
    #   - Works with plain URDF files (xacro passes them through)
    #   - Also supports .xacro macro files
    #   - The urdf_path argument lets users override the default
    #   - Fixes a bug where urdf_path was declared but never used
    # ============================================================
    robot_state_publisher = Node(
        package='robot_state_publisher',
        executable='robot_state_publisher',
        name='robot_state_publisher',
        output='screen',
        parameters=[{{
            'robot_description': Command([
                'xacro ', LaunchConfiguration('urdf_path')
            ]),
            'use_sim_time': LaunchConfiguration('use_sim_time'),
        }}],
    )

    # ============================================================
    # joint_state_publisher node: publishes default joint angles
    # for non-fixed joints.
    # Switches between GUI and headless mode based on use_gui.
    # ============================================================
    joint_state_publisher_gui = Node(
        package='joint_state_publisher_gui',
        executable='joint_state_publisher_gui',
        name='joint_state_publisher_gui',
        output='screen',
        condition=IfCondition(LaunchConfiguration('use_gui')),
        parameters=[{{
            'use_sim_time': LaunchConfiguration('use_sim_time'),
        }}],
    )

    joint_state_publisher = Node(
        package='joint_state_publisher',
        executable='joint_state_publisher',
        name='joint_state_publisher',
        output='screen',
        condition=UnlessCondition(LaunchConfiguration('use_gui')),
        parameters=[{{
            'use_sim_time': LaunchConfiguration('use_sim_time'),
        }}],
    )

    # ============================================================
    # rviz2 node: 3D visualization
    # ============================================================
    rviz_node = Node(
        package='rviz2',
        executable='rviz2',
        name='rviz2',
        output='screen',
        arguments=['-d', LaunchConfiguration('rviz_config')],
        parameters=[{{
            'use_sim_time': LaunchConfiguration('use_sim_time'),
        }}],
    )

    # ============================================================
    # Return LaunchDescription with all nodes in order
    # ============================================================
    return LaunchDescription([
        urdf_path_arg,
        use_sim_time_arg,
        use_gui_arg,
        rviz_config_arg,
        robot_state_publisher,
        joint_state_publisher,
        joint_state_publisher_gui,
        rviz_node,
    ])
";
        }
    }
}
