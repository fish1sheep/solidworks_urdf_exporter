using System;

namespace SW2URDF.URDF
{
    /// <summary>
    /// Base exception for URDF model serialization errors.
    /// </summary>
    public class URDFException : Exception
    {
        public URDFException() { }
        public URDFException(string message) : base(message) { }
        public URDFException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when a required attribute has a null value or invalid type.
    /// </summary>
    public class URDFAttributeException : URDFException
    {
        public URDFAttributeException() { }
        public URDFAttributeException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when URDF element fields are not satisfied before export.
    /// </summary>
    public class URDFElementValidationException : URDFException
    {
        public URDFElementValidationException() { }
        public URDFElementValidationException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when CSV parsing fails.
    /// </summary>
    public class URDFCSVParseException : URDFException
    {
        public URDFCSVParseException() { }
        public URDFCSVParseException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when a configuration/corruption issue prevents loading components from PIDs.
    /// </summary>
    public class URDFConfigurationException : URDFException
    {
        public URDFConfigurationException() { }
        public URDFConfigurationException(string message) : base(message) { }
    }
}
