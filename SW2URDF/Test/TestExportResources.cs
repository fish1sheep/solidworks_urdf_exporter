using SW2URDF.URDF;
using SW2URDF.URDFExport;
using System;
using System.IO;
using System.Xml;
using Xunit;

namespace SW2URDF.Test
{
    // These regressions exercise package generation and validation without SolidWorks.
    public class TestExportResources
    {
        [Theory]
        [InlineData("3_DOF_ARM", "robot_3_dof_arm")]
        [InlineData("My Robot.SLDPRT", "my_robot")]
        public void DefaultPackageNamesFollowRosNamingRules(string title, string expected)
        {
            Assert.Equal(expected, ExportValidation.SuggestPackageName(title));
        }

        [Fact]
        public void SharedPackageFilesUseTheExportedNameAndRootFrame()
        {
            string directory = Path.Combine(Path.GetTempPath(), "sw2urdf-resources-" + Guid.NewGuid().ToString("N"));
            try
            {
                Robot robot = CreateRobot();
                robot.Name = "renamed_robot";
                robot.BaseLink.Name = "root'frame";
                URDFPackage package = new URDFPackage(robot.Name, directory);
                package.CreatePackageFiles(robot);

                XmlDocument manifest = new XmlDocument();
                manifest.Load(Path.Combine(package.WindowsPackageDirectory, "package.xml"));
                Assert.Equal(robot.Name, manifest.SelectSingleNode("/package/name").InnerText);
                Assert.NotNull(manifest.SelectSingleNode("/package/exec_depend[text()='xacro']"));
                Assert.NotNull(manifest.SelectSingleNode("/package/test_depend[text()='ament_lint_auto']"));
                string launch = File.ReadAllText(Path.Combine(package.WindowsLaunchDirectory, "display.launch.py"));
                Assert.Contains("renamed_robot.urdf", launch);
                Assert.Contains("value_type=str", launch);
                Assert.Contains("Command(['xacro \"', LaunchConfiguration('urdf_path'), '\"'])", launch);
                Assert.Contains("rviz textures", File.ReadAllText(package.WindowsCMakeLists));
                Assert.Contains("Fixed Frame: 'root''frame'", File.ReadAllText(package.WindowsRvizConfig));
                Assert.Contains("Target Frame: 'root''frame'", File.ReadAllText(package.WindowsRvizConfig));
                Assert.Contains("joint_names: []", File.ReadAllText(package.WindowsConfigYAML));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Fact]
        public void CyclicLinkTreesAreRejectedBeforeRecursiveExport()
        {
            Robot robot = CreateRobot();
            robot.BaseLink.Children.Add(robot.BaseLink);
            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                ExportValidation.Validate(robot, "test_robot", Path.GetTempPath()));
            Assert.Contains("cycle", error.Message);
        }

        [Fact]
        public void DuplicateLinkNamesAreRejected()
        {
            Robot robot = CreateRobot();
            robot.BaseLink.Children.Add(CreateChild(robot.BaseLink, "arm", "joint_1"));
            robot.BaseLink.Children.Add(CreateChild(robot.BaseLink, "arm", "joint_2"));
            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                ExportValidation.Validate(robot, "test_robot", Path.GetTempPath()));
            Assert.Contains("Duplicate link name", error.Message);
        }

        [Fact]
        public void UnknownMimicReferencesAreRejected()
        {
            Robot robot = CreateRobot();
            Link child = CreateChild(robot.BaseLink, "arm", "joint_1");
            child.Joint.Mimic.JointName = "missing_joint";
            robot.BaseLink.Children.Add(child);
            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                ExportValidation.Validate(robot, "test_robot", Path.GetTempPath()));
            Assert.Contains("unknown joint", error.Message);
        }

        [Fact]
        public void ExportCopiesKeepParentsInsideTheCopiedTree()
        {
            Robot robot = CreateRobot();
            Link child = CreateChild(robot.BaseLink, "arm", "joint_1");
            robot.BaseLink.Children.Add(child);
            Link copy = robot.BaseLink.Clone();
            Assert.NotSame(robot.BaseLink, copy);
            Assert.NotSame(child, copy.Children[0]);
            Assert.Same(copy, copy.Children[0].Parent);
            copy.Children[0].Name = "renamed_arm";
            Assert.Equal("arm", child.Name);
        }

        private static Robot CreateRobot()
        {
            Robot robot = new Robot();
            robot.BaseLink.Name = "base_link";
            return robot;
        }

        private static Link CreateChild(Link parent, string name, string jointName)
        {
            Link child = new Link(parent) { Name = name };
            child.Joint.Name = jointName;
            child.Joint.Type = "fixed";
            child.Joint.Parent.Name = parent.Name;
            child.Joint.Child.Name = name;
            return child;
        }
    }
}
