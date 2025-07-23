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
using System;

namespace SW2URDF.ROS
{

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

        public void WriteFile(string dir)
        {
            
        }
    }

    public class Rviz
    {
        private readonly string package;
        private readonly string robotURDF;

        public Rviz(string packageName, string URDFName)
        {
            package = packageName;
            robotURDF = URDFName;

        }

        public void WriteFiles(string dir)
        {
            string path = dir + @"display.launch.py";
            string content = string.Format(
@"import os
from ament_index_python.packages import get_package_share_directory
from launch import LaunchDescription
from launch.conditions import IfCondition
from launch_ros.actions import Node
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration

def generate_launch_description():
    {0}_pkg = get_package_share_directory(""{0}"")

    default_urdf_path = os.path.join({0}_pkg, ""urdf"", ""{0}.urdf"")

    urdf_path_arg = DeclareLaunchArgument(
        name=""urdf_path"",
        default_value=default_urdf_path
    )

    urdf_path = LaunchConfiguration(""urdf_path"")
    robot_description_content = open(default_urdf_path).read()

    robot_state_publisher = Node(
        package=""robot_state_publisher"",
        executable=""robot_state_publisher"",
        parameters=[{{""robot_description"": robot_description_content}}],
    )

    joint_state_publisher = Node(
        package=""joint_state_publisher"",
        executable=""joint_state_publisher"",
    )

    rviz_node = Node(
        package=""rviz2"",
        executable=""rviz2"",
        name=""rviz2""
    )

    return LaunchDescription([
        urdf_path_arg,
        robot_state_publisher,
        joint_state_publisher,
        rviz_node
    ])", package);
            File.WriteAllText(path, content);
        }
    }
}
