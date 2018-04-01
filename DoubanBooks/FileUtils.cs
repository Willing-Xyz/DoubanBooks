using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoubanBooks
{
    public class FileUtils
    {
        public static string ReadCommentJson(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var strBuilder = new StringBuilder();
            foreach (var line in lines)
            {
                if (line.TrimStart().FirstOrDefault() == '#')
                    continue;
                strBuilder.Append(line);
            }
            return strBuilder.ToString();
        }
    }
}
