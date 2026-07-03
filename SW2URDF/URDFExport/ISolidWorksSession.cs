using SolidWorks.Interop.sldworks;

namespace SW2URDF.URDFExport
{
    /// <summary>
    /// Abstracts SolidWorks application-level operations that the export pipeline depends on.
    /// Inputs and outputs use simple value types (bool, int, double, string) wherever possible,
    /// to enable unit testing of the export logic without a running SolidWorks instance.
    /// </summary>
    public interface ISolidWorksSession
    {
        /// <summary>The underlying SolidWorks application object. Used for direct COM access when needed.</summary>
        SldWorks Application { get; }

        /// <summary>The active document in the SolidWorks session.</summary>
        ModelDoc2 ActiveDocument { get; }

        // ==================== STL Preferences ====================

        /// <summary>Gets a boolean SolidWorks user preference value.</summary>
        bool GetPreferenceToggle(int preferenceId);

        /// <summary>Sets a boolean SolidWorks user preference value.</summary>
        void SetPreferenceToggle(int preferenceId, bool value);

        /// <summary>Gets an integer SolidWorks user preference value.</summary>
        int GetPreferenceIntegerValue(int preferenceId);

        /// <summary>Sets an integer SolidWorks user preference value.</summary>
        void SetPreferenceIntegerValue(int preferenceId, int value);

        /// <summary>Gets a double SolidWorks user preference value.</summary>
        double GetPreferenceDoubleValue(int preferenceId);

        /// <summary>Sets a double SolidWorks user preference value.</summary>
        void SetPreferenceDoubleValue(int preferenceId, double value);

        // ==================== Mesh Export ====================

        /// <summary>Sets a string user preference on a specific document (e.g., coordinate system name for STL export).</summary>
        void SetDocumentPreferenceString(ModelDoc2 doc, int preferenceId, string value);

        /// <summary>Saves the active document as a mesh file in the specified format.</summary>
        bool SaveDocumentAs(ModelDoc2 doc, string path, int version, int options, ref int errors, ref int warnings);

        // ==================== Progress Bar ====================

        /// <summary>Creates and shows a progress bar in the SolidWorks status area.</summary>
        UserProgressBar CreateProgressBar();

        /// <summary>Starts the progress bar with a title and total number of steps.</summary>
        void StartProgressBar(UserProgressBar bar, string title, int totalSteps);

        /// <summary>Updates the progress bar to the current step.</summary>
        void UpdateProgressBar(UserProgressBar bar, int step);

        /// <summary>Hides and disposes the progress bar.</summary>
        void EndProgressBar(UserProgressBar bar);
    }
}
