# Auto-generated ROS2 launch file — created by SolidWorks URDF Exporter
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
    pkg_share = get_package_share_directory('block_description')

    default_urdf_path = os.path.join(pkg_share, 'urdf', 'BlockA.urdf')
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
    # ============================================================
    robot_state_publisher = Node(
        package='robot_state_publisher',
        executable='robot_state_publisher',
        name='robot_state_publisher',
        output='screen',
        parameters=[{
            'robot_description': Command([
                'xacro ', LaunchConfiguration('urdf_path')
            ]),
            'use_sim_time': LaunchConfiguration('use_sim_time'),
        }],
    )

    # ============================================================
    # joint_state_publisher node: publishes default joint angles
    # for non-fixed joints.
    # ============================================================
    joint_state_publisher_gui = Node(
        package='joint_state_publisher_gui',
        executable='joint_state_publisher_gui',
        name='joint_state_publisher_gui',
        output='screen',
        condition=IfCondition(LaunchConfiguration('use_gui')),
        parameters=[{
            'use_sim_time': LaunchConfiguration('use_sim_time'),
        }],
    )

    joint_state_publisher = Node(
        package='joint_state_publisher',
        executable='joint_state_publisher',
        name='joint_state_publisher',
        output='screen',
        condition=UnlessCondition(LaunchConfiguration('use_gui')),
        parameters=[{
            'use_sim_time': LaunchConfiguration('use_sim_time'),
        }],
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
        parameters=[{
            'use_sim_time': LaunchConfiguration('use_sim_time'),
        }],
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
