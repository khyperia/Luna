using System.Linq;

namespace Luna
{
    public static class Utils
    {
        public static T[] ConcatArrays<T>(params T[][] list)
        {
            var result = new T[list.Sum(a => a.Length)];
            var offset = 0;
            foreach (var t in list)
            {
                t.CopyTo(result, offset);
                offset += t.Length;
            }
            return result;
        }

        public static T[] Append<T>(this T[] arr, T value)
        {
            var newarr = new T[arr.Length + 1];
            arr.CopyTo(newarr, 0);
            newarr[arr.Length] = value;
            return newarr;
        }
    }
}