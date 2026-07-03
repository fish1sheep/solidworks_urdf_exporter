using System;

namespace SW2URDF.URDFExport
{
    /// <summary>
    /// Base exception for all export pipeline errors.
    /// </summary>
    public class ExportException : Exception
    {
        public ExportException() { }
        public ExportException(string message) : base(message) { }
        public ExportException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when a reference geometry element (coordinate system, axis, sketch) is missing or invalid.
    /// </summary>
    public class ReferenceGeometryException : ExportException
    {
        public ReferenceGeometryException() { }
        public ReferenceGeometryException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when mass property computation fails (e.g., failed to add bodies).
    /// </summary>
    public class MassPropertyException : ExportException
    {
        public MassPropertyException() { }
        public MassPropertyException(string message) : base(message) { }
    }
}
