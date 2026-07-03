using log4net;
using SolidWorks.Interop.swconst;
using SW2URDF.Utilities;

namespace SW2URDF.URDFExport
{
    /// <summary>
    /// Manages SolidWorks STL export preference save/set/restore lifecycle.
    /// The preferences are pure value types (bool, int, double), so this class
    /// can be tested by injecting a mock <see cref="ISolidWorksSession"/>.
    /// </summary>
    public class STLPreferenceManager
    {
        private static readonly ILog logger = Logger.GetLogger();

        private readonly ISolidWorksSession session;
        private bool mBinary;
        private bool mTranslateToPositive;
        private int mSTLUnits;
        private int mSTLQuality;
        private bool mshowInfo;
        private bool mSTLPreview;
        private double mHideTransitionSpeed;
        private bool mSaveComponentsIntoOneFile;

        public STLPreferenceManager(ISolidWorksSession session)
        {
            this.session = session;
        }

        /// <summary>
        /// Saves the user's current STL export preferences into internal fields.
        /// Call before modifying preferences for export.
        /// </summary>
        public void SaveUserPreferences()
        {
            logger.Info("Saving users preferences");
            mBinary = session.GetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat);
            mTranslateToPositive = session.GetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive);
            mSTLUnits = session.GetPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits);
            mSTLQuality = session.GetPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality);
            mshowInfo = session.GetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave);
            mSTLPreview = session.GetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview);
            mHideTransitionSpeed = session.GetPreferenceDoubleValue(
                (int)swUserPreferenceDoubleValue_e.swViewTransitionHideShowComponent);
            mSaveComponentsIntoOneFile = session.GetPreferenceToggle(
                (int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile);
        }

        /// <summary>
        /// Sets STL preferences to the values required for successful URDF mesh export.
        /// </summary>
        public void SetExportPreferences()
        {
            logger.Info("Setting STL preferences");
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, true);
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive, true);
            session.SetPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, 2);
            session.SetPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality,
                (int)swSTLQuality_e.swSTLQuality_Coarse);
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, false);
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, false);
            session.SetPreferenceDoubleValue(
                (int)swUserPreferenceDoubleValue_e.swViewTransitionHideShowComponent, 0);
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile, true);
        }

        /// <summary>
        /// Restores the user's original STL preferences (as saved by <see cref="SaveUserPreferences"/>).
        /// </summary>
        public void RestoreUserPreferences()
        {
            logger.Info("Returning STL preferences to user preferences");
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat, mBinary);
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive, mTranslateToPositive);
            session.SetPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swExportStlUnits, mSTLUnits);
            session.SetPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSTLQuality, mSTLQuality);
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave, mshowInfo);
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, mSTLPreview);
            session.SetPreferenceDoubleValue(
                (int)swUserPreferenceDoubleValue_e.swViewTransitionHideShowComponent, mHideTransitionSpeed);
            session.SetPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile, mSaveComponentsIntoOneFile);
        }
    }
}
