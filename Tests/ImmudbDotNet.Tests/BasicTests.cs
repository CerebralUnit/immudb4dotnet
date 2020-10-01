using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CodeNotary.ImmuDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmudbDotNet.Tests
{
    [TestClass]
    public class BasicTests
    {
        // for now, this is where we are looking for immudb installation
        //if it's not there, we will not start/stop db and assume that its already running on localhost
        // the tests are using same "unittestdb" and made to be able to ran multiple times on the same instance
        private static string localImmuDB = @"C:\Temp\immudb\immudb.exe";
        private static Process localImmuDBProcess;

        [AssemblyInitialize]
        public static void InitImmuDB(TestContext context)
        {
            _ = context;

            if (File.Exists(localImmuDB))
            {
                foreach (var process in Process.GetProcessesByName("immudb"))
                {
                    process.Kill();
                }

                var dataFolder = Path.Combine(Path.GetDirectoryName(typeof(BasicTests).Assembly.Location), "data");

                if (Directory.Exists(dataFolder))
                {
                    Directory.Delete(dataFolder, true);
                }

                localImmuDBProcess = Process.Start(localImmuDB);
            }
        }

        [AssemblyCleanup]
        public static void StopImmuDB()
        {
            if (localImmuDBProcess != null)
            {
                localImmuDBProcess.Kill();
            }
        }

        [TestMethod]
        public async Task ConnectionTest()
        {
            using var client = new ImmuClient("localhost");

            await client.LoginAsync("immudb", "immudb", "unittestdb");

            var key = $"test key {DateTime.Now.Ticks}";

            if (!client.TryGet(key, out var testValue))
            {
                await client.SetAsync(key, "test value");

                testValue = await client.GetAsync(key);

                Assert.AreEqual(testValue, "test value");

                await client.SafeSetAsync(key, "test value 2");

                testValue = await client.SafeGetAsync(key);

                Assert.AreEqual(testValue, "test value 2");
            }
            else
            {
                Assert.Fail("key should not exist");
            }
        }
    }
}
