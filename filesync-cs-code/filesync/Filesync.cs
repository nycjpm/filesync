using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace filesync
{
    public class Mapping
    {
        public string fromPath { get; set; }
        public string toPath { get; set; }
        public string options { get; set; }

        public override string ToString()
        {
            return this.fromPath + " -> " + this.toPath + (!string.IsNullOrWhiteSpace(this.options) ? " -> " + this.options : string.Empty);
        }
    }

    public class Filesync
    {
        public static void SyncFolders(List<Mapping> mappings, Dictionary<string, string> options)
        {
            foreach(var map in mappings)
            {
                SyncFolder(map, options);
            }
        }

        public static void SyncFolder(Mapping mapping, Dictionary<string, string> options)
        {
            // need MERGE code for files and folers

            // create target if not exists
            Util.ValidatePath(mapping.toPath, true);

            string[] rawf;

            try
            {
                rawf = Directory.GetDirectories(mapping.fromPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("error: " + ex.Message);
                return;
            }

            var fromfolders = rawf
                .Where(f => f != mapping.toPath)
                .Select(f => f.Split('\\').Last())
                .Where(f => f.ToUpper() != "$RECYCLE.BIN")
                .Where(f => f.ToUpper() != "RECYCLER")
                .ToList();

            var tofolders = Directory.GetDirectories(mapping.toPath)
                .Select(f => f.Split('\\').Last())
                .ToList();

            //
            // delete FOLDERS found in to but absent in from
            //
            var deletedfolders = tofolders.Select(f => f.ToLowerInvariant()).Except(fromfolders.Select(f => f.ToLowerInvariant())).ToList();
            if (deletedfolders.Any())
            {
                Parallel.ForEach(deletedfolders, f => DeleteFolder(mapping.toPath + '\\' + f, options));
                // foreach (var f in deletedfolders) DeleteFolder(to + '\\' + f, options);
            }

            //
            // delete FILES found in to but absent in from
            //
            var fromfiles = Directory.GetFiles(mapping.fromPath)
                    .Select(f => f.Split('\\').Last())
                    .ToList();

            var tofiles = Directory.GetFiles(mapping.toPath)
                .Select(f => f.Split('\\').Last())
                .ToList();

            var deletedfiles = tofiles.Select(f => f.ToLowerInvariant()).Except(fromfiles.Select(f => f.ToLowerInvariant())).ToList();
            if (deletedfiles.Any())
            {
                Parallel.ForEach(deletedfiles, f => DeleteFile(mapping.toPath + '\\' + f, options));
                // foreach (var f in deletedfiles) DeleteFile(mapping.toPath + '\\' + f, options);
            }

            //
            // sync FILES
            //
            Parallel.ForEach(fromfiles, f => SyncFile(mapping.fromPath + '\\' + f, mapping.toPath + '\\' + f, options));
            // foreach (var f in fromfiles) SyncFile(mapping.fromPath + '\\' + f, mapping.toPath + '\\' + f, options);

            //
            // recurse for child folders
            //
            var childmaps = fromfolders
                .Select(f => new Mapping
                {
                    fromPath = mapping.fromPath + '\\' + f,
                    toPath = mapping.toPath + '\\' + f
                });

            if (options["hash"] == "true")
            {
                // HASHING, serialize sub directory recursion
                foreach (var m in childmaps) SyncFolder(m, options);
            }
            else
            {
                // not hashing, go full parallel
                Parallel.ForEach(childmaps, m => SyncFolder(m, options));
            }
        }

        public static void DeleteFolder(string to, Dictionary<string, string> options)
        {
            bool echowrite = false;

            if (options.ContainsKey("echo"))
            {
                var vals = options["echo"].Split(',').ToList();
                echowrite = vals.Contains("w");
            }

            if (echowrite)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("deleted folder: " + to);
            }

            double retryms = 5;
            while (retryms < 15000)
            {
                try
                {
                    var dir = new System.IO.DirectoryInfo(to);
                    SetDirAttributesNormal(dir);
                    dir.Delete(true);
                    return;
                }
                catch (Exception ex)
                {
                    Random r = new Random();
                    var inc = (r.NextDouble() + 1) * 10;
                    retryms *= inc;

                    if (retryms < 15000)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"error deleting folder: {to}, retrying in {retryms:#,###} ms");
                        Console.WriteLine($"    {ex.Message}");
                        System.Threading.Thread.Sleep((int)retryms);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"FOLDER NOT DELETED DUE TO ERROR: {to} ");
                        Console.WriteLine($"    {ex.Message}");
                        return;
                    }
                }
            }
        }

        static void DeleteFile(string to, Dictionary<string, string> options)
        {
            bool echowrite = false;

            if (options.ContainsKey("echo"))
            {
                var vals = options["echo"].Split(',').ToList();
                echowrite = vals.Contains("w");
            }

            if (echowrite)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("deleted file: " + to);
            }

            double retryms = 5;
            while (retryms < 15000)
            {
                try
                {
                    File.Delete(to);
                    return;
                }
                catch (Exception ex)
                {
                    Random r = new Random();
                    var inc = (r.NextDouble() + 1) * 10;
                    retryms *= inc;

                    if (retryms < 15000)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"error deleting file: {to}, retrying in {retryms:#,###} ms");
                        Console.WriteLine($"    {ex.Message}");
                        System.Threading.Thread.Sleep((int)retryms);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"FILE NOT DELETED DUE TO ERROR: {to} ");
                        Console.WriteLine($"    {ex.Message}");
                        return;
                    }
                }
            }
        }

        static void SyncFile(string from, string to, Dictionary<string, string> options)
        {
            var t = new System.Diagnostics.Stopwatch();
            t.Start();

            bool echowrite = false;
            bool echoskip = false;

            if (options.ContainsKey("echo"))
            {
                var vals = options["echo"].Split(',').ToList();
                echowrite = vals.Contains("w");
                echoskip = vals.Contains("s");
            }

            bool missing = true;
            if (File.Exists(to))
            {
                missing = false;

                var fif = new FileInfo(from);
                var fit = new FileInfo(to);

                //
                // FAT and exFAT have 2s granularity on Modified dates.  Therefore, need to allow +/- 3s in comparisons.
                // https://superuser.com/questions/1685706/timestamp-changes-when-copying-file-to-exfat-drive
                //
                var different = fif.Length != fit.Length || Math.Abs((fif.LastWriteTime - fit.LastWriteTime).TotalSeconds) > 4;

                if (!different)
                {
                    if (options["hash"] == "false")
                    {
                        if (echoskip)
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine("nohash exists (skipped): " + to + " (" + t.Elapsed.TotalMilliseconds.ToString("0") + " ms)");
                        }
                        return;
                    }

                    var fhash = FileMD5(from);
                    var thash = FileMD5(to);

                    if (fhash == thash)
                    {
                        if (echoskip)
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine("hash match (skipped): " + to + " (" + t.Elapsed.TotalMilliseconds.ToString("0") + " ms)");
                        }
                        return;
                    }
                }
            }

            double retryms = 5;
            while (retryms < 15000)
            {
                try
                {
                    File.Copy(from, to, true);
                    if (echowrite)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine((missing ? "copied: " : "replaced: ") + to + " (" + t.Elapsed.TotalMilliseconds.ToString("0") + " ms)");
                    }

                    return;
                }
                catch (Exception ex)
                {
                    Random r = new Random();
                    var inc = (r.NextDouble() + 1) * 10;
                    retryms *= inc;

                    if (retryms < 15000)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"error copying file: {to}, retrying in {retryms:#,###} ms");
                        Console.WriteLine($"    {ex.Message}");
                        System.Threading.Thread.Sleep((int)retryms);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"FILE NOT COPIED DUE TO ERROR: {to} ");
                        Console.WriteLine($"    {ex.Message}");
                        return;
                    }
                }
            }
        }

        static void SetDirAttributesNormal(DirectoryInfo dir)
        {
            foreach (var subDir in dir.GetDirectories())
                SetDirAttributesNormal(subDir);

            foreach (var file in dir.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
            }

            dir.Attributes = FileAttributes.Normal;
        }

        static string FileMD5(string fileName)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(fileName))
                    {
                        return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error Hashing, returning empty hash for {fileName}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
