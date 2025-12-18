using MapPiloteGeopackageHelper;
using Microsoft.Data.Sqlite;

namespace TestMapPiloteGeoPackageHandler;

[TestClass]
public class TestWalModeSupport
{
    private static string CreateTempGpkgPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wal_test_{Guid.NewGuid():N}.gpkg");
        return path;
    }

    [TestMethod]
    public void CreateGeoPackage_WithWalModeTrue_ShouldEnableWalMode()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        
        try
        {
            // Act
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                srid: 3006, 
                walMode: true, 
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage file should be created");
            
            bool hasWalMessage = statusMessages.Any(msg => 
                msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
            Assert.IsTrue(hasWalMessage, "Should report WAL mode enablement");

            using SqliteConnection connection = new($"Data Source={gpkg}");
            connection.Open();
            
            using SqliteCommand command = new("PRAGMA journal_mode", connection);
            string? journalMode = command.ExecuteScalar()?.ToString();
            
            Assert.AreEqual("wal", journalMode?.ToLower(), "Journal mode should be set to WAL");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeoPackage_WithWalModeFalse_ShouldUseDefaultJournalMode()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        
        try
        {
            // Act
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                srid: 3006, 
                walMode: false, 
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage file should be created");
            
            bool hasWalMessage = statusMessages.Any(msg => 
                msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
            Assert.IsFalse(hasWalMessage, "Should not report WAL mode enablement when disabled");

            using SqliteConnection connection = new($"Data Source={gpkg}");
            connection.Open();
            
            using SqliteCommand command = new("PRAGMA journal_mode", connection);
            string? journalMode = command.ExecuteScalar()?.ToString();
            
            Assert.AreNotEqual("wal", journalMode?.ToLower(), "Journal mode should not be WAL when walMode=false");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeoPackage_BackwardCompatibility_WithoutWalParameter_ShouldWork()
    {
        // Test that existing method calls without walMode parameter continue to work
        string gpkg = CreateTempGpkgPath();
        
        try
        {
            // Act - Use old method signature (should call the overload with walMode=false)
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);

            // Assert
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created with backward compatible call");

            using SqliteConnection connection = new($"Data Source={gpkg}");
            connection.Open();
            
            using SqliteCommand command = new("PRAGMA journal_mode", connection);
            string? journalMode = command.ExecuteScalar()?.ToString();
            
            Assert.AreNotEqual("wal", journalMode?.ToLower(), "Should default to non-WAL mode for backward compatibility");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeoPackage_BackwardCompatibility_WithCallback_ShouldWork()
    {
        // Test backward compatibility with callback parameter
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        
        try
        {
            // Act - Use old method signature with callback
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                srid: 3006, 
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created");
            Assert.IsTrue(statusMessages.Count > 0, "Should receive status messages");
            
            bool hasWalMessage = statusMessages.Any(msg => 
                msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
            Assert.IsFalse(hasWalMessage, "Should not enable WAL by default in backward compatible call");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeoPackage_WalModeWithCustomSrid_ShouldWork()
    {
        // Test that WAL mode works with custom SRID
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        
        try
        {
            // Act
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                srid: 4326,
                walMode: true, 
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created");
            
            bool hasWalMessage = statusMessages.Any(msg => 
                msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
            Assert.IsTrue(hasWalMessage, "Should enable WAL mode");

            using SqliteConnection connection = new($"Data Source={gpkg}");
            connection.Open();
            
            using SqliteCommand journalCommand = new("PRAGMA journal_mode", connection);
            string? journalMode = journalCommand.ExecuteScalar()?.ToString();
            Assert.AreEqual("wal", journalMode?.ToLower(), "Should use WAL mode");

            using SqliteCommand sridCommand = new(
                "SELECT COUNT(*) FROM gpkg_spatial_ref_sys WHERE srs_id = 4326", connection);
            int sridCount = Convert.ToInt32(sridCommand.ExecuteScalar());
            Assert.AreEqual(1, sridCount, "WGS84 SRID should be present");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeoPackage_WalModeWithDefaultSrid_ShouldWork()
    {
        // Test WAL mode with default SRID (using walMode-first overload)
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        
        try
        {
            // Act - Use walMode as first parameter after path
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                walMode: true, 
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created");
            
            bool hasWalMessage = statusMessages.Any(msg => 
                msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
            Assert.IsTrue(hasWalMessage, "Should enable WAL mode");

            using SqliteConnection connection = new($"Data Source={gpkg}");
            connection.Open();
            
            using SqliteCommand journalCommand = new("PRAGMA journal_mode", connection);
            string? journalMode = journalCommand.ExecuteScalar()?.ToString();
            Assert.AreEqual("wal", journalMode?.ToLower(), "Should use WAL mode");

            using SqliteCommand sridCommand = new(
                "SELECT COUNT(*) FROM gpkg_spatial_ref_sys WHERE srs_id = 3006", connection);
            int sridCount = Convert.ToInt32(sridCommand.ExecuteScalar());
            Assert.AreEqual(1, sridCount, "Default SRID 3006 should be present");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeoPackage_WalModePerformanceTest_ShouldCreateSuccessfully()
    {
        // Basic test to ensure WAL mode doesn't break GeoPackage creation
        string gpkg = CreateTempGpkgPath();
        
        try
        {
            // Act
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, walMode: true);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created with WAL mode");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "Creation should complete in reasonable time");

            using SqliteConnection connection = new($"Data Source={gpkg}");
            connection.Open();

            string[] tables = ["gpkg_spatial_ref_sys", "gpkg_contents", "gpkg_geometry_columns"];
            foreach (string table in tables)
            {
                using SqliteCommand command = new(
                    $"SELECT name FROM sqlite_master WHERE type='table' AND name='{table}'", connection);
                object? result = command.ExecuteScalar();
                Assert.IsNotNull(result, $"Required table {table} should exist");
            }
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeoPackage_MethodOverloads_ShouldAllWork()
    {
        List<(string Name, Action TestAction)> testCases =
        [
            ("Path only", () => {
                string path = CreateTempGpkgPath();
                try { CMPGeopackageCreateHelper.CreateGeoPackage(path); } finally { TryDeleteFile(path); }
            }),
            
            ("Path + SRID", () => {
                string path = CreateTempGpkgPath();
                try { CMPGeopackageCreateHelper.CreateGeoPackage(path, 4326); } finally { TryDeleteFile(path); }
            }),
            
            ("Path + Callback", () => {
                string path = CreateTempGpkgPath();
                try { CMPGeopackageCreateHelper.CreateGeoPackage(path, msg => { }); } finally { TryDeleteFile(path); }
            }),
            
            ("Path + SRID + Callback", () => {
                string path = CreateTempGpkgPath();
                try { CMPGeopackageCreateHelper.CreateGeoPackage(path, 4326, msg => { }); } finally { TryDeleteFile(path); }
            }),
            
            ("Path + WAL", () => {
                string path = CreateTempGpkgPath();
                try { CMPGeopackageCreateHelper.CreateGeoPackage(path, walMode: true); } finally { TryDeleteFile(path); }
            }),
            
            ("Path + SRID + WAL + Callback", () => {
                string path = CreateTempGpkgPath();
                try { CMPGeopackageCreateHelper.CreateGeoPackage(path, 4326, true, msg => { }); } finally { TryDeleteFile(path); }
            })
        ];

        foreach ((string Name, Action TestAction) testCase in testCases)
        {
            try
            {
                testCase.TestAction.Invoke();
                Assert.IsTrue(true, $"{testCase.Name} overload should work");
            }
            catch (Exception ex)
            {
                Assert.Fail($"{testCase.Name} overload failed: {ex.Message}");
            }
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