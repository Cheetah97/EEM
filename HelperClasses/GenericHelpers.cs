using System;
using System.Collections.Generic;
using System.Linq;

namespace EEM.HelperClasses
{
    /// <summary>
    /// Provides a set of methods to fix some of the LINQ idiocy.
    /// <para/>
    /// Enjoy your allocations.
    /// </summary>
    public static class GenericHelpers
    {
        public static List<T> Except<T>(this List<T> Source, Func<T, bool> Sorter)
        {
            return Source.Where(x => !Sorter(x)).ToList();
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> Source)
        {
            HashSet<T> Hashset = new HashSet<T>();
            foreach (T item in Source)
                Hashset.Add(item);
            return Hashset;
        }

        /// <summary>
        /// Returns a list with one item excluded.
        /// </summary>
        public static List<T> Except<T>(this List<T> Source, T exclude)
        {
            return Source.Where(x => !x.Equals(exclude)).ToList();
        }

        public static bool Any<T>(this IEnumerable<T> Source, Func<T, bool> Sorter, out IEnumerable<T> Any)
        {
            Any = Source.Where(Sorter);
            return Any.Count() > 0;
        }

        /// <summary>
        /// Determines if the sequence has no elements matching a given predicate.
        /// <para />
        /// Basically, it's an inverted Any().
        /// </summary>
        public static bool None<T>(this IEnumerable<T> Source, Func<T, bool> Sorter)
        {
            return !Source.Any(Sorter);
        }

        public static IEnumerable<T> Unfitting<T>(this IEnumerable<T> Source, Func<T, bool> Sorter)
        {
            return Source.Where(x => Sorter(x) == false);
        }

        public static List<T> Unfitting<T>(this List<T> Source, Func<T, bool> Sorter)
        {
            return Source.Where(x => Sorter(x) == false).ToList();
        }

        public static bool Any<T>(this List<T> Source, Func<T, bool> Sorter, out List<T> Any)
        {
            Any = Source.Where(Sorter).ToList();
            return Any.Count > 0;
        }

        public static bool Empty<T>(this IEnumerable<T> Source)
        {
            return Source.Count() == 0;
        }
    }
}