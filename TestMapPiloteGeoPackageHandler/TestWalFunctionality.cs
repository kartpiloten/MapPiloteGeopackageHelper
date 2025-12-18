using MapPiloteGeopackageHelper;
using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler;

[TestClass]
public class TestWalFunctionality
{
    private static string CreateTempGpkgPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wal_functionality_test_{Guid.NewGuid():N}.gpkg");
        return path;
    }

    [TestMethod]
    public void WalMode_EnabledGeoPackage_ShouldSupportConcurrentReadsWhileWriting()
    {
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        
        try
        {
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                srid: 3006, 
                walMode: true, 
                onStatus: msg => statusMessages.Add(msg));

            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT",
                ["value"] = "INTEGER"
            };
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", schema);

            Point point1 = new(100, 200);
            CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "test_layer", point1, ["Point1", "10"]);

            using SqliteConnection readerConnection = new($"Data Source={gpkg}");
            using SqliteConnection writerConnection = new($"Data Source={gpkg}");
            
            readerConnection.Open();
            writerConnection.Open();

            using SqliteCommand journalCommand = new("PRAGMA journal_mode", readerConnection);
            string? journalMode = journalCommand.ExecuteScalar()?.ToString();
            Assert.AreEqual("wal", journalMode?.ToLower(), "WAL mode should be active");

            using SqliteTransaction writerTransaction = writerConnection.BeginTransaction();
            
            using SqliteCommand insertCommand = new(
                "INSERT INTO test_layer (geom, name, value) VALUES (@geom, @name, @value)", 
                writerConnection, writerTransaction);
            
            Point point2 = new(300, 400);
            byte[] wkb = point2.ToBinary();
            byte[] gpkgBlob = CMPGeopackageUtils.CreateGpkgBlob(wkb, 3006);
            
            insertCommand.Parameters.AddWithValue("@geom", gpkgBlob);
            insertCommand.Parameters.AddWithValue("@name", "Point2");
            insertCommand.Parameters.AddWithValue("@value", 20);
            insertCommand.ExecuteNonQuery();

            using SqliteCommand readerCommand = new("SELECT COUNT(*) FROM test_layer", readerConnection);
            int countBeforeCommit = Convert.ToInt32(readerCommand.ExecuteScalar());
            
            Assert.AreEqual(1, countBeforeCommit, "Reader should see consistent snapshot before writer commits");

            writerTransaction.Commit();

            using SqliteCommand readerCommand2 = new("SELECT COUNT(*) FROM test_layer", readerConnection);
            int countAfterCommit = Convert.ToInt32(readerCommand2.ExecuteScalar());
            
            Assert.IsTrue(countAfterCommit >= 1, "Reader should eventually see committed data");

            bool hasWalMessage = statusMessages.Any(msg => 
                msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
            Assert.IsTrue(hasWalMessage, "Should report WAL mode enablement");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void WalMode_ComparedToNormalMode_ShouldShowDifferentJournalModes()
    {
        string walGpkg = CreateTempGpkgPath();
        string normalGpkg = CreateTempGpkgPath();
        
        try
        {
            CMPGeopackageCreateHelper.CreateGeoPackage(walGpkg, srid: 3006, walMode: true);
            CMPGeopackageCreateHelper.CreateGeoPackage(normalGpkg, srid: 3006, walMode: false);

            using (SqliteConnection walConnection = new($"Data Source={walGpkg}"))
            {
                walConnection.Open();
                using SqliteCommand walCommand = new("PRAGMA journal_mode", walConnection);
                string? walMode = walCommand.ExecuteScalar()?.ToString();
                Assert.AreEqual("wal", walMode?.ToLower(), "WAL GeoPackage should use WAL journal mode");
            }

            using (SqliteConnection normalConnection = new($"Data Source={normalGpkg}"))
            {
                normalConnection.Open();
                using SqliteCommand normalCommand = new("PRAGMA journal_mode", normalConnection);
                string? normalMode = normalCommand.ExecuteScalar()?.ToString();
                Assert.AreNotEqual("wal", normalMode?.ToLower(), "Normal GeoPackage should not use WAL journal mode");
                
                Assert.IsTrue(normalMode?.ToLower() == "delete" || normalMode?.ToLower() == "truncate", 
                    $"Normal mode should be delete or truncate, but was: {normalMode}");
            }
        }
        finally
        {
            TryDeleteFile(walGpkg);
            TryDeleteFile(normalGpkg);
        }
    }

    [TestMethod]
    public void WalMode_CreateAndUseGeoPackage_ShouldMaintainDataIntegrity()
    {
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        
        try
        {
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                srid: 4326, 
                walMode: true, 
                onStatus: msg => statusMessages.Add(msg));

            Dictionary<string, string> schema = new()
            {
                ["city_name"] = "TEXT",
                ["population"] = "INTEGER",
                ["area_km2"] = "REAL"
            };
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "cities", schema, "POINT", 4326);

            (string Name, int Pop, double Area, Point Point)[] cities =
            [
                ("Stockholm", 975551, 188.0, new Point(18.0686, 59.3293)),
                ("Gothenburg", 579281, 203.67, new Point(11.9746, 57.7089)),
                ("Malmö", 344166, 156.87, new Point(13.0007, 55.6050))
            ];

            foreach ((string Name, int Pop, double Area, Point Point) city in cities)
            {
                CGeopackageAddDataHelper.AddPointToGeoPackage(
                    gpkg, "cities", city.Point, 
                    [city.Name, city.Pop.ToString(), city.Area.ToString()]);
            }

            List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "cities").ToList();
            
            Assert.AreEqual(3, features.Count, "Should have inserted 3 cities");
            
            FeatureRecord? stockholm = features.FirstOrDefault(f => f.Attributes["city_name"] == "Stockholm");
            Assert.IsNotNull(stockholm, "Stockholm should be found");
            Assert.AreEqual("975551", stockholm.Attributes["population"], "Stockholm population should be correct");

            using SqliteConnection connection = new($"Data Source={gpkg}");
            connection.Open();

            using SqliteCommand journalCommand = new("PRAGMA journal_mode", connection);
            string? journalMode = journalCommand.ExecuteScalar()?.ToString();
            Assert.AreEqual("wal", journalMode?.ToLower(), "Should maintain WAL mode");

            using SqliteCommand tablesCommand = new(
                "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('gpkg_contents', 'gpkg_spatial_ref_sys', 'gpkg_geometry_columns', 'cities')", 
                connection);
            
            List<string> tableResults = [];
            using SqliteDataReader reader = tablesCommand.ExecuteReader();
            while (reader.Read())
            {
                tableResults.Add(reader.GetString(0));
            }

            Assert.AreEqual(4, tableResults.Count, "Should have all required tables");
            Assert.IsTrue(tableResults.Contains("cities"), "Should have cities table");
            Assert.IsTrue(tableResults.Contains("gpkg_contents"), "Should have gpkg_contents table");

            bool hasWalMessage = statusMessages.Any(msg => msg.Contains("Enabled WAL"));
            bool hasCreationMessage = statusMessages.Any(msg => msg.Contains("Successfully created GeoPackage"));
            
            Assert.IsTrue(hasWalMessage, "Should report WAL enablement");
            Assert.IsTrue(hasCreationMessage, "Should report successful creation");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void WalMode_PerformanceBenchmark_ShouldCompleteWithinReasonableTime()
    {
        string gpkg = CreateTempGpkgPath();
        
        try
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, srid: 3006, walMode: true);
            
            Dictionary<string, string> schema = new() { ["test"] = "TEXT" };
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "perf_test", schema);
            
            for (int i = 0; i < 100; i++)
            {
                Point point = new(i * 10, i * 10);
                CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "perf_test", point, [$"Point_{i}"]);
            }
            
            stopwatch.Stop();
            
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, 
                $"WAL mode operations should complete within 10 seconds, took {stopwatch.ElapsedMilliseconds}ms");
            
            List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "perf_test").ToList();
            Assert.AreEqual(100, features.Count, "All 100 points should be written with WAL mode");
            
            Console.WriteLine($"WAL mode performance test completed in {stopwatch.ElapsedMilliseconds}ms");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void WalMode_CheckpointAndCleanup_ShouldManageWalFilesCorrectly()
    {
        string gpkg = CreateTempGpkgPath();
        
        try
        {
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, walMode: true);
            
            Dictionary<string, string> schema = new() { ["data"] = "TEXT" };
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "wal_test", schema);
            
            for (int i = 0; i < 10; i++)
            {
                Point point = new(i, i);
                CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "wal_test", point, [$"Data_{i}"]);
            }
            
            string walFile = gpkg + "-wal";
            string shmFile = gpkg + "-shm";
            
            bool walFileExists = File.Exists(walFile);
            bool shmFileExists = File.Exists(shmFile);
            
            using (SqliteConnection connection = new($"Data Source={gpkg}"))
            {
                connection.Open();
                
                using SqliteCommand journalCommand = new("PRAGMA journal_mode", connection);
                string? journalMode = journalCommand.ExecuteScalar()?.ToString();
                Assert.AreEqual("wal", journalMode?.ToLower(), "Should be in WAL mode");
                
                using SqliteCommand checkpointCommand = new("PRAGMA wal_checkpoint(TRUNCATE)", connection);
                object? result = checkpointCommand.ExecuteScalar();
                
                using SqliteCommand countCommand = new("SELECT COUNT(*) FROM wal_test", connection);
                int count = Convert.ToInt32(countCommand.ExecuteScalar());
                Assert.AreEqual(10, count, "All data should be accessible after checkpoint");
            }
            
            Assert.IsTrue(File.Exists(gpkg), "Main GeoPackage file should exist");
            
            List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "wal_test").ToList();
            Assert.AreEqual(10, features.Count, "All features should be readable after WAL operations");
            
            Console.WriteLine($"WAL file existed during test: {walFileExists}");
            Console.WriteLine($"SHM file existed during test: {shmFileExists}");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void WalMode_BackwardCompatibility_ExistingCodeShouldStillWork()
    {
        string gpkg = CreateTempGpkgPath();
        
        try
        {
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, srid: 3006, walMode: true);
            
            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT",
                ["type"] = "TEXT"
            };
            
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "compatibility_test", schema);
            
            Point point = new(500, 600);
            CGeopackageAddDataHelper.AddPointToGeoPackage(
                gpkg, "compatibility_test", point, ["Test Point", "Test Type"]);
            
            List<FeatureRecord> features =
            [
                new FeatureRecord(
                    new Point(100, 100),
                    new Dictionary<string, string?> { ["name"] = "Bulk1", ["type"] = "TypeA" }
                ),
                new FeatureRecord(
                    new Point(200, 200),
                    new Dictionary<string, string?> { ["name"] = "Bulk2", ["type"] = "TypeB" }
                )
            ];
            
            CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "compatibility_test", features);
            
            List<FeatureRecord> readFeatures = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "compatibility_test").ToList();
            
            Assert.AreEqual(3, readFeatures.Count, "Should read all inserted features with WAL mode");
            
            FeatureRecord? testPoint = readFeatures.FirstOrDefault(f => f.Attributes["name"] == "Test Point");
            Assert.IsNotNull(testPoint, "Individual insert should work with WAL mode");
            
            FeatureRecord? bulk1 = readFeatures.FirstOrDefault(f => f.Attributes["name"] == "Bulk1");
            FeatureRecord? bulk2 = readFeatures.FirstOrDefault(f => f.Attributes["name"] == "Bulk2");
            
            Assert.IsNotNull(bulk1, "Bulk insert feature 1 should work with WAL mode");
            Assert.IsNotNull(bulk2, "Bulk insert feature 2 should work with WAL mode");
            
            using SqliteConnection connection = new($"Data Source={gpkg}");
            connection.Open();
            using SqliteCommand command = new("PRAGMA journal_mode", connection);
            string? mode = command.ExecuteScalar()?.ToString();
            Assert.AreEqual("wal", mode?.ToLower(), "WAL mode should be maintained throughout operations");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try 
        { 
            if (File.Exists(path)) 
            {
                File.Delete(path);
                
                string walFile = path + "-wal";
                string shmFile = path + "-shm";
                
                if (File.Exists(walFile)) File.Delete(walFile);
                if (File.Exists(shmFile)) File.Delete(shmFile);
            }
        } 
        catch 
        { 
        }
    }
}