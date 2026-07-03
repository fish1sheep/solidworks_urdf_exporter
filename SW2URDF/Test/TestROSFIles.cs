using Moq;
using SW2URDF.ROS;
using SW2URDF.URDF;
using SW2URDF.UI;
using SW2URDF.URDFExport;
using System;
using System.IO;
using System.Text;
using System.Windows;
using Xunit;

namespace SW2URDF.Test
{
    /// <summary>
    /// Tests for ROS2 file generation (package.xml, CMakeLists.txt, display.launch.py, config YAML).
    /// Verifies that the generated files use ROS2 format and conventions.
    /// </summary>
    public class TestROSFIles : SW2URDFTest
    {
        public TestROSFIles(SWTestFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void TestPackageXMLWritesFormat3()
        {
            string tempDirectory = CreateRandomTempDirectory();
            string packageXmlPath = Path.Combine(tempDirectory, "package.xml");

            // Create PackageXML with default values
            PackageXML pkg = new PackageXML("test_robot_description");
            PackageXMLWriter writer = new PackageXMLWriter(packageXmlPath);
            pkg.WriteElement(writer);

            // Verify file exists
            Assert.True(File.Exists(packageXmlPath));

            // Read and verify contents
            string content = File.ReadAllText(packageXmlPath);

            // ROS2 format 3 marker
            Assert.Contains("format=\"3\"", content);

            // ROS2 build tool dependency (not catkin)
            Assert.Contains("buildtool_depend", content);
            Assert.Contains("ament_cmake", content);
            Assert.DoesNotContain("catkin", content);

            // ROS2 exec dependencies (not depend)
            Assert.Contains("exec_depend", content);
            Assert.DoesNotContain("<depend>", content);
            Assert.Contains("ros2launch", content);
            Assert.Contains("rviz2", content);
            Assert.Contains("robot_state_publisher", content);
            Assert.Contains("joint_state_publisher_gui", content);

            // ROS2 build_type export (not architecture_independent)
            Assert.Contains("build_type", content);
            Assert.Contains("ament_cmake", content);
            Assert.DoesNotContain("architecture_independent", content);

            // Maintainer should have email attribute
            Assert.Contains("email=", content);

            // Clean up
            Directory.Delete(tempDirectory, true);
        }

        [Fact]
        public void TestPackageXMLAuthorSet()
        {
            string tempDirectory = CreateRandomTempDirectory();
            string packageXmlPath = Path.Combine(tempDirectory, "package.xml");

            PackageXML pkg = new PackageXML("test_robot_description");
            pkg.SetAuthor("Test Author", "test@example.com");
            PackageXMLWriter writer = new PackageXMLWriter(packageXmlPath);
            pkg.WriteElement(writer);

            string content = File.ReadAllText(packageXmlPath);

            Assert.Contains("Test Author", content);
            Assert.Contains("test@example.com", content);

            Directory.Delete(tempDirectory, true);
        }

        [Fact]
        public void TestRvizWritesDisplayLaunchPy()
        {
            string tempDirectory = CreateRandomTempDirectory();

            Rviz rviz = new Rviz("test_robot_description", "test_robot.urdf");
            rviz.WriteFiles(tempDirectory);

            string launchPath = Path.Combine(tempDirectory, "display.launch.py");
            Assert.True(File.Exists(launchPath));

            string content = File.ReadAllText(launchPath);

            // ROS2 Python launch API
            Assert.Contains("ament_index_python", content);
            Assert.Contains("launch_ros", content);
            Assert.Contains("generate_launch_description", content);
            Assert.Contains("LaunchDescription", content);

            // ROS2 packages (not ROS1)
            Assert.Contains("robot_state_publisher", content);
            Assert.Contains("rviz2", content);
            Assert.Contains("joint_state_publisher_gui", content);
            Assert.Contains("joint_state_publisher", content);

            // Uses xacro for URDF loading (ROS2 standard)
            Assert.Contains("xacro", content);

            // Should reference the correct package and URDF path
            Assert.Contains("test_robot_description", content);
            Assert.Contains("test_robot.urdf", content);

            // Should reference rviz config
            Assert.Contains("urdf.rviz", content);

            Directory.Delete(tempDirectory, true);
        }

        [Fact]
        public void TestCreateCMakeListsAmentFormat()
        {
            string tempDirectory = CreateRandomTempDirectory();
            string name = Path.GetRandomFileName();
            URDFPackage pkg = new URDFPackage(name, tempDirectory);
            Mock<IMessageBox> messageBoxMock = new Mock<IMessageBox>();
            messageBoxMock.Setup(m => m.Show(It.IsAny<string>()))
                .Returns(MessageBoxResult.OK);
            URDFPackage.MessageBox = messageBoxMock.Object;
            pkg.CreateDirectories();
            pkg.CreateCMakeLists();

            Assert.True(File.Exists(pkg.WindowsCMakeLists));

            string content = File.ReadAllText(pkg.WindowsCMakeLists);

            // ROS2 CMake conventions
            Assert.Contains("cmake_minimum_required(VERSION 3.8)", content);
            Assert.Contains("find_package(ament_cmake REQUIRED)", content);
            Assert.Contains("ament_package()", content);
            Assert.DoesNotContain("catkin", content);
            Assert.DoesNotContain("CATKIN", content);

            // ROS2 install directory syntax
            Assert.Contains("share/${PROJECT_NAME}", content);

            Directory.Delete(tempDirectory, true);
        }

        [Fact]
        public void TestCreateConfigYAMLJointNamesKey()
        {
            string tempDirectory = CreateRandomTempDirectory();
            string name = Path.GetRandomFileName();
            URDFPackage pkg = new URDFPackage(name, tempDirectory);
            Mock<IMessageBox> messageBoxMock = new Mock<IMessageBox>();
            messageBoxMock.Setup(m => m.Show(It.IsAny<string>()))
                .Returns(MessageBoxResult.OK);
            URDFPackage.MessageBox = messageBoxMock.Object;
            pkg.CreateDirectories();

            string[] jointNames = { "joint_1", "joint_2", "joint_3" };
            pkg.CreateConfigYAML(jointNames);

            Assert.True(File.Exists(pkg.WindowsConfigYAML));

            string content = File.ReadAllText(pkg.WindowsConfigYAML);

            // ROS2-neutral joint_names key (not ROS1 controller_joint_names)
            Assert.Contains("joint_names:", content);
            Assert.DoesNotContain("controller_joint_names", content);

            // Joint names should be listed
            Assert.Contains("joint_1", content);
            Assert.Contains("joint_2", content);
            Assert.Contains("joint_3", content);

            Directory.Delete(tempDirectory, true);
        }

        [Fact]
        public void TestCreateDefaultRvizConfig()
        {
            string tempDirectory = CreateRandomTempDirectory();
            string name = Path.GetRandomFileName();
            URDFPackage pkg = new URDFPackage(name, tempDirectory);
            Mock<IMessageBox> messageBoxMock = new Mock<IMessageBox>();
            messageBoxMock.Setup(m => m.Show(It.IsAny<string>()))
                .Returns(MessageBoxResult.OK);
            URDFPackage.MessageBox = messageBoxMock.Object;
            pkg.CreateDirectories();
            pkg.CreateDefaultRvizConfig();

            Assert.True(File.Exists(pkg.WindowsRvizConfig));

            string content = File.ReadAllText(pkg.WindowsRvizConfig);

            // Should have RViz2-compatible configuration
            Assert.Contains("RobotModel", content);
            Assert.Contains("base_link", content);

            Directory.Delete(tempDirectory, true);
        }
    }
}
