using System;
using System.Collections.Generic;

namespace Area51
{
    public static class Logger
    {
        public static List<string> lines;

        static Logger()
        {
            lines = new List<string>();
        }

        public static void WriteLine(string text)
        {
            lines.Add(text);
        }
    }
}
