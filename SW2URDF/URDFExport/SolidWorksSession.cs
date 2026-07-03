using log4net;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SW2URDF.Utilities;

namespace SW2URDF.URDFExport
{
    /// <summary>
    /// Concrete implementation of <see cref="ISolidWorksSession"/> that delegates
    /// all calls to a live SolidWorks COM application instance.
    /// </summary>
    public class SolidWorksSession : ISolidWorksSession
    {
        private static readonly ILog logger = Logger.GetLogger();

        public SldWorks Application { get; private set; }
        public ModelDoc2 ActiveDocument { get; private set; }

        public SolidWorksSession(SldWorks swApp)
        {
            Application = swApp;
            ActiveDocument = (ModelDoc2)swApp.ActiveDoc;
        }

        // ==================== STL Preferences ====================

        public bool GetPreferenceToggle(int preferenceId)
        {
            return Application.GetUserPreferenceToggle(preferenceId);
        }

        public void SetPreferenceToggle(int preferenceId, bool value)
        {
            Application.SetUserPreferenceToggle(preferenceId, value);
        }

        public int GetPreferenceIntegerValue(int preferenceId)
        {
            return Application.GetUserPreferenceIntegerValue(preferenceId);
        }

        public void SetPreferenceIntegerValue(int preferenceId, int value)
        {
            Application.SetUserPreferenceIntegerValue(preferenceId, value);
        }

        public double GetPreferenceDoubleValue(int preferenceId)
        {
            return Application.GetUserPreferenceDoubleValue(preferenceId);
        }

        public void SetPreferenceDoubleValue(int preferenceId, double value)
        {
            Application.SetUserPreferenceDoubleValue(preferenceId, value);
        }

        // ==================== Mesh Export ====================

        public void SetDocumentPreferenceString(ModelDoc2 doc, int preferenceId, string value)
        {
            doc.Extension.SetUserPreferenceString(preferenceId,
                (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified, value);
        }

        public bool SaveDocumentAs(ModelDoc2 doc, string path, int version, int options, ref int errors, ref int warnings)
        {
            return doc.Extension.SaveAs(path, version, options, null, ref errors, ref warnings);
        }

        // ==================== Progress Bar ====================

        public UserProgressBar CreateProgressBar()
        {
            Application.GetUserProgressBar(out UserProgressBar bar);
            return bar;
        }

        public void StartProgressBar(UserProgressBar bar, string title, int totalSteps)
        {
            bar.Start(0, totalSteps, title);
        }

        public void UpdateProgressBar(UserProgressBar bar, int step)
        {
            bar.UpdateProgress(step);
        }

        public void EndProgressBar(UserProgressBar bar)
        {
            bar.End();
        }
    }
}
