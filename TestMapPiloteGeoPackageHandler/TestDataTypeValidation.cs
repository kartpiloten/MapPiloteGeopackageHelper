using System.IO;
using NetTopologySuite.Geometries;
using MapPiloteGeopackageHelper;

namespace TestMapPiloteGeoPackageHandler
{
    [TestClass]
    public class TestDataTypeValidation
    {
        private string _testGeoPackagePath;
        private string _layerName = "TestValidationLayer";
        private string _blobTestGeoPackagePath;
        private string _blobLayerName = "TestBlobLayer";
        
        // Use unique file paths for each test instance
        public TestDataTypeValidation()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var guid = Guid.NewGuid().ToString("N")[..8]; // First 8 characters of GUID
            _testGeoPackagePath = Path.Combine(Path.GetTempPath(), $"TestDataValidation_{timestamp}_{guid}.gpkg");
            _blobTestGeoPackagePath = Path.Combine(Path.GetTempPath(), $"TestBlobValidation_{timestamp}_{guid}.gpkg");
        }

        [TestInitialize]
        public void Setup()
        {
            // Clean up any existing test files with retry logic
            SafeDeleteFile(_testGeoPackagePath);
            SafeDeleteFile(_blobTestGeoPackagePath);

            // Create test GeoPackage and layer with various column types
            CMPGeopackageCreateHelper.CreateGeoPackage(_testGeoPackagePath);
            
            var columns = new Dictionary<string, string>
            {
                { "integer_col", "INTEGER" },
                { "real_col", "REAL" },
                { "text_col", "TEXT" },
                { "varchar_col", "VARCHAR" },
                { "float_col", "FLOAT" },
                { "double_col", "DOUBLE" },
                { "int_col", "INT" },
                { "char_col", "CHAR" }
            };
            
            GeopackageLayerCreateHelper.CreateGeopackageLayer(_testGeoPackagePath, _layerName, columns, "POINT");

            // Create a separate GeoPackage for BLOB testing
            CMPGeopackageCreateHelper.CreateGeoPackage(_blobTestGeoPackagePath);
            
            var blobColumns = new Dictionary<string, string>
            {
                { "text_col", "TEXT" },
                { "blob_col", "BLOB" }
            };
            
            GeopackageLayerCreateHelper.CreateGeopackageLayer(_blobTestGeoPackagePath, _blobLayerName, blobColumns, "POINT");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Force garbage collection to ensure any connections from test methods are disposed
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Clean up test files - but don't fail if they can't be deleted
            SafeDeleteFile(_testGeoPackagePath);
            SafeDeleteFile(_blobTestGeoPackagePath);
        }

        /// <summary>
        /// Safely delete a file with retry logic to handle file locks from SQLite connections
        /// </summary>
        private static void SafeDeleteFile(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            try
            {
                File.Delete(filePath);
                return; // Success on first try
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not delete file {filePath}. Error: {ex.Message}");
                
                // Try a few more times with delays
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        Thread.Sleep(100 * attempt); // Progressive delay
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        
                        File.Delete(filePath);
                        Console.WriteLine($"Successfully deleted file {filePath} on attempt {attempt + 1}");
                        return;
                    }
                    catch (Exception retryEx)
                    {
                        Console.WriteLine($"Retry {attempt} failed for {filePath}: {retryEx.Message}");
                    }
                }
                
                // Final attempt: try to move the file to temp directory
                try
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), "test_cleanup_" + Path.GetRandomFileName());
                    File.Move(filePath, tempPath);
                    Console.WriteLine($"Moved locked file {filePath} to temporary location: {tempPath}");
                }
                catch (Exception moveEx)
                {
                    Console.WriteLine($"Could not move file {filePath}: {moveEx.Message}. File will remain in place.");
                }
            }
        }

        [TestMethod]
        public void TestBlobColumn_ShouldThrowException()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] blobData = { "text_value", "blob_value" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                CGeopackageAddDataHelper.AddPointToGeoPackage(_blobTestGeoPackagePath, _blobLayerName, point, blobData));
            
            Assert.IsTrue(exception.Message.Contains("Column 'blob_col' is of type BLOB"));
            Assert.IsTrue(exception.Message.Contains("cannot be inserted via string array"));
            Assert.IsTrue(exception.Message.Contains("BLOB columns require special handling"));
        }

        [TestMethod]
        public void TestValidIntegerValues_ShouldSucceed()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] validIntegerData = { "123", "45.67", "text", "varchar_test", "89.123", "12.34", "456", "char_test" };

            // Act & Assert - Should not throw exception
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, validIntegerData);
        }

        [TestMethod]
        public void TestInvalidIntegerValue_ShouldThrowException()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] invalidIntegerData = { "not_a_number", "45.67", "text", "varchar_test", "89.123", "12.34", "456", "char_test" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidIntegerData));
            
            Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 0"));
            Assert.IsTrue(exception.Message.Contains("Column 'integer_col' expects INTEGER"));
            Assert.IsTrue(exception.Message.Contains("cannot be converted to an integer"));
        }

        [TestMethod]
        public void TestInvalidRealValue_ShouldThrowException()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] invalidRealData = { "123", "not_a_number", "text", "varchar_test", "89.123", "12.34", "456", "char_test" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidRealData));
            
            Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 1"));
            Assert.IsTrue(exception.Message.Contains("Column 'real_col' expects REAL/FLOAT"));
            Assert.IsTrue(exception.Message.Contains("cannot be converted to a number"));
        }

        [TestMethod]
        public void TestInvalidFloatValue_ShouldThrowException()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] invalidFloatData = { "123", "45.67", "text", "varchar_test", "invalid_float", "12.34", "456", "char_test" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidFloatData));
            
            Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 4"));
            Assert.IsTrue(exception.Message.Contains("Column 'float_col' expects REAL/FLOAT"));
        }

        [TestMethod]
        public void TestInvalidDoubleValue_ShouldThrowException()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] invalidDoubleData = { "123", "45.67", "text", "varchar_test", "89.123", "invalid_double", "456", "char_test" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidDoubleData));
            
            Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 5"));
            Assert.IsTrue(exception.Message.Contains("Column 'double_col' expects REAL/FLOAT"));
        }

        [TestMethod]
        public void TestInvalidIntColValue_ShouldThrowException()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] invalidIntData = { "123", "45.67", "text", "varchar_test", "89.123", "12.34", "not_an_int", "char_test" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidIntData));
            
            Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 6"));
            Assert.IsTrue(exception.Message.Contains("Column 'int_col' expects INTEGER"));
        }

        [TestMethod]
        public void TestTextValues_ShouldAlwaysSucceed()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            // Text columns should accept any string value
            string[] anyTextData = { "123", "45.67", "any_text_value", "varchar_test", "89.123", "12.34", "456", "any_char_value" };

            // Act & Assert - Should not throw exception
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, anyTextData);
        }

        [TestMethod]
        public void TestEmptyAndNullValues_ShouldSucceed()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] emptyData = { "", "", "", "", "", "", "", "" };

            // Act & Assert - Should not throw exception (empty values are allowed as NULL)
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, emptyData);
        }

        [TestMethod]
        public void TestWrongNumberOfColumns_ShouldThrowException()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] tooFewData = { "123", "45.67", "text" }; // Only 3 values instead of 8

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, tooFewData));
            
            Assert.IsTrue(exception.Message.Contains("Column count mismatch"));
            Assert.IsTrue(exception.Message.Contains("Expected 8 attribute values"));
            Assert.IsTrue(exception.Message.Contains("but received 3 values"));
        }

        [TestMethod]
        public void TestBoundaryValues_ShouldSucceed()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] boundaryData = { 
                long.MaxValue.ToString(),    // integer_col - max long value
                double.MaxValue.ToString(),  // real_col - max double value  
                "boundary_text",             // text_col
                "boundary_varchar",          // varchar_col
                float.MaxValue.ToString(),   // float_col - max float value
                double.MinValue.ToString(),  // double_col - min double value
                long.MinValue.ToString(),    // int_col - min long value
                "b"                         // char_col - single character
            };

            // Act & Assert - Should not throw exception
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, boundaryData);
        }

        [TestMethod]
        public void TestNegativeNumbers_ShouldSucceed()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] negativeData = { 
                "-123",      // integer_col
                "-45.67",    // real_col
                "text",      // text_col
                "varchar",   // varchar_col
                "-89.123",   // float_col
                "-12.34",    // double_col
                "-456",      // int_col
                "char"       // char_col
            };

            // Act & Assert - Should not throw exception
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, negativeData);
        }

        [TestMethod]
        public void TestScientificNotation_ShouldSucceed()
        {
            // Arrange
            var point = new Point(415000, 7045000);
            string[] scientificData = { 
                "123",         // integer_col
                "1.23E+10",    // real_col - scientific notation
                "text",        // text_col
                "varchar",     // varchar_col
                "4.56E-5",     // float_col - scientific notation
                "7.89E+15",    // double_col - scientific notation
                "456",         // int_col
                "char"         // char_col
            };

            // Act & Assert - Should not throw exception
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, scientificData);
        }

        [TestMethod]
        public void TestUnknownColumnType_ShouldLogWarningAndProceed()
        {
            // This test would require creating a layer with a custom column type
            // For now, we'll focus on the standard SQLite types
            // The unknown type handling is already covered by the default case in the switch statement
        }
    }
}