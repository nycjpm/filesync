using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace filesync
{
    class Shell
    {
        static Dictionary<string, string> inputoptions;

        static HashSet<string> validOptions = new HashSet<string>("from,to,hash,echo,prompt,op,manifest".Split(','));

        //static string slug = "asof-";

        static void Main(string[] args)
        {
            try
            {
                inputoptions = Util.ParseArgs(args);
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Filesync ERROR PARSING ARGUMENTS");
                Shell.Usage();
                return;
            }

            foreach (var k in inputoptions.Keys)
            {
                if (!validOptions.Contains(k.Trim().ToLower()))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Filesync ERROR -> INVALID OPTION: " + k);
                    Console.ForegroundColor = ConsoleColor.White;
                    Usage();
                    return;
                }
            }

            if (!inputoptions.ContainsKey("from") || !inputoptions.ContainsKey("to"))
            {
                if (!inputoptions.ContainsKey("manifest"))
                {
                    inputoptions["op"] = "readme";
                    inputoptions["prompt"] = "true";
                }
            }

            //
            // set DEFAULT behaviors
            //
            if (!inputoptions.ContainsKey("hash"))
            {
                inputoptions["hash"] = "false";
            }

            if (!inputoptions.ContainsKey("prompt"))
            {
                inputoptions["prompt"] = "true";
            }

            if (!inputoptions.ContainsKey("echo"))
            {
                inputoptions["echo"] = "w";
            }

            if (!inputoptions.ContainsKey("op"))
            {
                inputoptions["op"] = "sync";
            }

            if (!inputoptions.ContainsKey("retain"))
            {
                inputoptions["retain"] = "m6,q8,y99";
            }

            if (!inputoptions.ContainsKey("versions"))
            {
                inputoptions["versions"] = "false";
            }

            if (!inputoptions.ContainsKey("slug"))
            {
                inputoptions["slug"] = "asof-";
            }

            // conform case and trim
            inputoptions["hash"] = inputoptions["hash"].ToLower().Trim();
            inputoptions["prompt"] = inputoptions["prompt"].ToLower().Trim();
            inputoptions["echo"] = inputoptions["echo"].ToLower().Trim();

            //
            // README
            //
            if (inputoptions.ContainsKey("op") && inputoptions["op"] == "readme")
            {
                var readme = ReadResourceLines("readme.txt");

                Console.ForegroundColor = ConsoleColor.DarkYellow;
                foreach (var l in readme)
                    Console.WriteLine(l);

                if (inputoptions["prompt"] == "true")
                {
                    var c = Console.ReadKey();
                }

                return;
            }

            var maps = new List<Mapping>();

            if (!inputoptions.ContainsKey("manifest"))
            {
                if (!inputoptions.ContainsKey("from") || !Util.ValidatePath(inputoptions["from"]))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"INVALID FROM PATH: [from:{inputoptions["from"]}");
                    Usage();
                    return;
                }

                if (!inputoptions.ContainsKey("to") || !Util.ValidatePath(inputoptions["to"], true))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"INVALID TO PATH: [to:{inputoptions["to"]}");
                    Usage();
                    return;
                }

                maps.Add(new Mapping { fromPath = inputoptions["from"], toPath = inputoptions["to"] });
            }
            else
            {
                string[] flines = new string[] { };

                try
                {
                    flines =
                        File.ReadAllLines(inputoptions["manifest"])
                        .Where(l => !l.Trim().StartsWith("#"))
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToArray();
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"UNABLE TO READ MANIFEST FILE: [manifest:{inputoptions["manifest"]}");
                    Usage();
                    return;
                }

                foreach (var fline in flines)
                {
                    try
                    {
                        var chunks = fline.Split(new string[] { "->" }, StringSplitOptions.None)
                            .Select(c => c.Trim())
                            .ToArray();

                        var fmap = new Mapping
                        {
                            fromPath = chunks[0],
                            toPath = chunks[1],
                            options = chunks.Length > 2 ? chunks[2] : string.Empty,
                        };

                        maps.Add(fmap);
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"UNABLE TO PARSE  MANIFEST LINE: {fline}");
                        Usage();
                        return;
                    }
                }
            }

            ////////// validate each mapping
            ////////foreach (var map in maps)
            ////////{
            ////////    //
            ////////    // validate paths
            ////////    //
            ////////    if (!Util.ValidatePath(map.fromPath))
            ////////    {
            ////////        Console.ForegroundColor = ConsoleColor.White;
            ////////        Console.WriteLine($"INVALID FROM PATH: {map.fromPath}");
            ////////        Usage();
            ////////        return;
            ////////    }

            ////////    if (!Util.ValidatePath(map.toPath, true))
            ////////    {
            ////////        Console.ForegroundColor = ConsoleColor.White;
            ////////        Console.WriteLine($"INVALID TO PATH: {map.toPath}");
            ////////        Usage();
            ////////        return;
            ////////    }
            ////////}

            // batch timings
            var bwatch = new System.Diagnostics.Stopwatch();
            bwatch.Start();

            //
            // process each mapping
            //
            foreach (var map in maps)
            {
                //
                // validate paths
                //
                if (!Util.ValidatePath(map.fromPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"");
                    Console.WriteLine($"INVALID FROM PATH: {map.fromPath}");
                    Console.WriteLine($"COMMAND WAS NOT PROCESSED: {map.ToString()}");
                    Console.WriteLine($"");
                    continue;
                }

                if (!Util.ValidatePath(map.toPath, true))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"");
                    Console.WriteLine($"INVALID TO PATH: {map.toPath}");
                    Console.WriteLine($"COMMAND WAS NOT PROCESSED: {map.ToString()}");
                    Console.WriteLine($"");
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(string.Empty);
                Console.WriteLine($"Start processing: {map.ToString()}");
                var mwatch = new System.Diagnostics.Stopwatch();
                mwatch.Start();

                var mapoptions = Util.CloneWithOverrides(inputoptions, map.options);
                if (mapoptions["op"] == "sync")
                {
                    Filesync.SyncFolder(map, mapoptions);
                }
                else if (mapoptions["op"] == "wayback")
                {
                    var slug = inputoptions["slug"];

                    var wbkey = $"\\{slug}{DateTime.Now:yyyy-MM}";
                    map.toPath += wbkey; 
                    Filesync.SyncFolder(map, mapoptions);
                }
                else if (mapoptions["op"] == "purge")
                {
                    var slug = inputoptions["slug"];

                    var keeplist = PurgeToKeep(mapoptions["retain"])
                        .Select(k => slug + k).ToList();
                    var keephash = new HashSet<string>(keeplist);

                    var found = Directory.EnumerateDirectories(map.toPath).ToList();
                    found = found.Select(f => f.Split('\\').Last()).ToList();
                    found = found.Where(f => f.StartsWith(slug)).ToList();
                    found = found.Except(keephash).ToList();

                    foreach(var todel in found)
                    {
                        // must be [slug]yyyy-MM format to be in scope
                        if (todel.Length > slug.Length)
                        {
                            var rest = todel.Substring(slug.Length) + "-01";
                            DateTime trydate;
                            if (DateTime.TryParse(rest, out trydate))
                            {
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine($"PURGE TARGET FOUND: " + todel);

                                Filesync.DeleteFolder(map.toPath + "\\" + todel, mapoptions);

                            }
                        }
                    }
                }
                else if (mapoptions["op"] == "remver")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"REMVER NOT YET IMPLEMENTED");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ILLEGAL OP - op:{mapoptions["op"]}");
                }

                mwatch.Stop();
                var et = mwatch.Elapsed;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Done processing: {map}");
                Console.WriteLine($"Elapsed: {et.TotalSeconds:#,###} Seconds ({et.Hours:0#}H:{et.Minutes:0#}M:{et.Seconds:0#}S)");
            }

            // batch timings
            var bt = bwatch.Elapsed;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(string.Empty);
            Console.WriteLine($"TOTAL ELAPSED: {bt.TotalSeconds:#,###} Seconds ({bt.Hours:0#}H:{bt.Minutes:0#}M:{bt.Seconds:0#}S)");

            if (inputoptions["prompt"] == "true")
            {
                var c = Console.ReadKey();
            }
        }

        static List<string> PurgeToKeep(string retainoption)
        {
            var opts = retainoption.Split(',')
                        .Select(o => o.Trim())
                        .Select(o => new { key = o.Substring(0, 1), value = int.Parse(o.Substring(1, o.Length - 1)) })
                        .ToDictionary(kv => kv.key, kv => kv.value);

            var keep = new HashSet<DateTime>();

            for (int m = 0; m < opts["m"]; m++)
            {
                var dt = DateTime.Now.Date.AddMonths(-m);
                keep.Add(dt);
            }

            for (int q = 0; q < opts["q"]; q++)
            {
                //
                // logic
                //
                // foreach (var m in new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })
                //    Console.WriteLine($"{m} => {(m + 2) / 3 * 3}");
                var dt = DateTime.Now.Date.AddMonths(-(q * 3));
                dt = new DateTime(dt.Year, (dt.Month + 2) / 3 * 3, dt.Day);
                keep.Add(dt);
            }

            for (int y = 0; y <= opts["y"]; y++)
            {
                var dt = new DateTime(DateTime.Now.Year - y,12,1);
                keep.Add(dt);
            }

            var res = keep.Select(d => $"{d:yyyy-MM}").OrderByDescending(d => d).ToList();
            return res;
        }

        static void Usage()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("");
            Console.WriteLine("Filesync USAGE");
            Console.WriteLine("");
            Console.WriteLine("FOR README: filesync op:readme");
            Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.White;
            return;
        }

        static List<string> ReadResourceLines(string name)
        {
            var lines = ReadResource(name).Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();
            return lines;
        }

        static string ReadResource(string name)
        {
            var names = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames().ToList();
            var resourceName = names.Where(str => str.EndsWith(name)).FirstOrDefault();

            string result;

            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (StreamReader reader = new StreamReader(stream))
                {
                    result = reader.ReadToEnd();
                }

            return result;
        }
    }
}

