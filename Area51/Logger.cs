using System;
using System.Collections.Generic;

namespace Area51
{
    public static class Logger
    {
        private static List<string> lines;
        private static object @lock;

        static Logger()
        {
            lines = new List<string>();
            @lock = new object();
        }

        public static void WriteLine(string text)
        {
            lock (@lock)
            {
                lines.Add(text);
            }
        }

        public static List<string> GetLines()
        {
            lock (@lock)
            {
                return new List<string>(lines);
            }
        }
    }
}
