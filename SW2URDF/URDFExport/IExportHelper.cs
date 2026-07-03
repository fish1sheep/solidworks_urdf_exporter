using SolidWorks.Interop.sldworks;
using SW2URDF.URDF;
using System.Collections.Generic;

namespace SW2URDF.URDFExport
{
    /// <summary>
    /// Interface for the URDF export pipeline. Abstracts the SW COM-dependent
    /// ExportHelper implementation so that consumers (UI forms, tests) can depend
    /// on the contract rather than the concrete class.
    /// </summary>
    public interface IExportHelper
    {
        /// <summary>SolidWorks application object. Used internally for COM operations.</summary>
        ISldWorks iSwApp { get; set; }

        /// <summary>The active SolidWorks document being exported.</summary>
        ModelDoc2 ActiveSWModel { get; set; }

        /// <summary>COM Property ID for the math utility (used for coordinate transforms).</summary>
        object SWMathPID { get; set; }

        /// <summary>The URDF robot model being built during the export process.</summary>
        Robot URDFRobot { get; set; }

        /// <summary>Name of the ROS2 package being created.</summary>
        string PackageName { get; set; }

        /// <summary>Filesystem path where the package will be saved.</summary>
        string SavePath { get; set; }

        /// <summary>List of all Link objects in the exported assembly.</summary>
        List<Link> Links { get; }

        // ==================== Export Methods ====================

        /// <summary>
        /// Sets whether inertial properties (mass, inertia) should be computed during export.
        /// </summary>
        void SetComputeInertial(bool computeInertial);

        /// <summary>
        /// Sets whether visual and collision geometry should be computed.
        /// </summary>
        void SetComputeVisualCollision(bool computeVisual);

        /// <summary>
        /// Sets whether joint kinematics (axis, type estimation) should be computed.
        /// </summary>
        void SetComputeJointKinematics(bool computeKinematics);

        /// <summary>
        /// Sets whether joint limits should be computed from SolidWorks mate limits.
        /// </summary>
        void SetComputeJointLimits(bool computeJointLimits);

        /// <summary>
        /// Exports a complete robot assembly as a ROS2 URDF description package.
        /// Creates package directory, CMakeLists.txt, package.xml, config, launch files,
        /// mesh files, URDF XML, and CSV data.
        /// </summary>
        void ExportRobot(bool exportSTL = true, MeshExportFormat meshFormat = MeshExportFormat.STL);

        /// <summary>
        /// Returns the list of joint names in the current URDF robot model.
        /// </summary>
        List<string> GetJointNames();

        /// <summary>
        /// Exports a single part (not an assembly) as a URDF description package.
        /// </summary>
        void ExportLink(bool zIsUp);

        /// <summary>
        /// Creates the robot model data structure from the currently active SolidWorks document.
        /// </summary>
        void CreateRobotFromActiveModel();

        /// <summary>
        /// Creates the robot model data structure from a LinkNode tree (from the Property Manager Page).
        /// </summary>
        /// <returns>True if the robot was created successfully</returns>
        bool CreateRobotFromTreeView(LinkNode baseNode);

        /// <summary>
        /// Estimates the global joint origin and axis between two links based on their component geometry.
        /// </summary>
        bool EstimateGlobalJointFromComponents(Link parent, Link child);

        /// <summary>
        /// Estimates the joint axis direction from a named reference axis feature.
        /// </summary>
        double[] EstimateAxis(string axisName);

        /// <summary>
        /// Localizes a global axis vector into a named coordinate system.
        /// </summary>
        double[] LocalizeAxis(double[] axis, string coordsys);

        /// <summary>
        /// Updates reference geometry (coordinate systems and axes) in the SolidWorks model.
        /// </summary>
        void UpdateReferenceGeometries();

        /// <summary>
        /// Returns the list of available reference coordinate system names in the model.
        /// </summary>
        List<string> GetRefCoordinateSystems();

        /// <summary>
        /// Returns the list of available reference axis names in the model.
        /// </summary>
        List<string> GetRefAxes();
    }

    /// <summary>
    /// Mesh file format for exported geometry.
    /// </summary>
    public enum MeshExportFormat { STL, THREEDXML }
}
