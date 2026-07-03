namespace SW2URDF.URDF
{
    /// <summary>
    /// Lightweight handle for a SolidWorks assembly component.
    /// Provides the component name and persistence key without requiring
    /// a live COM object. The underlying COM Component2 can be obtained
    /// lazily via <see cref="ComponentHandle.GetCOMObject"/> when needed.
    /// </summary>
    public interface IComponentHandle
    {
        /// <summary>The display name of the component in the SolidWorks feature tree.</summary>
        string Name { get; }

        /// <summary>
        /// A byte array that uniquely identifies this component across sessions
        /// (SolidWorks Persist Reference). Null if the component has not been persisted.
        /// </summary>
        byte[] PersistenceKey { get; }
    }
}
