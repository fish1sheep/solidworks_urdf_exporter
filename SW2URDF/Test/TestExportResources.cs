using SW2URDF.URDF;
using SW2URDF.URDFExport;
using SW2URDF.URDFExport.CSV;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
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

        [Fact]
        public void ConfigurationRoundTripPreservesUnicodeNames()
        {
            LinkNode node = new LinkNode(new Link { Name = "机械臂底座" });
            Type type = typeof(ConfigurationSerialization);
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            string data = (string)type.GetMethod("SerializeToString", flags).Invoke(null, new object[] { node });
            LinkNode loaded = (LinkNode)type.GetMethod("DeserializeFromString", flags)
                .Invoke(null, new object[] { data });
            Assert.Equal(node.Name, loaded.Link.Name);
        }

        [Theory]
        [InlineData("001")]
        [InlineData("1;2")]
        public void CsvRoundTripKeepsTextNamesAndDotDecimals(string name)
        {
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            string file = Path.Combine(Path.GetTempPath(), "sw2urdf-csv-" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Robot robot = CreateRobot();
                robot.BaseLink.Name = name;
                robot.BaseLink.Inertial.Mass.Value = 1.25;
                robot.BaseLink.Visual.Material.Color.Red = 0.25;
                ImportExport.WriteRobotToCSV(robot, file);
                using (Stream stream = File.OpenRead(file))
                {
                    Link loaded = ImportExport.LoadURDFRobotFromCSV(stream)[0];
                    Assert.Equal(name, loaded.Name);
                    Assert.Equal(1.25, loaded.Inertial.Mass.Value);
                    Assert.Equal(0.25, loaded.Visual.Material.Color.Red);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
                File.Delete(file);
            }
        }

        [Theory]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("oops")]
        public void InvalidCsvNumbersIdentifyTheRowAndField(string number)
        {
            using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("Link Name,Mass\nbase_link," + number + "\n")))
            {
                InvalidDataException error = Assert.Throws<InvalidDataException>(() => ImportExport.LoadURDFRobotFromCSV(stream));
                Assert.Contains("row 2", error.Message);
                Assert.Contains("Mass.value", error.Message);
            }
        }

        [Fact]
        public void NonFiniteNumbersAndInvalidJointGeometryAreRejected()
        {
            Robot robot = CreateRobot();
            robot.BaseLink.Inertial.Mass.Value = Double.NaN;
            Assert.Throws<ArgumentException>(() => ExportValidation.Validate(robot, "test_robot", Path.GetTempPath()));
            robot.BaseLink.Inertial.Mass.Value = 0;
            Link child = CreateChild(robot.BaseLink, "arm", "joint_1");
            child.Joint.Type = "revolute";
            child.Joint.Axis.X = 1;
            child.Joint.Limit.Lower = 2;
            child.Joint.Limit.Upper = 1;
            child.Joint.Limit.Effort = 1;
            child.Joint.Limit.Velocity = 1;
            robot.BaseLink.Children.Add(child);
            Assert.Contains("lower limit", Assert.Throws<ArgumentException>(() =>
                ExportValidation.Validate(robot, "test_robot", Path.GetTempPath())).Message);
            child.Joint.Limit.Lower = 0;
            child.Joint.Axis.X = 0;
            Assert.Contains("non-zero axis", Assert.Throws<ArgumentException>(() =>
                ExportValidation.Validate(robot, "test_robot", Path.GetTempPath())).Message);
        }

        [Fact]
        public void FailedAndCancelledExportsPreserveThePreviousPackage()
        {
            string directory = Path.Combine(Path.GetTempPath(), "sw2urdf-rollback-" + Guid.NewGuid().ToString("N"));
            string target = Path.Combine(directory, "test_robot");
            try
            {
                Directory.CreateDirectory(target);
                string previousFile = Path.Combine(target, "previous.txt");
                File.WriteAllText(previousFile, "previous package");
                using (PackageExportTransaction export = new PackageExportTransaction("test_robot", directory))
                {
                    File.WriteAllText(Path.Combine(export.Package.WindowsPackageDirectory, "previous.txt"), "partial package");
                }
                Assert.Equal("previous package", File.ReadAllText(previousFile));
                using (PackageExportTransaction export = new PackageExportTransaction("test_robot", directory))
                {
                    // Force installation to fail after the previous package has been moved to its backup.
                    Directory.Move(export.Package.WindowsPackageDirectory.TrimEnd(Path.DirectorySeparatorChar),
                        Path.Combine(directory, "unavailable"));
                    Assert.ThrowsAny<IOException>(() => export.Commit());
                }
                Assert.Equal("previous package", File.ReadAllText(previousFile));
                Assert.Empty(Directory.GetDirectories(directory, ".sw2urdf-*"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void SuccessfulExportKeepsExistingResourcesAndInstallsTheNewPackage()
        {
            string directory = Path.Combine(Path.GetTempPath(), "sw2urdf-commit-" + Guid.NewGuid().ToString("N"));
            try
            {
                string target = Path.Combine(directory, "test_robot");
                Directory.CreateDirectory(Path.Combine(target, "meshes"));
                File.WriteAllText(Path.Combine(target, "meshes", "existing.STL"), "mesh");
                File.WriteAllText(Path.Combine(target, "robot.urdf"), "previous");
                using (PackageExportTransaction export = new PackageExportTransaction("test_robot", directory))
                {
                    File.WriteAllText(Path.Combine(export.Package.WindowsPackageDirectory, "robot.urdf"), "new");
                    Assert.Equal("previous", File.ReadAllText(Path.Combine(target, "robot.urdf")));
                    export.Commit();
                }
                Assert.Equal("new", File.ReadAllText(Path.Combine(target, "robot.urdf")));
                Assert.Equal("mesh", File.ReadAllText(Path.Combine(target, "meshes", "existing.STL")));
                Assert.Empty(Directory.GetDirectories(directory, ".sw2urdf-*"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
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
