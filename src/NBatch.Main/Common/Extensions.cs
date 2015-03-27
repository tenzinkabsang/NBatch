
using System.Collections.Generic;
using System.Linq;

namespace NBatch.Main.Common
{
    public static class Extensions
    {
        public static IList<T> EmptyList<T>()
        {
            return Enumerable.Empty<T>().ToList();
        }
    }
}
