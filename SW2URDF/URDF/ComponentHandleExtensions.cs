using SolidWorks.Interop.sldworks;
using System.Collections.Generic;
using System.Linq;

namespace SW2URDF.URDF
{
    /// <summary>
    /// Extension methods for working with <see cref="IComponentHandle"/> collections.
    /// Provides helpers to convert handles back to COM <see cref="Component2"/> objects
    /// when direct SolidWorks API access is needed.
    /// </summary>
    public static class ComponentHandleExtensions
    {
        /// <summary>
        /// Lazily resolves all component handles to their COM Component2 objects.
        /// </summary>
        /// <param name="handles">The collection of component handles.</param>
        /// <param name="model">The SolidWorks model to resolve components from.</param>
        /// <returns>List of resolved Component2 objects (null entries for failed resolutions).</returns>
        public static List<Component2> ToCOMList(this IEnumerable<IComponentHandle> handles, ModelDoc2 model)
        {
            if (handles == null || model == null)
            {
                return new List<Component2>();
            }

            return handles
                .Select(h => (h as ComponentHandle)?.GetCOMObject(model))
                .Where(c => c != null)
                .ToList();
        }

        /// <summary>
        /// Extracts component names from a collection of handles.
        /// </summary>
        public static List<string> GetNames(this IEnumerable<IComponentHandle> handles)
        {
            if (handles == null)
            {
                return new List<string>();
            }
            return handles.Select(h => h.Name).ToList();
        }
    }
}
