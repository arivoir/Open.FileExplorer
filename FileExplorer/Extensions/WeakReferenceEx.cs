using System;

namespace Open.FileExplorer
{
    public static class WeakReferenceEx
    {
        public static T GetTarget<T>(this WeakReference<T> reference) where T : class
        {
            T result;
            if (reference != null && reference.TryGetTarget(out result))
            {
                return result;
            }
            return default(T);
        }
    }
}
