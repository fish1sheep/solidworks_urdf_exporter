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

using MathNet.Numerics.LinearAlgebra;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SW2URDF.URDF;
using SW2URDF.URDFExport.CSV;
using SW2URDF.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace SW2URDF.URDFExport
{
    // This class contains a long list of methods that are used throughout the export process.
    // Methods for building links and joints are contained in here.
    // Many of the methods are overloaded, but seek to reduce repeated code as much as possible
    // (i.e. the overloaded methods call eachother).
    // These methods are used by the PartExportForm, the AssemblyExportForm and the PropertyManager Page
    public partial class ExportHelper
    {
        #region class variables

        private static readonly log4net.ILog logger = Logger.GetLogger();

        [XmlIgnore]
        public ISldWorks iSwApp = null;

        [XmlIgnore]
        private bool mBinary;

        private bool mshowInfo;
        private bool mSTLPreview;
        private bool mTranslateToPositive;
        private bool mSaveComponentsIntoOneFile;
        private string mExportCoordinateSystem;
        private int mSTLUnits;
        private int mSTLQuality;
        private double mHideTransitionSpeed;

        private UserProgressBar progressBar;

        [XmlIgnore]
        public ModelDoc2 ActiveSWModel;

        [XmlIgnore]
        public MathUtility swMath;

        [XmlIgnore]
        public Object SWMathPID
        { get; set; }

        public Robot URDFRobot
        { get; set; }

        public string PackageName
        { get; set; }

        public string SavePath
        { get; set; }

        public readonly List<Link> Links;

        private readonly List<string> ReferenceCoordinateSystemNames;
        private readonly List<string> ReferenceAxesNames;

        private bool ComputeInertialValues;
        private bool ComputeVisualCollision;
        private bool ComputeJointKinematics;
        private bool ComputeJointLimits;

        #endregion class variables

        // Constructor for SW2URDF Exporter class
        public ExportHelper(SldWorks iSldWorksApp)
        {
            ConstructExporter(iSldWorksApp);
            iSwApp.GetUserProgressBar(out progressBar);

            SavePath = System.Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            PackageName = ExportValidation.SuggestPackageName(ActiveSWModel.GetTitle());

            ReferenceCoordinateSystemNames = FindRefGeoNames("CoordSys");
            ReferenceAxesNames = FindRefGeoNames("RefAxis");

            ComputeInertialValues = true;
            ComputeVisualCollision = true;
            ComputeJointKinematics = true;
            ComputeJointLimits = true;
        }

        public void SetComputeInertial(bool computeInertial)
        {
            ComputeInertialValues = computeInertial;
        }

        public void SetComputeVisualCollision(bool computeVisual)
        {
            ComputeVisualCollision = computeVisual;
        }

        public void SetComputeJointKinematics(bool computeKinematics)
        {
            ComputeJointKinematics = computeKinematics;
        }

        public void SetComputeJointLimits(bool computeJointLimits)
        {
            ComputeJointLimits = computeJointLimits;
        }

        private void ConstructExporter(SldWorks iSldWorksApp)
        {
            iSwApp = iSldWorksApp;
            ActiveSWModel = (ModelDoc2)iSwApp.ActiveDoc;
            swMath = iSwApp.GetMathUtility();
        }

        #region Export Methods

        // Beginning method for exporting the full package
        /// <summary>
        /// Exports a complete robot assembly as a ROS2 URDF description package.
        ///
        /// This method performs the full export workflow:
        ///   1. Create package directory structure (package, meshes, urdf, launch, config, textures)
        ///   2. Generate CMakeLists.txt (ament_cmake format)
        ///   3. Generate package.xml (ROS2 format 3)
        ///   4. Generate joint name configuration YAML
        ///   5. Generate display.launch.py (ROS2 Python launch file)
        ///   6. Save mesh files (STL or 3DXML format)
        ///   7. Write URDF XML file
        ///   8. Export CSV data file
        ///
        /// Note: This method temporarily modifies SolidWorks STL export preferences
        /// and restores the user's original settings on completion.
        /// </summary>
        /// <param name="exportSTL">Whether to export mesh files (default true)</param>
        /// <param name="meshFormat">Mesh export format (STL or 3DXML, default STL)</param>
        public bool ExportRobot(bool exportSTL = true, MeshExportFormat meshFormat = MeshExportFormat.STL)
        {
            Robot sourceRobot = URDFRobot;
            bool exportSucceeded = false;
            bool preferencesSaved = false;
            bool visibilityChanged = false;
            bool progressStarted = false;
            List<string> hiddenComponents = null;
            PackageExportTransaction transaction = null;
            try
            {
                ExportValidation.Validate(URDFRobot, PackageName, SavePath);
                URDFRobot = CreateExportCopy(sourceRobot);
                transaction = new PackageExportTransaction(PackageName, SavePath);
                URDFPackage package = transaction.Package;

                iSwApp.GetUserProgressBar(out progressBar);
                if (progressBar != null)
                {
                    progressStarted = true;
                    progressBar.Start(0, CommonSwOperations.GetCount(URDFRobot.BaseLink), "Creating package directories");
                }
                package.CreatePackageFiles(URDFRobot);

                if (exportSTL)
                {
                    AssemblyDoc assyDoc = (AssemblyDoc)ActiveSWModel;
                    hiddenComponents = CommonSwOperations.FindHiddenComponents(assyDoc.GetComponents(false));
                    SaveUserPreferences();
                    preferencesSaved = true;
                    SetSTLExportPreferences();
                    visibilityChanged = true;
                    ActiveSWModel.Extension.SelectAll();
                    ActiveSWModel.HideComponent2();
                }

                int count = 0;
                ExportFiles(URDFRobot.BaseLink, package, ref count, exportSTL, meshFormat);
                WriteRobotFile(package);
                ImportExport.WriteRobotToCSV(URDFRobot,
                    Path.Combine(package.WindowsRobotsDirectory, URDFRobot.Name + ".csv"));
                transaction.Commit();
                exportSucceeded = true;
                URDFPackage finalPackage = new URDFPackage(PackageName, SavePath);
                logger.Info("URDF package export completed: " + finalPackage.WindowsPackageDirectory);
                TryCopyLogFile(finalPackage);
                return true;
            }
            catch (Exception e)
            {
                ReportExportFailure(e);
                return false;
            }
            finally
            {
                if (!exportSucceeded)
                {
                    URDFRobot = sourceRobot;
                }
                RestoreExportState(preferencesSaved, visibilityChanged, hiddenComponents, progressStarted);
                CleanupExportTransaction(transaction);
            }
        }

        public List<string> GetJointNames()
        {
            return new List<string>(URDFRobot.GetJointNames(true));
        }

        //Recursive method for exporting each link (and writing it to the URDF)
        private void ExportFiles(Link link, URDFPackage package, ref int count, bool exportSTL = true, MeshExportFormat meshFormat = MeshExportFormat.STL)
        {
            if (progressBar != null)
            {
                progressBar.UpdateProgress(count++);
                progressBar.UpdateTitle("Exporting mesh: " + link.Name);
            }
            logger.Info("Exporting link: " + link.Name);
            // Iterate through each child and export its files
            logger.Info("Link " + link.Name + " has " + link.Children.Count + " children");
            foreach (Link child in link.Children)
            {
                ExportFiles(child, package, ref count, exportSTL, meshFormat);
            }
            // Fixed frames have no mesh, but their descendants still need exporting.
            if (link.isFixedFrame)
            {
                return;
            }
            CopyTexture(link, package);

            // Build package:// URI and Windows absolute path for the mesh file.
            // SolidWorks uses '/' as a component name separator — replace with '_'
            // to avoid filesystem path conflicts.
            string extension;
            switch (meshFormat)
            {
                case MeshExportFormat.THREEDXML:
                    extension = ".3dxml";
                    break;

                case MeshExportFormat.STL:
                default:
                    extension = ".STL";
                    break;
            }

            string meshFilename = package.GetMeshPackageUri(link.Name, extension);
            string windowsMeshFileName = package.GetWindowsMeshPath(link.Name, extension);

            // Export the mesh file (STL or 3DXML format)
            if (exportSTL)
            {
                switch (meshFormat)
                {
                    case MeshExportFormat.STL:
                        if (!SaveSTL(link, windowsMeshFileName))
                        {
                            throw new IOException("Could not prepare the STL mesh for link " + link.Name);
                        }
                        break;

                    case MeshExportFormat.THREEDXML:
                        Save3dxml(link, windowsMeshFileName);
                        break;

                    default:
                        if (!SaveSTL(link, windowsMeshFileName))
                        {
                            throw new IOException("Could not prepare the STL mesh for link " + link.Name);
                        }
                        break;
                }
            }

            // Write the package:// URI into the URDF model.
            // ROS2 resolves package:// prefixes at runtime via the ament resource index.
            link.Visual.Geometry.Mesh.Filename = meshFilename;
            link.Collision.Geometry.Mesh.Filename = meshFilename;
        }

        private void Save3dxml(Link link, string windowsMeshFilename)
        {
            int errors = 0;
            int warnings = 0;

            string coordsysName = link.Joint.CoordinateSystemName;

            logger.Info(link.Name + ": Exporting 3dxml with coordinate frame " + coordsysName);

            Dictionary<string, string> names = GetComponentRefGeoNames(coordsysName);
            ModelDoc2 ActiveDoc = ActiveSWModel;

            logger.Info(link.Name + ": Reference geometry name " + names["component"]);

            CommonSwOperations.ShowComponents(ActiveSWModel, link.SWComponents);

            int saveOptions = (int)swSaveAsOptions_e.swSaveAsOptions_Silent |
                (int)swSaveAsOptions_e.swSaveAsOptions_Copy;
            SetLinkSpecificSTLPreferences(names["geo"], link.STLQualityFine, ActiveDoc);

            logger.Info("Saving 3dxml to " + windowsMeshFilename);

            // === 3dxml Localize Link === //

            // Remove suffix from coordinate-system name.
            // ex. "Joint Origin <Arm_link-1>" -> "Joint Origin"
            // Suffix is included when coordinate is inside sub-assembly.
            string linkModelName = names["component"];
            string linkModelSuffix = " <" + linkModelName + ">";
            if(coordsysName.Contains(linkModelSuffix))
            {
                coordsysName = coordsysName.Replace(linkModelSuffix, "");
                logger.Info($"Suffix of {linkModelName} was removed from coordsysName : {coordsysName}");
            }

            // Get the model document of the link.
            ModelDoc2 linkModel;
            bool isBaseLink = linkModelName == "";
            if (isBaseLink)
            {
                linkModel = ActiveDoc;
            }
            else
            {
                if (link.SWMainComponent != null)
                {
                    linkModel = link.SWMainComponent.GetModelDoc2();
                }
                else
                {
                    logger.Warn("Could not get linkModel because SWMainComponent was null");
                    linkModel = null;
                }
            }

            // Localize the link to the certain place.
            if (linkModel != null)
            {
                MathTransform coordSysTransform =
                    linkModel.Extension.GetCoordinateSystemTransformByName(coordsysName);
                if (coordSysTransform != null)
                {
                    logger.Info("Localizing Link : " + coordsysName);
                    Matrix<double> GlobalTransform = MathOps.GetTransformation(coordSysTransform);
                    LocalizeLink(link, GlobalTransform);
                }
                else
                {
                    logger.Warn("coordSysTransform was null : " + coordsysName);
                }
            }
            else
            { 
                logger.Warn("Link model was null.");
            }
            // === 3dxml Localize Link === //

            bool saved = ActiveDoc.Extension.SaveAs(windowsMeshFilename,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion, saveOptions, null, ref errors, ref warnings);
            EnsureMeshSaved(saved, errors, warnings, link.Name, windowsMeshFilename);
            CommonSwOperations.HideComponents(ActiveSWModel, link.SWComponents);
        }

        private bool SaveSTL(Link link, string windowsMeshFilename)
        {
            int errors = 0;
            int warnings = 0;

            string coordsysName = link.Joint.CoordinateSystemName;

            logger.Info(link.Name + ": Exporting STL with coordinate frame " + coordsysName);

            Dictionary<string, string> names = GetComponentRefGeoNames(coordsysName);
            ModelDoc2 ActiveDoc = ActiveSWModel;

            logger.Info(link.Name + ": Reference geometry name " + names["component"]);

            CommonSwOperations.ShowComponents(ActiveSWModel, link.SWComponents);

            int saveOptions = (int)swSaveAsOptions_e.swSaveAsOptions_Silent |
                (int)swSaveAsOptions_e.swSaveAsOptions_Copy;
            SetLinkSpecificSTLPreferences(names["geo"], link.STLQualityFine, ActiveDoc);

            logger.Info("Saving STL to " + windowsMeshFilename);
            bool saved = ActiveDoc.Extension.SaveAs(windowsMeshFilename,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion, saveOptions, null, ref errors, ref warnings);
            EnsureMeshSaved(saved, errors, warnings, link.Name, windowsMeshFilename);
            CommonSwOperations.HideComponents(ActiveSWModel, link.SWComponents);

            bool success = CorrectSTLMesh(windowsMeshFilename);
            if (!success)
            {
                logger.Warn("There was an issue exporting the STL for " + link.Name + ". It " +
                    "may not be readable by CAD programs that aren't SolidWorks");
            }
            return success;
        }

        /// <summary>
        /// Exports a single part as a URDF package (used in SolidWorks part mode).
        ///
        /// Unlike ExportRobot() (assembly mode), this method handles only a single base link
        /// without creating child links or joint trees. The generated package structure is
        /// consistent with the assembly export, using ROS2 package.xml (not legacy manifest.xml).
        /// </summary>
        /// <param name="zIsUp">Whether Z is up (true applies a coordinate rotation for ROS convention)</param>
        public bool ExportLink(bool zIsUp)
        {
            Robot sourceRobot = URDFRobot;
            bool exportSucceeded = false;
            bool preferencesSaved = false;
            PackageExportTransaction transaction = null;
            try
            {
                ExportValidation.Validate(URDFRobot, PackageName, SavePath);
                URDFRobot = CreateExportCopy(sourceRobot);
                CreateBaseRefOrigin(zIsUp);
                MathTransform coordSysTransform =
                    ActiveSWModel.Extension.GetCoordinateSystemTransformByName("Origin_global");
                if (coordSysTransform == null)
                {
                    throw new InvalidOperationException("Could not create the part export coordinate system.");
                }
                LocalizeLink(URDFRobot.BaseLink, MathOps.GetTransformation(coordSysTransform));

                transaction = new PackageExportTransaction(PackageName, SavePath);
                URDFPackage package = transaction.Package;
                package.CreatePackageFiles(URDFRobot);
                Link link = URDFRobot.BaseLink;
                string meshFileName = package.GetMeshPackageUri(link.Name, ".STL");
                string windowsMeshFileName = package.GetWindowsMeshPath(link.Name, ".STL");

                SaveUserPreferences();
                preferencesSaved = true;
                SetSTLExportPreferences();
                SetLinkSpecificSTLPreferences("", link.STLQualityFine, ActiveSWModel);
                int errors = 0;
                int warnings = 0;
                bool saved = ActiveSWModel.Extension.SaveAs(windowsMeshFileName,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent |
                    (int)swSaveAsOptions_e.swSaveAsOptions_Copy, null, ref errors, ref warnings);
                EnsureMeshSaved(saved, errors, warnings, link.Name, windowsMeshFileName);
                if (!CorrectSTLMesh(windowsMeshFileName))
                {
                    throw new IOException("Could not prepare the STL mesh for link " + link.Name);
                }

                link.Visual.Geometry.Mesh.Filename = meshFileName;
                link.Collision.Geometry.Mesh.Filename = meshFileName;
                CopyTexture(link, package);
                WriteRobotFile(package);
                transaction.Commit();
                exportSucceeded = true;
                URDFPackage finalPackage = new URDFPackage(PackageName, SavePath);
                logger.Info("URDF part export completed: " + finalPackage.WindowsPackageDirectory);
                TryCopyLogFile(finalPackage);
                return true;
            }
            catch (Exception e)
            {
                ReportExportFailure(e);
                return false;
            }
            finally
            {
                if (!exportSucceeded)
                {
                    URDFRobot = sourceRobot;
                }
                RestoreExportState(preferencesSaved, false, null, false);
                CleanupExportTransaction(transaction);
            }
        }

        private Robot CreateExportCopy(Robot source)
        {
            Robot copy = new Robot { Name = PackageName };
            copy.SetBaseLink(source.BaseLink.Clone());
            return copy;
        }

        private static void EnsureMeshSaved(bool saved, int errors, int warnings, string linkName, string filename)
        {
            if (!saved || errors != 0 || !File.Exists(filename) || new FileInfo(filename).Length == 0)
            {
                throw new IOException("Mesh export failed for link " + linkName +
                    " (SolidWorks error " + errors + ", warnings " + warnings + "): " + filename);
            }
            if (warnings != 0)
            {
                logger.Warn("Mesh export warnings for link " + linkName + ": " + warnings);
            }
        }

        private static void CopyTexture(Link link, URDFPackage package)
        {
            var texture = link.Visual.Material.Texture;
            if (string.IsNullOrWhiteSpace(texture.wFilename))
            {
                texture.Filename = null;
                return;
            }
            string textureName = Path.GetFileName(texture.wFilename);
            string destination = Path.Combine(package.WindowsTexturesDirectory, textureName);
            if (!string.Equals(Path.GetFullPath(texture.wFilename), Path.GetFullPath(destination),
                StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(texture.wFilename, destination, true);
            }
            texture.Filename = package.TexturesDirectory + textureName;
        }

        private void WriteRobotFile(URDFPackage package)
        {
            // Localization can change numeric values after the initial preflight.
            ExportValidation.Validate(URDFRobot, PackageName, SavePath);
            URDFWriter writer = new URDFWriter(
                Path.Combine(package.WindowsRobotsDirectory, URDFRobot.Name + ".urdf"));
            using (writer.writer)
            {
                URDFRobot.WriteURDF(writer.writer);
            }
        }

        private static void ReportExportFailure(Exception error)
        {
            logger.Error("URDF export failed", error);
            MessageBox.Show("URDF export failed:\n" + error.Message +
                "\nLog file: " + Logger.GetFileName());
        }

        private static void CleanupExportTransaction(PackageExportTransaction transaction)
        {
            if (transaction == null)
            {
                return;
            }
            transaction.Dispose();
            if (!String.IsNullOrEmpty(transaction.CleanupWarning))
            {
                logger.Warn(transaction.CleanupWarning);
                MessageBox.Show(transaction.CleanupWarning);
            }
        }

        private void RestoreExportState(bool preferencesSaved, bool visibilityChanged,
            List<string> hiddenComponents, bool progressStarted)
        {
            List<string> failures = new List<string>();
            if (visibilityChanged)
            {
                TryRestore(() => CommonSwOperations.ShowAllComponents(ActiveSWModel, hiddenComponents),
                    "component visibility", failures);
            }
            if (preferencesSaved)
            {
                TryRestore(ResetUserPreferences, "STL export settings", failures);
            }
            if (progressStarted)
            {
                TryRestore(() => progressBar.End(), "progress bar", failures);
            }
            if (failures.Count > 0)
            {
                MessageBox.Show("Could not restore the following SolidWorks state:\n" +
                    string.Join("\n", failures) + "\nLog file: " + Logger.GetFileName());
            }
        }

        private static void TryRestore(Action restore, string description, List<string> failures)
        {
            try
            {
                restore();
            }
            catch (Exception e)
            {
                logger.Error("Could not restore " + description, e);
                failures.Add(description);
            }
        }

        private static void TryCopyLogFile(URDFPackage package)
        {
            try
            {
                CopyLogFile(package);
            }
            catch (Exception e)
            {
                logger.Warn("URDF export completed, but the log file could not be copied", e);
                MessageBox.Show("URDF export completed, but the log file could not be copied.\n" + e.Message);
            }
        }

        //Writes an empty header to the STL to get rid of the BS that SolidWorks adds to a binary STL file
        public static bool CorrectSTLMesh(string filename)
        {
            logger.Info("Removing SW header in STL file");
            try
            {
                using (FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    byte[] emptyHeader = new byte[80];
                    fileStream.Write(emptyHeader, 0, emptyHeader.Length);
                }
            }
            catch (Exception e)
            {
                logger.Warn("Correcting the STL " + filename + " failed. This STL may not be " +
                    "readable by ROS or other CAD programs", e);
                return false;
            }
            return true;
        }

        #endregion Export Methods

        private static void CopyLogFile(URDFPackage package)
        {
            string destination = package.WindowsPackageDirectory + "export.log";
            string log_filename = Logger.GetFileName();

            if (log_filename != null)
            {
                if (!File.Exists(log_filename))
                {
                    throw new FileNotFoundException("The export log file could not be found.", log_filename);
                }
                else
                {
                    logger.Info("Copying " + log_filename + " to " + destination);
                    File.Copy(log_filename, destination, true);
                }
            }
        }

        #region STL Preference shuffling

        //Saves the preferences that the user had setup so that I can change them and revert back to their configuration
        private void SaveUserPreferences()
        {
            logger.Info("Saving users preferences");
            mBinary = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat);
            mTranslateToPositive = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive);
            mSTLUnits = iSwApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits);
            mSTLQuality = iSwApp.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality);
            mshowInfo = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave);
            mSTLPreview = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview);
            mHideTransitionSpeed = iSwApp.GetUserPreferenceDoubleValue((int)swUserPreferenceDoubleValue_e.swViewTransitionHideShowComponent);
            mSaveComponentsIntoOneFile = iSwApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile);
            mExportCoordinateSystem = ActiveSWModel.Extension.GetUserPreferenceString(
                (int)swUserPreferenceStringValue_e.swFileSaveAsCoordinateSystem,
                (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified);
        }

        //This is how the STL export preferences need to be to properly export
        private void SetSTLExportPreferences()
        {
            logger.Info("Setting STL preferences");
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, true);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive, true);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, 2);
            iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Coarse);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, false);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, false);
            iSwApp.SetUserPreferenceDoubleValue((int)swUserPreferenceDoubleValue_e.swViewTransitionHideShowComponent, 0);
            iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile, true);
        }

        //This resets the user preferences back to what they were.
        private void ResetUserPreferences()
        {
            logger.Info("Returning STL preferences to user preferences");
            List<string> failures = new List<string>();
            RestorePreference(() => iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, mBinary),
                "binary STL format", failures);
            RestorePreference(() => iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive, mTranslateToPositive),
                "STL translation", failures);
            RestorePreference(() => iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, mSTLUnits),
                "STL units", failures);
            RestorePreference(() => iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, mSTLQuality),
                "STL quality", failures);
            RestorePreference(() => iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, mshowInfo),
                "STL save information", failures);
            RestorePreference(() => iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, mSTLPreview),
                "STL preview", failures);
            RestorePreference(() => iSwApp.SetUserPreferenceDoubleValue((int)swUserPreferenceDoubleValue_e.swViewTransitionHideShowComponent, mHideTransitionSpeed),
                "component transition speed", failures);
            RestorePreference(() => iSwApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile, mSaveComponentsIntoOneFile),
                "STL component grouping", failures);
            RestorePreference(() => ActiveSWModel.Extension.SetUserPreferenceString(
                (int)swUserPreferenceStringValue_e.swFileSaveAsCoordinateSystem,
                (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, mExportCoordinateSystem),
                "export coordinate system", failures);
            if (failures.Count > 0)
            {
                throw new InvalidOperationException("Could not restore: " + string.Join(", ", failures));
            }
        }

        private static void RestorePreference(Func<bool> restore, string description, List<string> failures)
        {
            TryRestore(() =>
            {
                if (!restore())
                {
                    throw new InvalidOperationException("SolidWorks rejected the restored preference.");
                }
            }, description, failures);
        }

        //If the user selected something specific for a particular link, that is handled here.
        private void SetLinkSpecificSTLPreferences(string CoordinateSystemName, bool qualityFine, ModelDoc2 doc)
        {
            doc.Extension.SetUserPreferenceString((int)swUserPreferenceStringValue_e.swFileSaveAsCoordinateSystem,
                (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, CoordinateSystemName);
            if (qualityFine)
            {
                iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Fine);
            }
            else
            {
                iSwApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, (int)swSTLQuality_e.swSTLQuality_Coarse);
            }
        }

        #endregion STL Preference shuffling
    }
}
