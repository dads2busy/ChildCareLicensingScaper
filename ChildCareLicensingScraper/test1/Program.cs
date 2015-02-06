using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace test1
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(DBStringOccurenceCount("needle", "a needle a needle another needle"));
        }

        public static int DBStringOccurenceCount(string needle, string haystack)
        {
            if (haystack == null)
                haystack = "";
            int needleCount = new Regex(needle).Matches(haystack).Count;
            if (needleCount > 0)
                return needleCount;
            else
                return 0;
        }
    }
}
