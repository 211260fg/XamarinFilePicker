using System;

namespace FolderPicker
{
    public static class WeakReferenceExtensions
    {
        public static T TryGetTarget<T>(this WeakReference<T> weakReference) where T : class 
        {
            T result = null;
            weakReference?.TryGetTarget(out result);
            return result;
        }
    }
}