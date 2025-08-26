using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public static class IEnumerableEx
    {
        public static async Task<bool> AnyAsync<T>(this IEnumerable<T> enumerable, Func<T, Task<bool>> predicate)
        {
            foreach (var item in enumerable)
            {
                if (await predicate(item))
                {
                    return true;
                }
            }
            return false;
        }

        public static async Task<bool> AllAsync<T>(this IEnumerable<T> enumerable, Func<T, Task<bool>> predicate)
        {
            foreach (var item in enumerable)
            {
                if (!await predicate(item))
                {
                    return false;
                }
            }
            return true;
        }

        public static async Task<IEnumerable<T>> WhereAsync<T>(this IEnumerable<T> enumerable, Func<T, Task<bool>> predicate)
        {
            List<T> result = new List<T>();
            foreach (var item in enumerable)
            {
                if (await predicate(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        public static async Task WhenAll<T>(this IEnumerable<Task<T>> tasks)
        {
            Task<T>[] tsks = null;
            try
            {
                tsks = tasks.ToArray();
                await Task.WhenAll(tsks);
            }
            catch
            {
                var errors = tsks.Where(t => t.IsFaulted).Select(t => t.Exception.InnerException);
                throw new AggregateException(errors);
            }
        }

        public static async Task WhenAll(this IEnumerable<Task> tasks, bool executeInParallel = true)
        {
            if (executeInParallel)
            {
                Task[] tsks = null;
                try
                {
                    tsks = tasks.ToArray();
                    await Task.WhenAll(tsks);
                }
                catch
                {
                    var errors = tsks.Where(t => t.IsFaulted).Select(t => t.Exception.InnerException);
                    throw new AggregateException(errors);
                }
            }
            else
            {
                foreach (var task in tasks)
                {
                    await task;
                }
            }
        }
    }
}
