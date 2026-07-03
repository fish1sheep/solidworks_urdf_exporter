using SolidWorks.Interop.sldworks;

namespace SW2URDF.URDF
{
    /// <summary>
    /// Wraps a SolidWorks <see cref="Component2"/> COM object, providing
    /// access to the component name and persistence key without requiring
    /// a live COM reference for basic metadata. The underlying COM object
    /// can be retrieved lazily when needed.
    /// </summary>
    public class ComponentHandle : IComponentHandle
    {
        private Component2 comObject;

        /// <summary>
        /// The display name of the component in the SolidWorks feature tree.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// A byte array that uniquely identifies this component across sessions.
        /// May be null if the component has not been persisted.
        /// </summary>
        public byte[] PersistenceKey { get; private set; }

        /// <summary>
        /// Creates a ComponentHandle from a live COM object.
        /// </summary>
        public ComponentHandle(Component2 component, byte[] persistenceKey = null)
        {
            comObject = component;
            Name = component?.Name2;
            PersistenceKey = persistenceKey;
        }

        /// <summary>
        /// Creates a ComponentHandle from a persistence key only (lazy loading).
        /// Name is set later when the COM object is resolved.
        /// </summary>
        public ComponentHandle(byte[] persistenceKey)
        {
            PersistenceKey = persistenceKey;
        }

        /// <summary>
        /// Creates a ComponentHandle with name only (for disconnected use).
        /// </summary>
        public ComponentHandle(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Returns the underlying COM Component2 object. If the object has not been
        /// loaded yet, resolves it from the persistence key using the provided model.
        /// Returns null if the resolution fails.
        /// </summary>
        /// <param name="model">The SolidWorks ModelDoc2 to resolve the component from.</param>
        public Component2 GetCOMObject(ModelDoc2 model)
        {
            if (comObject != null)
            {
                return comObject;
            }
            if (PersistenceKey != null && model != null)
            {
                comObject = model.Extension.GetObjectByPersistReference3(
                    PersistenceKey, out int errors) as Component2;
                if (comObject != null)
                {
                    Name = comObject.Name2;
                }
            }
            return comObject;
        }

        /// <summary>
        /// Lazily resolves the COM object using the provided model, if not already loaded.
        /// </summary>
        public void EnsureLoaded(ModelDoc2 model)
        {
            if (comObject == null && PersistenceKey != null && model != null)
            {
                GetCOMObject(model);
            }
        }

        /// <summary>
        /// Clears the cached COM object reference (e.g., when the model is closed).
        /// The component can be re-resolved later via <see cref="GetCOMObject"/>.
        /// </summary>
        public void ReleaseCOMObject()
        {
            comObject = null;
        }
    }
}
