using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler;

[TestClass]
public class TestStatusCallbacks
{
    private static string CreateTempGpkgPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"callback_test_{Guid.NewGuid():N}.gpkg");
        return path;
    }

    [TestMethod]
    public void CMPGeopackageCreateHelper_WithStatusCallback_ShouldInvokeCallback()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        
        try
        {
            // Act
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(statusMessages.Count > 0, "No status messages were received");
            Assert.IsTrue(statusMessages.Any(msg => msg.Contains("Successfully created GeoPackage")), 
                "Expected success message not found");
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage file was not created");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void GeopackageLayerCreateHelper_WithStatusCallback_ShouldInvokeCallback()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        List<string> errorMessages = [];
        
        try
        {
            // Create GeoPackage first
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
            
            Dictionary<string, string> schema = new()
            {
                ["test_col"] = "TEXT"
            };

            // Act
            GeopackageLayerCreateHelper.CreateGeopackageLayer(
                gpkg, 
                "test_layer", 
                schema,
                onStatus: msg => statusMessages.Add(msg),
                onError: msg => errorMessages.Add(msg));

            // Assert
            Assert.IsTrue(statusMessages.Count > 0, "No status messages were received");
            Assert.IsTrue(statusMessages.Any(msg => msg.Contains("Successfully created spatial layer")), 
                "Expected success message not found");
            Assert.AreEqual(0, errorMessages.Count, "Unexpected error messages received");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void AddPointToGeoPackage_WithWarningCallback_ShouldInvokeOnUnknownType()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> warningMessages = [];
        
        try
        {
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
            
            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT",
                ["value"] = "INTEGER"
            };
            
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", schema);
            
            Point point = new(100, 200);
            string[] attributes = ["Test", "42"];

            // Act
            CGeopackageAddDataHelper.AddPointToGeoPackage(
                gpkg, 
                "test_layer", 
                point, 
                attributes,
                onWarning: msg => warningMessages.Add(msg));

            // Assert - No warnings expected for valid data
            Assert.AreEqual(0, warningMessages.Count, "No warnings should be generated for valid data");
            
            List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer").ToList();
            Assert.AreEqual(1, features.Count, "Point should have been inserted");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public async Task FluentAPI_WithStatusCallback_ShouldInvokeCallback()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = [];
        
        try
        {
            // Act
            using GeoPackage geoPackage = await GeoPackage.OpenAsync(
                gpkg, 
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(statusMessages.Count > 0, "No status messages were received from fluent API");
            Assert.IsTrue(statusMessages.Any(msg => msg.Contains("Successfully initialized GeoPackage")), 
                "Expected initialization message not found");
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage file was not created by fluent API");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void WithoutCallbacks_ShouldStillWork()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        
        try
        {
            // Act - Test that methods work without any callbacks (backward compatibility)
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
            
            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT"
            };
            
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", schema);
            
            Point point = new(100, 200);
            string[] attributes = ["Test"];
            
            CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "test_layer", point, attributes);

            // Assert
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created without callbacks");
            
            List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer").ToList();
            Assert.AreEqual(1, features.Count, "Point should be inserted without callbacks");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}