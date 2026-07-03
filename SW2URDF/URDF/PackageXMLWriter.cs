using log4net;
using SW2URDF.Utilities;
using System;
using System.Text;
using System.Xml;

namespace SW2URDF.URDF
{
    /// <summary>
    /// XML writer for ROS2 package.xml manifest files.
    /// Configures UTF-8 encoding without BOM and indented formatting for readability.
    /// </summary>
    public class PackageXMLWriter : IDisposable
    {
        public XmlWriter writer;
        private static readonly ILog logger = Logger.GetLogger();

        public PackageXMLWriter(string savePath)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = new UTF8Encoding(false);
            settings.OmitXmlDeclaration = true;
            settings.Indent = true;
            settings.NewLineOnAttributes = false;
            logger.Info("Creating package.xml at " + savePath);
            writer = XmlWriter.Create(savePath, settings);
        }

        public void Dispose()
        {
            writer?.Close();
            writer?.Dispose();
        }
    }

    /// <summary>
    /// Base class for package.xml elements.
    /// </summary>
    public class PackageElement
    {
    }

    /// <summary>
    /// Top-level element for package.xml, corresponding to ROS2 package format 3.
    /// Contains description (name/version/description), author, maintainer,
    /// license, dependencies, and export sub-elements.
    ///
    /// Usage:
    ///   var pkg = new PackageXML("my_robot_description");
    ///   pkg.SetAuthor("Your Name", "your.email@example.com");
    ///   pkg.Version = "2.0.0";
    ///   pkg.WriteElement(writer);
    /// </summary>
    public class PackageXML : PackageElement
    {
        /// <summary>
        /// Package description (name, version, brief, and detailed description).
        /// </summary>
        public Description description;

        /// <summary>
        /// Dependency declarations (buildtool_depend, build_depend, exec_depend).
        /// </summary>
        public Dependencies dependencies;

        /// <summary>
        /// Author and maintainer information. Can be changed via SetAuthor().
        /// </summary>
        public Author author;

        /// <summary>
        /// Open-source license declaration.
        /// </summary>
        public License license;

        /// <summary>
        /// Export declaration (build_type).
        /// </summary>
        public Export export;

        /// <summary>
        /// Package version string. Default is "1.0.0".
        /// Can be set directly via this property.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Constructs default package.xml content.
        /// Author defaults to "TODO"; call SetAuthor() before writing to override.
        /// </summary>
        /// <param name="name">ROS2 package name</param>
        public PackageXML(string name)
        {
            // Set default version
            Version = "1.0.0";

            description = new Description(name);

            // Build tool dependency: ament_cmake
            // Build dependency: empty array (description-only packages have none)
            // Exec dependency: packages needed for ROS2 visualization and launch
            dependencies = new Dependencies(
                new string[] { "ament_cmake" },
                new string[] { },
                new string[] {
                    "ros2launch", "robot_state_publisher", "rviz2", "joint_state_publisher_gui" });

            // Default author placeholder; set via SetAuthor() before writing to override.
            author = new Author("ros2_user");

            license = new License("BSD");

            export = new Export(new string[] { "ament_cmake" });
        }

        /// <summary>
        /// Sets the author and maintainer information for the package.
        /// The author name is also used as the maintainer name;
        /// the maintainer email is set separately.
        /// </summary>
        /// <param name="name">Author/maintainer name</param>
        /// <param name="email">Maintainer email address</param>
        public void SetAuthor(string name, string email)
        {
            author = new Author(name, email);
        }

        /// <summary>
        /// Writes the complete package.xml to the XML file.
        /// </summary>
        /// <param name="mWriter">PackageXMLWriter instance</param>
        public void WriteElement(PackageXMLWriter mWriter)
        {
            XmlWriter writer = mWriter.writer;
            writer.WriteStartDocument();
            writer.WriteStartElement("package");
            writer.WriteAttributeString("format", "3");

            // description: name, version, description/p
            description.WriteElement(writer, Version);

            // author and maintainer
            author.WriteElement(writer);

            // license
            license.WriteElement(writer);

            // dependencies: buildtool_depend, build_depend, exec_depend
            dependencies.WriteElement(writer);

            // export: build_type
            writer.WriteStartElement("export");
            export.WriteElement(writer);
            writer.WriteEndElement();

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
    }

    /// <summary>
    /// The &lt;description&gt; element of package.xml.
    /// Contains the package name (name), version (version),
    /// a brief summary, and a long description.
    /// </summary>
    public class Description : PackageElement
    {
        private readonly string name;
        private readonly string brief;
        private readonly string longDescription;

        public Description(string name)
        {
            this.name = name;
            brief = "URDF Description package for " + name;
            longDescription = "This package contains configuration data, 3D models and launch files\r\n" +
                                    "for " + name + " robot";
        }

        /// <summary>
        /// Writes the description-related XML elements.
        /// </summary>
        /// <param name="writer">XML writer</param>
        /// <param name="version">Package version string</param>
        public void WriteElement(XmlWriter writer, string version)
        {
            // Package name
            writer.WriteStartElement("name");
            writer.WriteString(name);
            writer.WriteEndElement();

            // Version (configurable, default "1.0.0")
            writer.WriteStartElement("version");
            writer.WriteString(version);
            writer.WriteEndElement();

            // Description text (brief + long, each in its own <p> paragraph)
            writer.WriteStartElement("description");

            writer.WriteStartElement("p");
            writer.WriteString(brief);
            writer.WriteEndElement();

            writer.WriteStartElement("p");
            writer.WriteString(longDescription);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }
    }

    /// <summary>
    /// Dependency declarations for package.xml.
    ///
    /// ROS2 package format 3 supports three dependency types:
    ///   - buildtool_depend: Build tool dependencies (e.g. ament_cmake, ament_python)
    ///   - build_depend:     Build-time dependencies (packages providing headers,
    ///                       libraries, or message definitions)
    ///   - exec_depend:      Run-time dependencies (packages needed at execution)
    ///
    /// For description-only packages (URDF, meshes, launch files only),
    /// typically only buildtool_depend and exec_depend are needed;
    /// the build_depend array may be empty.
    /// </summary>
    public class Dependencies : PackageElement
    {
        /// <summary>
        /// Build tool dependencies (buildtool_depend), e.g. ament_cmake.
        /// </summary>
        private readonly string[] buildTool;

        /// <summary>
        /// Build-time dependencies (build_depend). Typically empty for description packages.
        /// </summary>
        private readonly string[] build;

        /// <summary>
        /// Run-time dependencies (exec_depend), e.g. robot_state_publisher, rviz2.
        /// </summary>
        private readonly string[] buildExec;

        /// <summary>
        /// Constructs the dependency declarations.
        /// </summary>
        /// <param name="buildTool">Build tool dependencies (e.g. "ament_cmake")</param>
        /// <param name="build">Build-time dependencies (may be empty)</param>
        /// <param name="buildExec">Run-time dependencies</param>
        public Dependencies(string[] buildTool, string[] build, string[] buildExec)
        {
            this.buildTool = buildTool;
            this.build = build;
            this.buildExec = buildExec;
        }

        /// <summary>
        /// Writes all dependency XML elements.
        /// Output order: buildtool_depend → build_depend → exec_depend.
        /// </summary>
        /// <param name="writer">XML writer</param>
        public void WriteElement(XmlWriter writer)
        {
            // buildtool_depend: build tool dependencies
            foreach (string depend in buildTool)
            {
                writer.WriteStartElement("buildtool_depend");
                writer.WriteString(depend);
                writer.WriteEndElement();
            }

            // build_depend: build-time dependencies (may be empty)
            foreach (string depend in build)
            {
                writer.WriteStartElement("build_depend");
                writer.WriteString(depend);
                writer.WriteEndElement();
            }

            // exec_depend: run-time dependencies
            foreach (string depend in buildExec)
            {
                writer.WriteStartElement("exec_depend");
                writer.WriteString(depend);
                writer.WriteEndElement();
            }
        }
    }

    /// <summary>
    /// The &lt;export&gt; sub-element of package.xml — build_type.
    /// Declares the ament build type (e.g. ament_cmake, ament_python).
    /// </summary>
    public class Export : PackageElement
    {
        private readonly string[] buildtype;

        public Export(string[] buildtype)
        {
            this.buildtype = buildtype;
        }

        public void WriteElement(XmlWriter writer)
        {
            foreach (string export in buildtype)
            {
                writer.WriteStartElement("build_type");
                writer.WriteString(export);
                writer.WriteEndElement();
            }
        }
    }

    /// <summary>
    /// The &lt;author&gt; and &lt;maintainer&gt; elements of package.xml.
    ///
    /// ROS2 package format 3 requires at least one &lt;maintainer&gt; tag
    /// with a mandatory email attribute. &lt;author&gt; is optional.
    ///
    /// Defaults can be changed via PackageXML.SetAuthor(name, email).
    /// </summary>
    public class Author : PackageElement
    {
        private readonly string name;
        private readonly string email;

        /// <summary>
        /// Constructs author/maintainer with default values.
        /// Default name is "TODO", email is "unknown@unknown.com".
        /// It is recommended to call PackageXML.SetAuthor() before exporting.
        /// </summary>
        /// <param name="name">Author name (also used as maintainer name)</param>
        public Author(string name)
        {
            this.name = name;
            this.email = "unknown@unknown.com";
        }

        /// <summary>
        /// Constructs author/maintainer with specified name and email.
        /// </summary>
        /// <param name="name">Author/maintainer name</param>
        /// <param name="email">Maintainer email</param>
        public Author(string name, string email)
        {
            this.name = name;
            this.email = email;
        }

        /// <summary>
        /// Writes the author and maintainer XML elements.
        /// The maintainer element includes the mandatory email attribute.
        /// </summary>
        /// <param name="writer">XML writer</param>
        public void WriteElement(XmlWriter writer)
        {
            // author element (optional, but recommended)
            writer.WriteStartElement("author");
            writer.WriteString(name);
            writer.WriteEndElement();

            // maintainer element (required by ROS2 format 3, email attribute is mandatory)
            writer.WriteStartElement("maintainer");
            writer.WriteAttributeString("email", email);
            writer.WriteString(name);
            writer.WriteEndElement();
        }
    }

    /// <summary>
    /// The &lt;license&gt; element of package.xml.
    /// Declares the package license (e.g. BSD, MIT, Apache-2.0).
    /// </summary>
    public class License : PackageElement
    {
        private readonly string lic;

        public License(string lic)
        {
            this.lic = lic;
        }

        public void WriteElement(XmlWriter writer)
        {
            writer.WriteStartElement("license");
            writer.WriteString(lic);
            writer.WriteEndElement();
        }
    }
}
