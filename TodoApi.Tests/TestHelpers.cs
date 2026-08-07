using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace TodoApi.Tests
{
    public static class TestHelpers
    {
        public static IConfiguration CreateConfiguration(string dbFilePath)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:TodoDatabase", $"Data Source={dbFilePath}" }
                })
                .Build();
        }

        public static string CreateTempDatabasePath()
        {
            return Path.Combine(Path.GetTempPath(), $"todos_{Guid.NewGuid():N}.db");
        }

        public static void DeleteFileWithRetries(string path, int retries = 5, int delayMs = 200)
        {
            if (!File.Exists(path)) return;

            for (int attempt = 0; attempt < retries; attempt++)
            {
                try
                {
                    File.Delete(path);
                    return;
                }
                catch (IOException)
                {
                    // Force finalizers and wait a bit for OS to release file handles, then retry
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(delayMs);
                }
                catch (UnauthorizedAccessException)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(delayMs);
                }
            }

            // Final attempt (suppress any exception to avoid failing cleanup)
            try { File.Delete(path); } catch { }
        }
    }
}