using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler;

/// <summary>
/// Tests for optional callback pattern.
/// Output files are saved to TestResults/GeoPackages folder for inspection in QGIS.
/// </summary>
[TestClass]
public class TestOptionalCallbackPattern
{
    [TestMethod]
    public void CreateGeoPackage_WithStatusCallback_ShouldInvokeCallbackWithCorrectMessages()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(
            gpkg, 
            srid: 3006,
            onStatus: msg => statusMessages.Add(msg));

        Assert.IsTrue(statusMessages.Count >= 1, "Should receive at least one status message");
        
        bool hasCreationMessage = statusMessages.Any(msg => 
            msg.Contains("Successfully created GeoPackage") && msg.Contains(gpkg));
        Assert.IsTrue(hasCreationMessage, "Should contain GeoPackage creation success message");
        
        Assert.IsTrue(File.Exists(gpkg), "GeoPackage file should exist");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeoPackage_WithExistingFile_ShouldInvokeDeletionCallback()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        // Create file first
        File.WriteAllText(gpkg, "dummy content");
        Assert.IsTrue(File.Exists(gpkg), "Setup: File should exist before test");

        CMPGeopackageCreateHelper.CreateGeoPackage(
            gpkg, 
            onStatus: msg => statusMessages.Add(msg));

        Assert.IsTrue(statusMessages.Count >= 2, "Should receive multiple status messages");
        
        bool hasDeletionMessage = statusMessages.Any(msg => 
            msg.Contains("Deleted existing GeoPackage file") && msg.Contains(gpkg));
        bool hasCreationMessage = statusMessages.Any(msg => 
            msg.Contains("Successfully created GeoPackage") && msg.Contains(gpkg));
        
        Assert.IsTrue(hasDeletionMessage, "Should contain file deletion message");
        Assert.IsTrue(hasCreationMessage, "Should contain GeoPackage creation message");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeopackageLayer_WithStatusAndErrorCallbacks_ShouldInvokeStatusOnSuccess()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        List<string> errorMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
        
        Dictionary<string, string> schema = new()
        {
            ["name"] = "TEXT",
            ["value"] = "INTEGER"
        };

        GeopackageLayerCreateHelper.CreateGeopackageLayer(
            gpkg, 
            "test_layer", 
            schema,
            "POINT",
            3006,
            onStatus: msg => statusMessages.Add(msg),
            onError: msg => errorMessages.Add(msg));

        Assert.IsTrue(statusMessages.Count >= 1, "Should receive status messages");
        Assert.AreEqual(0, errorMessages.Count, "Should not receive error messages on success");
        
        bool hasLayerCreationMessage = statusMessages.Any(msg => 
            msg.Contains("Successfully created spatial layer") && msg.Contains("test_layer"));
        Assert.IsTrue(hasLayerCreationMessage, "Should contain layer creation success message");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeopackageLayer_WithError_ShouldInvokeErrorCallback()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        List<string> errorMessages = [];
        
        Dictionary<string, string> schema = new()
        {
            ["test_col"] = "TEXT"
        };

        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(() =>
        {
            GeopackageLayerCreateHelper.CreateGeopackageLayer(
                gpkg, 
                "test_layer", 
                schema,
                onStatus: msg => statusMessages.Add(msg),
                onError: msg => errorMessages.Add(msg));
        });

        Assert.IsTrue(exception.Message.Contains("GeoPackage file not found"), 
            "Should throw FileNotFoundException with appropriate message");
    }

    [TestMethod]
    public void AddPointToGeoPackage_WithUnknownColumnType_ShouldInvokeWarningCallback()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> warningMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
        
        Dictionary<string, string> schema = new()
        {
            ["name"] = "TEXT",
            ["value"] = "INTEGER"
        };
        
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", schema);
        
        Point point = new(100, 200);
        string[] attributes = ["Test Name", "42"];

        CGeopackageAddDataHelper.AddPointToGeoPackage(
            gpkg, 
            "test_layer", 
            point, 
            attributes,
            onWarning: msg => warningMessages.Add(msg));

        Assert.AreEqual(0, warningMessages.Count, "No warnings should be generated for valid data types");
        
        List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer").ToList();
        Assert.AreEqual(1, features.Count, "Point should have been inserted");
        Assert.AreEqual("Test Name", features[0].Attributes["name"]);
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void BulkInsertFeatures_WithWarningCallback_ShouldAcceptCallback()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> warningMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
        
        Dictionary<string, string> schema = new()
        {
            ["name"] = "TEXT",
            ["count"] = "INTEGER"
        };
        
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "bulk_test", schema);
        
        List<FeatureRecord> features =
        [
            new FeatureRecord(
                new Point(100, 100),
                new Dictionary<string, string?> { ["name"] = "Feature1", ["count"] = "10" }
            ),
            new FeatureRecord(
                new Point(200, 200),
                new Dictionary<string, string?> { ["name"] = "Feature2", ["count"] = "20" }
            )
        ];

        CGeopackageAddDataHelper.BulkInsertFeatures(
            gpkg,
            "bulk_test", 
            features,
            onWarning: msg => warningMessages.Add(msg));

        Assert.AreEqual(0, warningMessages.Count, "No warnings expected for valid data");
        
        List<FeatureRecord> readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "bulk_test").ToList();
        Assert.AreEqual(2, readBack.Count, "Both features should be inserted");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public async Task FluentAPI_OpenAsync_WithStatusCallback_ShouldInvokeCallback()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        using GeoPackage geoPackage = await GeoPackage.OpenAsync(
            gpkg, 
            defaultSrid: 3006,
            onStatus: msg => statusMessages.Add(msg));

        Assert.IsTrue(statusMessages.Count >= 1, "Should receive initialization status messages");
        
        bool hasInitMessage = statusMessages.Any(msg => 
            msg.Contains("Successfully initialized GeoPackage") && msg.Contains(gpkg));
        Assert.IsTrue(hasInitMessage, "Should contain initialization success message");
        
        Assert.IsTrue(File.Exists(gpkg), "GeoPackage file should be created");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public async Task FluentAPI_OpenExisting_WithStatusCallback_ShouldNotInvokeCallback()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
        
        using GeoPackage geoPackage = await GeoPackage.OpenAsync(
            gpkg, 
            onStatus: msg => statusMessages.Add(msg));

        Assert.AreEqual(0, statusMessages.Count, "No status messages expected when opening existing GeoPackage");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public async Task FluentAPI_EnsureLayerAsync_ShouldCreateLayerSilently()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        
        using GeoPackage geoPackage = await GeoPackage.OpenAsync(gpkg);
        
        Dictionary<string, string> schema = new()
        {
            ["name"] = "TEXT",
            ["value"] = "INTEGER"
        };

        GeoPackageLayer layer = await geoPackage.EnsureLayerAsync("silent_layer", schema);

        Assert.IsNotNull(layer, "Layer should be created");
        
        CMPGeopackageReadDataHelper.GeopackageInfo info = await geoPackage.GetInfoAsync();
        bool layerExists = info.Layers.Any(l => l.TableName == "silent_layer");
        Assert.IsTrue(layerExists, "Layer should exist in GeoPackage metadata");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void BackwardCompatibility_AllMethodsWithoutCallbacks_ShouldWork()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
        
        Dictionary<string, string> schema = new()
        {
            ["name"] = "TEXT",
            ["age"] = "INTEGER"
        };
        
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "compat_test", schema);
        
        Point point = new(500, 600);
        string[] attributes = ["Alice", "30"];
        
        CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "compat_test", point, attributes);
        
        List<FeatureRecord> features =
        [
            new FeatureRecord(
                new Point(700, 800),
                new Dictionary<string, string?> { ["name"] = "Bob", ["age"] = "25" }
            )
        ];
        
        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "compat_test", features);

        Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created");
        
        List<FeatureRecord> readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "compat_test").ToList();
        Assert.AreEqual(2, readBack.Count, "Both features should be inserted");
        
        FeatureRecord? firstFeature = readBack.FirstOrDefault(f => f.Attributes["name"] == "Alice");
        FeatureRecord? secondFeature = readBack.FirstOrDefault(f => f.Attributes["name"] == "Bob");
        
        Assert.IsNotNull(firstFeature, "First feature should exist");
        Assert.IsNotNull(secondFeature, "Second feature should exist");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CallbackPattern_NullCallbacks_ShouldNotThrowExceptions()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, onStatus: null);
        
        Dictionary<string, string> schema = new()
        {
            ["test_col"] = "TEXT"
        };
        
        GeopackageLayerCreateHelper.CreateGeopackageLayer(
            gpkg, "null_test", schema,
            onStatus: null,
            onError: null);
        
        Point point = new(100, 200);
        string[] attributes = ["Test"];
        
        CGeopackageAddDataHelper.AddPointToGeoPackage(
            gpkg, "null_test", point, attributes,
            onWarning: null);

        Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created with null callbacks");
        
        List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "null_test").ToList();
        Assert.AreEqual(1, features.Count, "Feature should be inserted with null callbacks");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void CreateGeopackageLayer_WithNonExistentGeoPackage_ShouldThrowFileNotFoundException()
    {
        string nonExistentGpkg = TestOutputHelper.GetTestOutputPath();
        // Don't create the file - it should not exist
        TestOutputHelper.DeleteIfExists(nonExistentGpkg); // Ensure it doesn't exist
        
        List<string> statusMessages = [];
        List<string> errorMessages = [];
        
        Dictionary<string, string> schema = new()
        {
            ["test_col"] = "TEXT"
        };

        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(() =>
        {
            GeopackageLayerCreateHelper.CreateGeopackageLayer(
                nonExistentGpkg, 
                "test_layer", 
                schema,
                onStatus: msg => statusMessages.Add(msg),
                onError: msg => errorMessages.Add(msg));
        });

        Assert.IsTrue(exception.Message.Contains("GeoPackage file not found"), 
            "Should throw FileNotFoundException when GeoPackage file doesn't exist");
        Assert.IsTrue(exception.Message.Contains(nonExistentGpkg), 
            "Should include the file path in error message");
    }

    [TestMethod]
    public void CreateGeopackageLayer_WithInvalidPath_ShouldThrowDirectoryNotFoundException()
    {
        string invalidPath = "Z:\\NonExistentDirectory\\test.gpkg";
        List<string> statusMessages = [];
        List<string> errorMessages = [];
        
        Dictionary<string, string> schema = new()
        {
            ["test_col"] = "TEXT"
        };

        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(() =>
        {
            GeopackageLayerCreateHelper.CreateGeopackageLayer(
                invalidPath, 
                "test_layer", 
                schema,
                onStatus: msg => statusMessages.Add(msg),
                onError: msg => errorMessages.Add(msg));
        });

        Assert.IsTrue(exception.Message.Contains("GeoPackage file not found"), 
            "Should throw FileNotFoundException for invalid paths");
        Assert.IsTrue(exception.Message.Contains(invalidPath), 
            "Should include the invalid path in error message");
        
        Assert.IsTrue(errorMessages.Count > 0, "Error callback should be invoked");
        Assert.IsTrue(errorMessages.Any(msg => msg.Contains("Error creating GeoPackage layer")), 
            "Should contain error callback message");
    }
}