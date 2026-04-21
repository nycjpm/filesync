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
            var proformaTag = options["write"] != "on" ? "would have" : string.Empty;

            // threads options from command line
            var paralleloptions = new ParallelOptions { };
            if (options.ContainsKey("threads"))
            {
                var n = int.Parse(options["threads"]);
                paralleloptions.MaxDegreeOfParallelism = n;
            }

            // need MERGE code for files and folers

            // create target if not exists (only create in write mode)
            Util.ValidatePath(mapping.toPath, options["write"] == "on");

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
                Parallel.ForEach(deletedfolders, paralleloptions, f => DeleteFolder(mapping.toPath + '\\' + f, options));
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
                Parallel.ForEach(deletedfiles, paralleloptions, f => DeleteFile(mapping.toPath + '\\' + f, options));
                // foreach (var f in deletedfiles) DeleteFile(mapping.toPath + '\\' + f, options);
            }

            //
            // sync FILES
            //
            Parallel.ForEach(fromfiles, paralleloptions, f => SyncFile(mapping.fromPath + '\\' + f, mapping.toPath + '\\' + f, options));
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
                Parallel.ForEach(childmaps, paralleloptions, m => SyncFolder(m, options));
            }

            // set folder timestamps
            try
            {
                var fromDir = new DirectoryInfo(mapping.fromPath);
                var toDir = new DirectoryInfo(mapping.toPath);
                if (Math.Abs((fromDir.LastWriteTimeUtc - toDir.LastWriteTimeUtc).TotalSeconds) > 5)
                {
                    if (options["write"] == "on")
                    {
                        toDir.LastWriteTimeUtc = fromDir.LastWriteTimeUtc;
                    }

                    if (options.ContainsKey("echo") && options["echo"].Split(',').Contains("w"))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"folder timestamp {proformaTag} updated {fromDir.LastWriteTimeUtc.ToString()}: " + mapping.toPath);
                    }
                }
            }
            catch { } // non-fatal, best effort
        }

        public static void DeleteFolder(string to, Dictionary<string, string> options)
        {
            var proformaTag = options["write"] != "on" ? "would have" : string.Empty;

            bool echowrite = false;

            if (options.ContainsKey("echo"))
            {
                var vals = options["echo"].Split(',').ToList();
                echowrite = vals.Contains("w");
            }

            if (echowrite)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"{proformaTag} deleted folder: " + to);
            }

            double retryms = 5;
            while (retryms < 15000)
            {
                try
                {
                    if (options["write"] == "on")
                    {
                        var dir = new System.IO.DirectoryInfo(to);
                        SetDirAttributesNormal(dir);
                        dir.Delete(true);
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
            var proformaTag = options["write"] != "on" ? "would have" : string.Empty;

            bool echowrite = false;

            if (options.ContainsKey("echo"))
            {
                var vals = options["echo"].Split(',').ToList();
                echowrite = vals.Contains("w");
            }

            if (echowrite)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"{proformaTag} deleted file: " + to);
            }

            double retryms = 5;
            while (retryms < 15000)
            {
                try
                {
                    if (options["write"] == "on")
                    {
                        File.Delete(to);
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
            var proformaTag = options["write"] != "on" ? "would have" : string.Empty;
                
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
                // FAT and exFAT have 2s granularity on Modified dates.  Therefore, need to allow +/- 5s in comparisons.
                // https://superuser.com/questions/1685706/timestamp-changes-when-copying-file-to-exfat-drive
                //
                var different = fif.Length != fit.Length || Math.Abs((fif.LastWriteTimeUtc - fit.LastWriteTimeUtc).TotalSeconds) > 5;

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
                // if not retrying then suppress filesystem errors, dont sleep and dont retry
                if (options["retries"] != "on")
                    retryms = 15000;

                try
                {
                    if (options["write"] == "on")
                    {
                        File.Copy(from, to, true);
                    }

                    if (echowrite)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine((missing ? $"{proformaTag} copied: " : $"{proformaTag} replaced: ") + to + " (" + t.Elapsed.TotalMilliseconds.ToString("0") + " ms)");
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
                        // make sure target didnt fail because of readonly bullshit
                        if (File.Exists(to))
                            File.SetAttributes(to, FileAttributes.Normal);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"error copying file: {to}, retrying in {retryms:#,###} ms");
                        Console.WriteLine($"    {ex.Message}");
                        System.Threading.Thread.Sleep((int)retryms);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"FILE NOT COPIED DUE TO ERROR: {from} ");
                        // Console.WriteLine($"    {ex.Message}");
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
