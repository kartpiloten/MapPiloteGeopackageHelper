using MapPiloteGeopackageHelper;
using Microsoft.Data.Sqlite;

namespace TestMapPiloteGeoPackageHandler;

/// <summary>
/// Tests for WAL mode support.
/// Output files are saved to TestResults/GeoPackages folder for inspection in QGIS.
/// </summary>
[TestClass]
public class TestWalModeSupport
{
    [TestMethod]
    public void CreateGeoPackage_WithWalModeTrue_ShouldEnableWalMode()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(
            gpkg, 
            srid: 3006, 
            walMode: true, 
            onStatus: msg => statusMessages.Add(msg));

        Assert.IsTrue(File.Exists(gpkg), "GeoPackage file should be created");
        
        bool hasWalMessage = statusMessages.Any(msg => 
            msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
        Assert.IsTrue(hasWalMessage, "Should report WAL mode enablement");

        using SqliteConnection connection = new($"Data Source={gpkg}");
        connection.Open();
        
        using SqliteCommand command = new("PRAGMA journal_mode", connection);
        string? journalMode = command.ExecuteScalar()?.ToString();
        
        Assert.AreEqual("wal", journalMode?.ToLower(), "Journal mode should be set to WAL");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeoPackage_WithWalModeFalse_ShouldUseDefaultJournalMode()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(
            gpkg, 
            srid: 3006, 
            walMode: false, 
            onStatus: msg => statusMessages.Add(msg));

        Assert.IsTrue(File.Exists(gpkg), "GeoPackage file should be created");
        
        bool hasWalMessage = statusMessages.Any(msg => 
            msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
        Assert.IsFalse(hasWalMessage, "Should not report WAL mode enablement when disabled");

        using SqliteConnection connection = new($"Data Source={gpkg}");
        connection.Open();
        
        using SqliteCommand command = new("PRAGMA journal_mode", connection);
        string? journalMode = command.ExecuteScalar()?.ToString();
        
        Assert.AreNotEqual("wal", journalMode?.ToLower(), "Journal mode should not be WAL when walMode=false");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeoPackage_BackwardCompatibility_WithoutWalParameter_ShouldWork()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);

        Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created with backward compatible call");

        using SqliteConnection connection = new($"Data Source={gpkg}");
        connection.Open();
        
        using SqliteCommand command = new("PRAGMA journal_mode", connection);
        string? journalMode = command.ExecuteScalar()?.ToString();
        
        Assert.AreNotEqual("wal", journalMode?.ToLower(), "Should default to non-WAL mode for backward compatibility");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeoPackage_BackwardCompatibility_WithCallback_ShouldWork()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(
            gpkg, 
            srid: 3006, 
            onStatus: msg => statusMessages.Add(msg));

        Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created");
        Assert.IsTrue(statusMessages.Count > 0, "Should receive status messages");
        
        bool hasWalMessage = statusMessages.Any(msg => 
            msg.Contains("Enabled WAL (Write-Ahead Logging) mode"));
        Assert.IsFalse(hasWalMessage, "Should not enable WAL by default in backward compatible call");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeoPackage_WalModeWithCustomSrid_ShouldWork()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(
            gpkg, 
            srid: 4326,
            walMode: true, 
            onStatus: msg => statusMessages.Add(msg));

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
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeoPackage_WalModeWithDefaultSrid_ShouldWork()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(
            gpkg, 
            walMode: true, 
            onStatus: msg => statusMessages.Add(msg));

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
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeoPackage_WalModePerformanceTest_ShouldCreateSuccessfully()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, walMode: true);
        stopwatch.Stop();

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
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeoPackage_MethodOverloads_ShouldAllWork()
    {
        List<(string Name, Func<string> GetPath, Action<string> TestAction)> testCases =
        [
            ("PathOnly", () => TestOutputHelper.GetTestOutputPath("PathOnly"), 
                path => CMPGeopackageCreateHelper.CreateGeoPackage(path)),
            
            ("PathAndSrid", () => TestOutputHelper.GetTestOutputPath("PathAndSrid"), 
                path => CMPGeopackageCreateHelper.CreateGeoPackage(path, 4326)),
            
            ("PathAndCallback", () => TestOutputHelper.GetTestOutputPath("PathAndCallback"), 
                path => CMPGeopackageCreateHelper.CreateGeoPackage(path, msg => { })),
            
            ("PathSridCallback", () => TestOutputHelper.GetTestOutputPath("PathSridCallback"), 
                path => CMPGeopackageCreateHelper.CreateGeoPackage(path, 4326, msg => { })),
            
            ("PathAndWal", () => TestOutputHelper.GetTestOutputPath("PathAndWal"), 
                path => CMPGeopackageCreateHelper.CreateGeoPackage(path, walMode: true)),
            
            ("PathSridWalCallback", () => TestOutputHelper.GetTestOutputPath("PathSridWalCallback"), 
                path => CMPGeopackageCreateHelper.CreateGeoPackage(path, 4326, true, msg => { }))
        ];

        foreach (var testCase in testCases)
        {
            string path = testCase.GetPath();
            try
            {
                testCase.TestAction(path);
                Assert.IsTrue(File.Exists(path), $"{testCase.Name} overload should create file");
                TestOutputHelper.LogOutputLocation(path);
            }
            catch (Exception ex)
            {
                Assert.Fail($"{testCase.Name} overload failed: {ex.Message}");
            }
        }
    }
}