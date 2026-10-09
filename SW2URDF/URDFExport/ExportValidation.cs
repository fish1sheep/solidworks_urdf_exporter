using SW2URDF.URDF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SW2URDF.URDFExport
{
    /// <summary>
    /// Checks export inputs before any files or SolidWorks settings are changed.
    /// Does not access SolidWorks COM objects.
    /// </summary>
    public static class ExportValidation
    {
        public static string SuggestPackageName(string modelTitle)
        {
            string name = Regex.Replace(Path.GetFileNameWithoutExtension(modelTitle).ToLowerInvariant(),
                "[^a-z0-9_]", "_").Trim('_');
            if (name.Length == 0)
            {
                return "robot";
            }
            return name[0] >= 'a' && name[0] <= 'z' ? name : "robot_" + name;
        }

        public static void Validate(Robot robot, string packageName, string savePath)
        {
            if (string.IsNullOrWhiteSpace(packageName) ||
                !Regex.IsMatch(packageName, @"\A[a-z][a-z0-9_]*\z"))
            {
                throw new ArgumentException("Choose a ROS2 package name starting with a lowercase letter, " +
                    "using only lowercase letters, digits and underscores (for example: my_robot).");
            }
            if (string.IsNullOrWhiteSpace(savePath) || !Path.IsPathRooted(savePath))
            {
                throw new ArgumentException("Choose an absolute export directory.");
            }
            Path.GetFullPath(savePath);
            if (robot == null || robot.BaseLink == null)
            {
                throw new ArgumentException("The robot must contain a root link.");
            }

            HashSet<Link> visited = new HashSet<Link>();
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> meshNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Joint> joints = new Dictionary<string, Joint>(StringComparer.Ordinal);
            Dictionary<string, string> textures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ValidateLink(robot.BaseLink, null, visited, names, meshNames, joints, textures);

            foreach (KeyValuePair<string, Joint> entry in joints)
            {
                HashSet<string> chain = new HashSet<string>(StringComparer.Ordinal);
                string current = entry.Key;
                while (true)
                {
                    if (!chain.Add(current))
                    {
                        throw new ArgumentException("Mimic joints contain a cycle involving " + current + ".");
                    }
                    Joint joint = joints[current];
                    if (joint.Mimic == null || !joint.Mimic.ElementContainsData())
                    {
                        break;
                    }
                    string target = joint.Mimic.JointName;
                    if (string.IsNullOrWhiteSpace(target) || !joints.ContainsKey(target))
                    {
                        throw new ArgumentException("Mimic joint " + current + " references an unknown joint: " + target);
                    }
                    current = target;
                }
            }
            if (!robot.BaseLink.AreRequiredFieldsSatisfied())
            {
                throw new ArgumentException("Required link or joint properties are missing. Complete them before exporting.");
            }
        }

        private static void ValidateLink(Link link, Link parent, HashSet<Link> visited,
            HashSet<string> names, HashSet<string> meshNames, Dictionary<string, Joint> joints,
            Dictionary<string, string> textures)
        {
            if (link == null || !visited.Add(link))
            {
                throw new ArgumentException("The link structure contains a cycle, a reused link or an empty child.");
            }
            if (string.IsNullOrWhiteSpace(link.Name) || link.Name.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new ArgumentException("Each link needs a non-empty name without line breaks.");
            }
            if (!names.Add(link.Name))
            {
                throw new ArgumentException("Duplicate link name: " + link.Name);
            }
            if (link.Children == null)
            {
                throw new ArgumentException("The child list is missing for link " + link.Name);
            }
            if (parent != null)
            {
                Joint joint = link.Joint;
                if (joint == null || !joint.ElementContainsData() || !Joint.AvailableTypes.Contains(joint.Type))
                {
                    throw new ArgumentException("Link " + link.Name + " needs a named joint with a valid type.");
                }
                if (joint.Name.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                {
                    throw new ArgumentException("Joint names cannot contain line breaks: " + joint.Name);
                }
                if (joints.ContainsKey(joint.Name))
                {
                    throw new ArgumentException("Duplicate joint name: " + joint.Name);
                }
                joints.Add(joint.Name, joint);
                if (joint.Parent.Name != parent.Name || joint.Child.Name != link.Name)
                {
                    throw new ArgumentException("Joint " + joint.Name + " must connect " + parent.Name + " to " + link.Name + ".");
                }
            }
            else if (link.Joint != null && link.Joint.ElementContainsData())
            {
                throw new ArgumentException("The root link cannot have a parent joint.");
            }

            if (!link.isFixedFrame)
            {
                string meshName = link.Name.Replace('/', '_');
                if (meshName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    meshName.EndsWith(".", StringComparison.Ordinal) ||
                    meshName.EndsWith(" ", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Link name cannot be used as a mesh filename: " + link.Name);
                }
                if (!meshNames.Add(meshName))
                {
                    throw new ArgumentException("Link names produce the same Windows mesh filename: " + meshName);
                }
                string texturePath = link.Visual.Material.Texture.wFilename;
                if (!string.IsNullOrWhiteSpace(texturePath))
                {
                    if (!File.Exists(texturePath))
                    {
                        throw new ArgumentException("Texture file is missing for link " + link.Name + ": " + texturePath);
                    }
                    string filename = Path.GetFileName(texturePath);
                    string fullPath = Path.GetFullPath(texturePath);
                    if (textures.TryGetValue(filename, out string previous) &&
                        !string.Equals(previous, fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException("Different texture files have the same filename: " + filename);
                    }
                    textures[filename] = fullPath;
                }
            }
            foreach (Link child in link.Children)
            {
                ValidateLink(child, link, visited, names, meshNames, joints, textures);
            }
        }
    }
}
