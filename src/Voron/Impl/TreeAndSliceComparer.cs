using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Voron.Data.BTrees;

namespace Voron.Impl
{
    internal struct TreeAndSliceComparer : IEqualityComparer<Tuple<Tree, Slice>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Tuple<Tree, Slice> x, Tuple<Tree, Slice> y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;

            if (x.Item1 != y.Item1)
                return false;

            return SliceComparer.Equals(x.Item2, y.Item2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(Tuple<Tree, Slice> obj)
        {
            return obj.Item1.GetHashCode() ^ 397 * obj.Item2.GetHashCode();
        }
    }
}
