using System;
using System.IO;
using System.Threading.Tasks;
using WATA.LIS.Core.Parser;
using WATA.LIS.DB;
using WATA.LIS.Core.Model.SystemConfig;

class Program
{
    private static string SanitizeSearchPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "lis_core,public";
        var v = value.Trim().Replace("\"", string.Empty).Replace("'", string.Empty);
        v = v.Replace(";", ",").Replace("=", string.Empty);
        v = string.Join(",", v.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(v) ? "lis_core,public" : v;
    }

    static async Task<int> Main(string[] args)
    {
        try
        {
            var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "all";

            var uiWd = ResolveUiWorkingDirectory();
            if (!string.IsNullOrEmpty(uiWd))
            {
                Environment.CurrentDirectory = uiWd;
                Console.WriteLine($"[Smoke] WorkingDirectory set to: {uiWd}");
            }
            else
            {
                Console.WriteLine($"[Smoke] Could not resolve UI working dir. Using: {Environment.CurrentDirectory}");
            }

            MainConfigModel mainCfg = null;
            if (mode is "all" or "config")
            {
                Console.WriteLine("[Smoke] Config parse start...");
                var parser = new SystemJsonConfigParser();
                var (weight, distance, rfid, main, led, dps, nav, vision, livox, display) = parser.LoadJsonfile();
                mainCfg = main as MainConfigModel;
                Console.WriteLine($"[Smoke] Config OK. device_type={main?.device_type}");
            }

            if (mode is "all" or "db")
            {
                Console.WriteLine("[Smoke] DB migration start...");
                var conn = Environment.GetEnvironmentVariable("WATA_LIS_CONN");
                if (string.IsNullOrWhiteSpace(conn))
                {
                    try
                    {
                        if (mainCfg == null)
                        {
                            var parser = new SystemJsonConfigParser();
                            var (_, _, _, main, _, _, _, _, _, _) = parser.LoadJsonfile();
                            mainCfg = main as MainConfigModel;
                        }
                        string host = mainCfg?.db_host ?? "localhost";
                        int port = mainCfg?.db_port ?? 5432;
                        string database = !string.IsNullOrWhiteSpace(mainCfg?.db_database) ? mainCfg.db_database : "forkliftDB";
                        string username = !string.IsNullOrWhiteSpace(mainCfg?.db_username) ? mainCfg.db_username : "postgres";
                        string password = !string.IsNullOrWhiteSpace(mainCfg?.db_password) ? mainCfg.db_password : "wata2019";
                        string searchPath = SanitizeSearchPath(!string.IsNullOrWhiteSpace(mainCfg?.db_search_path) ? mainCfg.db_search_path : "lis_core,public");
                        conn = $"Host={host};Port={port};Database={database};Username={username};Password={password};SearchPath={searchPath};Pooling=true;Include Error Detail=true";
                    }
                    catch
                    {
                        conn = "Host=localhost;Port=5432;Database=forkliftDB;Username=postgres;Password=wata2019;SearchPath=lis_core,public";
                    }
                }

                try
                {
                    var migrator = new MigrationService(conn);
                    await migrator.EnsureSchemaAsync();
                    Console.WriteLine("[Smoke] DB migration OK.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Smoke] DB migration failed: {ex.GetType().Name} {ex.Message}");
                    Console.Error.WriteLine(ex.ToString());
                    return 1;
                }
            }

            Console.WriteLine("[Smoke] Done.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static string ResolveUiWorkingDirectory()
    {
        var env = Environment.GetEnvironmentVariable("WATA_LIS_WD");
        if (IsValidWd(env)) return env!;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 5 && dir != null; i++) dir = dir.Parent;
        if (dir != null)
        {
            var tfm = "net8.0-windows10.0.18362.0";
            var debug = Path.Combine(dir.FullName, "WATA.LIS", "WATA.LIS", "bin", "Debug", tfm);
            if (IsValidWd(debug)) return debug;
            var release = Path.Combine(dir.FullName, "WATA.LIS", "WATA.LIS", "bin", "Release", tfm);
            if (IsValidWd(release)) return release;
        }
        return string.Empty;
    }

    private static bool IsValidWd(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var cfg = Path.Combine(path, "SystemConfig", "SystemConfig.json");
            return File.Exists(cfg);
        }
        catch { return false; }
    }
}
