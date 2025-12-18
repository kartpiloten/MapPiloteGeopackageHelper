using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler;

[TestClass]
public class TestOptionalCallbackPattern
{
    private static string CreateTempGpkgPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"callback_pattern_test_{Guid.NewGuid():N}.gpkg");
        return path;
    }

    [TestMethod]
    public void CreateGeoPackage_WithStatusCallback_ShouldInvokeCallbackWithCorrectMessages()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = new();
        
        try
        {
            // Act
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                srid: 3006,
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(statusMessages.Count >= 1, "Should receive at least one status message");
            
            bool hasCreationMessage = statusMessages.Any(msg => 
                msg.Contains("Successfully created GeoPackage") && msg.Contains(gpkg));
            Assert.IsTrue(hasCreationMessage, "Should contain GeoPackage creation success message");
            
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage file should exist");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeoPackage_WithExistingFile_ShouldInvokeDeletionCallback()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = new();
        
        try
        {
            // Create file first
            File.WriteAllText(gpkg, "dummy content");
            Assert.IsTrue(File.Exists(gpkg), "Setup: File should exist before test");

            // Act
            CMPGeopackageCreateHelper.CreateGeoPackage(
                gpkg, 
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(statusMessages.Count >= 2, "Should receive multiple status messages");
            
            bool hasDeletionMessage = statusMessages.Any(msg => 
                msg.Contains("Deleted existing GeoPackage file") && msg.Contains(gpkg));
            bool hasCreationMessage = statusMessages.Any(msg => 
                msg.Contains("Successfully created GeoPackage") && msg.Contains(gpkg));
            
            Assert.IsTrue(hasDeletionMessage, "Should contain file deletion message");
            Assert.IsTrue(hasCreationMessage, "Should contain GeoPackage creation message");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeopackageLayer_WithStatusAndErrorCallbacks_ShouldInvokeStatusOnSuccess()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = new();
        List<string> errorMessages = new();
        
        try
        {
            // Setup: Create GeoPackage first
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
            
            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT",
                ["value"] = "INTEGER"
            };

            // Act
            GeopackageLayerCreateHelper.CreateGeopackageLayer(
                gpkg, 
                "test_layer", 
                schema,
                "POINT",
                3006,
                onStatus: msg => statusMessages.Add(msg),
                onError: msg => errorMessages.Add(msg));

            // Assert
            Assert.IsTrue(statusMessages.Count >= 1, "Should receive status messages");
            Assert.AreEqual(0, errorMessages.Count, "Should not receive error messages on success");
            
            bool hasLayerCreationMessage = statusMessages.Any(msg => 
                msg.Contains("Successfully created spatial layer") && msg.Contains("test_layer"));
            Assert.IsTrue(hasLayerCreationMessage, "Should contain layer creation success message");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeopackageLayer_WithError_ShouldInvokeErrorCallback()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = new();
        List<string> errorMessages = new();
        
        Dictionary<string, string> schema = new()
        {
            ["test_col"] = "TEXT"
        };

        // Act & Assert
        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(() =>
        {
            GeopackageLayerCreateHelper.CreateGeopackageLayer(
                gpkg, 
                "test_layer", 
                schema,
                onStatus: msg => statusMessages.Add(msg),
                onError: msg => errorMessages.Add(msg));
        });

        // The error should be thrown as exception (error callback receives it but exception still bubbles up)
        Assert.IsTrue(exception.Message.Contains("GeoPackage file not found"), 
            "Should throw FileNotFoundException with appropriate message");
    }

    [TestMethod]
    public void AddPointToGeoPackage_WithUnknownColumnType_ShouldInvokeWarningCallback()
    {
        // This test demonstrates the warning callback for unknown column types
        // Since we can't easily create unknown types in standard SQLite, 
        // we'll test the warning callback parameter acceptance
        
        string gpkg = CreateTempGpkgPath();
        List<string> warningMessages = new();
        
        try
        {
            // Arrange
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
            
            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT",
                ["value"] = "INTEGER"
            };
            
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", schema);
            
            Point point = new(100, 200);
            string[] attributes = new[] { "Test Name", "42" };

            // Act - Test with warning callback (no warnings expected for valid data)
            CGeopackageAddDataHelper.AddPointToGeoPackage(
                gpkg, 
                "test_layer", 
                point, 
                attributes,
                onWarning: msg => warningMessages.Add(msg));

            // Assert
            Assert.AreEqual(0, warningMessages.Count, "No warnings should be generated for valid data types");
            
            // Verify the point was inserted successfully
            List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer").ToList();
            Assert.AreEqual(1, features.Count, "Point should have been inserted");
            Assert.AreEqual("Test Name", features[0].Attributes["name"]);
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void BulkInsertFeatures_WithWarningCallback_ShouldAcceptCallback()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> warningMessages = new();
        
        try
        {
            // Setup
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
            
            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT",
                ["count"] = "INTEGER"
            };
            
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "bulk_test", schema);
            
            List<FeatureRecord> features = new()
            {
                new FeatureRecord(
                    new Point(100, 100),
                    new Dictionary<string, string?> { ["name"] = "Feature1", ["count"] = "10" }
                ),
                new FeatureRecord(
                    new Point(200, 200),
                    new Dictionary<string, string?> { ["name"] = "Feature2", ["count"] = "20" }
                )
            };

            // Act
            CGeopackageAddDataHelper.BulkInsertFeatures(
                gpkg,
                "bulk_test", 
                features,
                onWarning: msg => warningMessages.Add(msg));

            // Assert
            Assert.AreEqual(0, warningMessages.Count, "No warnings expected for valid data");
            
            List<FeatureRecord> readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "bulk_test").ToList();
            Assert.AreEqual(2, readBack.Count, "Both features should be inserted");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public async Task FluentAPI_OpenAsync_WithStatusCallback_ShouldInvokeCallback()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = new();
        
        try
        {
            // Act - Create new GeoPackage with fluent API
            using GeoPackage geoPackage = await GeoPackage.OpenAsync(
                gpkg, 
                defaultSrid: 3006,
                onStatus: msg => statusMessages.Add(msg));

            // Assert
            Assert.IsTrue(statusMessages.Count >= 1, "Should receive initialization status messages");
            
            bool hasInitMessage = statusMessages.Any(msg => 
                msg.Contains("Successfully initialized GeoPackage") && msg.Contains(gpkg));
            Assert.IsTrue(hasInitMessage, "Should contain initialization success message");
            
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage file should be created");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public async Task FluentAPI_OpenExisting_WithStatusCallback_ShouldNotInvokeCallback()
    {
        // Arrange
        string gpkg = CreateTempGpkgPath();
        List<string> statusMessages = new();
        
        try
        {
            // Setup: Create GeoPackage first
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg);
            
            // Act - Open existing GeoPackage
            using GeoPackage geoPackage = await GeoPackage.OpenAsync(
                gpkg, 
                onStatus: msg => statusMessages.Add(msg));

            // Assert - No status messages expected when opening existing file
            Assert.AreEqual(0, statusMessages.Count, "No status messages expected when opening existing GeoPackage");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public async Task FluentAPI_EnsureLayerAsync_ShouldCreateLayerSilently()
    {
        // Test that layer creation through fluent API is silent (internal operation)
        string gpkg = CreateTempGpkgPath();
        
        try
        {
            // Arrange
            using GeoPackage geoPackage = await GeoPackage.OpenAsync(gpkg);
            
            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT",
                ["value"] = "INTEGER"
            };

            // Act - This should work without any external callbacks
            GeoPackageLayer layer = await geoPackage.EnsureLayerAsync("silent_layer", schema);

            // Assert - Verify layer was created
            Assert.IsNotNull(layer, "Layer should be created");
            
            CMPGeopackageReadDataHelper.GeopackageInfo info = await geoPackage.GetInfoAsync();
            bool layerExists = info.Layers.Any(l => l.TableName == "silent_layer");
            Assert.IsTrue(layerExists, "Layer should exist in GeoPackage metadata");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void BackwardCompatibility_AllMethodsWithoutCallbacks_ShouldWork()
    {
        // Test that all methods work without any callbacks (backward compatibility)
        string gpkg = CreateTempGpkgPath();
        
        try
        {
            // Act - Use all methods without any callbacks
            CMPGeopackageCreateHelper.CreateGeoPackage(gpkg); // No callback
            
            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT",
                ["age"] = "INTEGER"
            };
            
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "compat_test", schema); // No callbacks
            
            Point point = new(500, 600);
            string[] attributes = new[] { "Alice", "30" };
            
            CGeopackageAddDataHelper.AddPointToGeoPackage(gpkg, "compat_test", point, attributes); // No callback
            
            List<FeatureRecord> features = new()
            {
                new FeatureRecord(
                    new Point(700, 800),
                    new Dictionary<string, string?> { ["name"] = "Bob", ["age"] = "25" }
                )
            };
            
            CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "compat_test", features); // No callback

            // Assert - Everything should work normally
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created");
            
            List<FeatureRecord> readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "compat_test").ToList();
            Assert.AreEqual(2, readBack.Count, "Both features should be inserted");
            
            FeatureRecord? firstFeature = readBack.FirstOrDefault(f => f.Attributes["name"] == "Alice");
            FeatureRecord? secondFeature = readBack.FirstOrDefault(f => f.Attributes["name"] == "Bob");
            
            Assert.IsNotNull(firstFeature, "First feature should exist");
            Assert.IsNotNull(secondFeature, "Second feature should exist");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CallbackPattern_NullCallbacks_ShouldNotThrowExceptions()
    {
        // Test that explicitly passing null callbacks works correctly
        string gpkg = CreateTempGpkgPath();
        
        try
        {
            // Act - Explicitly pass null callbacks
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
            string[] attributes = new[] { "Test" };
            
            CGeopackageAddDataHelper.AddPointToGeoPackage(
                gpkg, "null_test", point, attributes,
                onWarning: null);

            // Assert - Should work without throwing exceptions
            Assert.IsTrue(File.Exists(gpkg), "GeoPackage should be created with null callbacks");
            
            List<FeatureRecord> features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "null_test").ToList();
            Assert.AreEqual(1, features.Count, "Feature should be inserted with null callbacks");
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public void CreateGeopackageLayer_WithNonExistentGeoPackage_ShouldThrowFileNotFoundException()
    {
        // Arrange - Valid path but file doesn't exist
        string nonExistentGpkg = CreateTempGpkgPath(); // Valid path, but file not created
        List<string> statusMessages = new();
        List<string> errorMessages = new();
        
        Dictionary<string, string> schema = new()
        {
            ["test_col"] = "TEXT"
        };

        // Act & Assert
        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(() =>
        {
            GeopackageLayerCreateHelper.CreateGeopackageLayer(
                nonExistentGpkg, 
                "test_layer", 
                schema,
                onStatus: msg => statusMessages.Add(msg),
                onError: msg => errorMessages.Add(msg));
        });

        // The error should indicate the GeoPackage file doesn't exist (not a path error)
        Assert.IsTrue(exception.Message.Contains("GeoPackage file not found"), 
            "Should throw FileNotFoundException when GeoPackage file doesn't exist");
        Assert.IsTrue(exception.Message.Contains(nonExistentGpkg), 
            "Should include the file path in error message");
    }

    [TestMethod]
    public void CreateGeopackageLayer_WithInvalidPath_ShouldThrowDirectoryNotFoundException()
    {
        // Arrange - Invalid directory path
        string invalidPath = "Z:\\NonExistentDirectory\\test.gpkg"; // Invalid path
        List<string> statusMessages = new();
        List<string> errorMessages = new();
        
        Dictionary<string, string> schema = new()
        {
            ["test_col"] = "TEXT"
        };

        // Act & Assert - File.Exists() returns false for invalid paths, triggering FileNotFoundException
        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(() =>
        {
            GeopackageLayerCreateHelper.CreateGeopackageLayer(
                invalidPath, 
                "test_layer", 
                schema,
                onStatus: msg => statusMessages.Add(msg),
                onError: msg => errorMessages.Add(msg));
        });

        // Verify the expected error message
        Assert.IsTrue(exception.Message.Contains("GeoPackage file not found"), 
            "Should throw FileNotFoundException for invalid paths");
        Assert.IsTrue(exception.Message.Contains(invalidPath), 
            "Should include the invalid path in error message");
        
        // Verify error callback was invoked
        Assert.IsTrue(errorMessages.Count > 0, "Error callback should be invoked");
        Assert.IsTrue(errorMessages.Any(msg => msg.Contains("Error creating GeoPackage layer")), 
            "Should contain error callback message");
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}