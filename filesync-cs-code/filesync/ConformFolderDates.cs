using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace filesync
{
    public class ConformFolderDates
    {
        public static void Conform(string root, Dictionary<string, string> options)
        {
            bool write = options["write"] == "on";
            bool echowrite = options.ContainsKey("echo") && options["echo"].Split(',').Contains("w");
            ConformFolder(root, write, echowrite);
        }

        static DateTime? ConformFolder(string path, bool write, bool echowrite)
        {
            DateTime? maxModified = null;

            // recurse first - bottom up
            foreach (var subDir in Directory.GetDirectories(path))
            {
                var child = ConformFolder(subDir, write, echowrite);
                if (child == null) continue;
                if (maxModified == null || child.Value > maxModified) maxModified = child.Value;
            }

            // factor in direct files
            foreach (var file in Directory.GetFiles(path))
            {
                var fi = new FileInfo(file);
                if (maxModified == null || fi.LastWriteTimeUtc > maxModified) maxModified = fi.LastWriteTimeUtc;
            }

            // no files anywhere in this subtree - ignore entirely
            if (maxModified == null)
                return null;

            var dir = new DirectoryInfo(path);
            bool modifiedChanged = Math.Abs((dir.LastWriteTimeUtc - maxModified.Value).TotalSeconds) > 5;

            if (modifiedChanged)
            {
                if (write)
                {
                    try
                    {
                        dir.LastWriteTimeUtc = maxModified.Value;
                        if (echowrite)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"set modified:          {maxModified.Value:yyyy-MM-dd} {path}");
                        }
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"UNABLE TO set modified: {maxModified.Value:yyyy-MM-dd} {path}");
                    }
                }
                else if (echowrite)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"would set modified:    {maxModified.Value:yyyy-MM-dd} {path}");
                }
            }

            return maxModified;
        }
    }
}