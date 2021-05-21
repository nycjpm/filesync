namespace filesync
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Linq;

    public class Util
    {
        private static string[] numericTypes = new string[] { "int", "money", "decimal", "float", "real" };

        public static Dictionary<string, string> ParseArgs(string[] args)
        {
            var options = new Dictionary<string, string>();

            foreach (var arg in args)
            {
                var tuple = arg.Split(new string[] { ":" }, 2, StringSplitOptions.None);
                tuple[0] = tuple[0].ToLower().Trim();
                tuple[1] = tuple[1].Trim();
                options[tuple[0]] = tuple[1];
            }

            return options;
        }

        public static Dictionary<string, string> CloneDictionary(Dictionary<string, string> source)
        {
            var target = new Dictionary<string, string>();
            foreach (var key in source.Keys)
            {
                target[key] = source[key];
            }

            return target;
        }

        // override options in command line argument format (key:val key2:val key3:v1,v2)
        public static Dictionary<string, string> CloneWithOverrides(Dictionary<string, string> source, string options)
        {
            var target = CloneDictionary(source);

            if (!string.IsNullOrWhiteSpace(options))
            {
                var keyvals = options.Trim().Split(' ')
                    .Select(o => o.Split(':'))
                    .Select(a => new { key = a[0].Trim(), value = a[1].Trim() })
                    .ToList();

                foreach (var keyval in keyvals)
                {
                    target[keyval.key] = keyval.value;
                }
            }

            return target;
        }

        // vaidate that a file path exists (and optionally create it)
        public static bool ValidatePath(string path, bool create = false)
        {
            if (Directory.Exists(path))
            {
                return true;
            }

            if (!create)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        public static string ReplaceDict(string template, string token, Dictionary<string, string> dict, string key)
        {
            if (dict.ContainsKey(key))
            {
                return template.Replace(token, dict[key]);
            }

            return template;
        }

        // interprets the date codes "today" and "yesterday" for an option
        public static void InterpretDateCodeOption(Dictionary<string, string> options, string key)
        {
            if (!options.ContainsKey(key))
            {
                options[key] =
                options[key] = "today";
            }

            if (options[key] == "yesterday")
            {
                var delta = -1;

                if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    delta = -2;
                }

                if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
                {
                    delta = -3;
                }

                options[key] = string.Format("{0:yyyy-MM-dd}", DateTime.Now.AddDays(delta).Date);
            }

            if (options[key] == "today")
            {
                options[key] = string.Format("{0:yyyy-MM-dd}", DateTime.Now.Date);
            }
        }

        public static string FileName(string fullpath)
        {
            var chunks = fullpath.Split(new string[] { "\\" }, StringSplitOptions.None);
            var name = chunks[chunks.Count() - 1];
            chunks = name.Split(new string[] { ".csv" }, StringSplitOptions.None);
            name = chunks[0];

            return name;
        }

        // SQL Library
        public static SqlConnection ConnectSql(string connStr)
        {
            SqlConnection connection = new SqlConnection(connStr);
            return connection;
        }

        public static string Fq(string sql)
        {
            return sql.Replace("'", "''");
        }

        public static DataSet ExecSQL(string sql, string connStr)
        {
            var conn = ConnectSql(connStr);
            var ds = ExecSQL(sql, conn);
            conn.Close();
            return ds;
        }

        // default to 30 seconds
        public static DataSet ExecSQL(string sql, SqlConnection connection)
        {
            return ExecSQL(sql, connection, 30);
        }

        // adjust timeout
        public static DataSet ExecSQL(string sql, SqlConnection connection, int timeoutSeconds)
        {
            var cmd = new SqlCommand(sql, connection);
            cmd.CommandTimeout = timeoutSeconds;
            var adapter = new SqlDataAdapter(cmd);
            var ds = new DataSet();
            adapter.Fill(ds);

            return ds;
        }

    }
}
