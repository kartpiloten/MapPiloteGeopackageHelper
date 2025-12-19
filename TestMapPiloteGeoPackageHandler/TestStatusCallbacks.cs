using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler;

/// <summary>
/// Tests for status callbacks.
/// Output files are saved to TestResults/GeoPackages folder for inspection in QGIS.
/// </summary>
[TestClass]
public class TestStatusCallbacks
{
    [TestMethod]
    public void CMPGeopackageCreateHelper_WithStatusCallback_ShouldInvokeCallback()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(
            gpkg, 
            onStatus: msg => statusMessages.Add(msg));

        Assert.IsTrue(statusMessages.Count > 0, "No status messages were received");
        Assert.IsTrue(statusMessages.Any(msg => msg.Contains("Successfully created GeoPackage")), 
            "Expected success message not found");
        Assert.IsTrue(File.Exists(gpkg), "GeoPackage file was not created");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void GeopackageLayerCreateHelper_WithStatusCallback_ShouldInvokeCallback()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        List<string> errorMessages = [];
        
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
        
        Dictionary<string, string> schema = new()
        {
            ["test_col"] = "TEXT"
        };

        GeopackageLayerCreateHelper.CreateGeopackageLayer(
            gpkg, 
            "test_layer", 
            schema,
            onStatus: msg => statusMessages.Add(msg),
            onError: msg => errorMessages.Add(msg));

        Assert.IsTrue(statusMessages.Count > 0, "No status messages were received");
        Assert.IsTrue(statusMessages.Any(msg => msg.Contains("Successfully created spatial layer")), 
            "Expected success message not found");
        Assert.AreEqual(0, errorMessages.Count, "Unexpected error messages received");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void AddPointToGeoPackage_WithWarningCallback_ShouldInvokeOnUnknownType()
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
        string[] attributes = ["Test", "42"];

        CGeopackageAddDataHelper.AddPointToGeoPackage(
            gpkg, 
            "test_layer", 
            point, 
            attributes,
            onWarning: msg => warningMessages.Add(msg));

        Assert.AreEqual(0, warningMessages.Count, "No warnings should be generated for valid data");
        
        List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer").ToList();
        Assert.AreEqual(1, features.Count, "Point should have been inserted");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public async Task FluentAPI_WithStatusCallback_ShouldInvokeCallback()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        List<string> statusMessages = [];
        
        using GeoPackage geoPackage = await GeoPackage.OpenAsync(
            gpkg, 
            onStatus: msg => statusMessages.Add(msg));

        Assert.IsTrue(statusMessages.Count > 0, "No status messages were received from fluent API");
        Assert.IsTrue(statusMessages.Any(msg => msg.Contains("Successfully initialized GeoPackage")), 
            "Expected initialization message not found");
        Assert.IsTrue(File.Exists(gpkg), "GeoPackage file was not created by fluent API");
        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void WithoutCallbacks_ShouldStillWork()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();
        
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
        
        Dictionary<string, string> schema = new()
        {
            ["name"] = "TEXT"
        };
        
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", schema);
        
        Point point = new(100, 200);
        string[] attributes = ["Test"];
        
        CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "test_layer", point, attributes);

        Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created without callbacks");
        
        List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer").ToList();
        Assert.AreEqual(1, features.Count, "Point should be inserted without callbacks");
        TestOutputHelper.LogOutputLocation(gpkg);
    }
}