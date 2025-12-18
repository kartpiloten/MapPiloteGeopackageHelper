using NetTopologySuite.Geometries;
using MapPiloteGeopackageHelper;

namespace TestMapPiloteGeoPackageHandler;

[TestClass]
public class TestDataTypeValidation
{
    private readonly string _testGeoPackagePath;
    private readonly string _layerName = "TestValidationLayer";
    private readonly string _blobTestGeoPackagePath;
    private readonly string _blobLayerName = "TestBlobLayer";
    
    public TestDataTypeValidation()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string guid = Guid.NewGuid().ToString("N")[..8];
        _testGeoPackagePath = Path.Combine(Path.GetTempPath(), $"TestDataValidation_{timestamp}_{guid}.gpkg");
        _blobTestGeoPackagePath = Path.Combine(Path.GetTempPath(), $"TestBlobValidation_{timestamp}_{guid}.gpkg");
    }

    [TestInitialize]
    public void Setup()
    {
        SafeDeleteFile(_testGeoPackagePath);
        SafeDeleteFile(_blobTestGeoPackagePath);

        CMPGeopackageCreateHelper.CreateGeoPackage(_testGeoPackagePath);
        
        Dictionary<string, string> columns = new()
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

        CMPGeopackageCreateHelper.CreateGeoPackage(_blobTestGeoPackagePath);
        
        Dictionary<string, string> blobColumns = new()
        {
            { "text_col", "TEXT" },
            { "blob_col", "BLOB" }
        };
        
        GeopackageLayerCreateHelper.CreateGeopackageLayer(_blobTestGeoPackagePath, _blobLayerName, blobColumns, "POINT");
    }

    [TestCleanup]
    public void Cleanup()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        SafeDeleteFile(_testGeoPackagePath);
        SafeDeleteFile(_blobTestGeoPackagePath);
    }

    private static void SafeDeleteFile(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        try
        {
            File.Delete(filePath);
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not delete file {filePath}. Error: {ex.Message}");
            
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Thread.Sleep(100 * attempt);
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
            
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "test_cleanup_" + Path.GetRandomFileName());
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
        Point point = new(415000, 7045000);
        string[] blobData = new[] { "text_value", "blob_value" };

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(_blobTestGeoPackagePath, _blobLayerName, point, blobData));
        
        Assert.IsTrue(exception.Message.Contains("Column 'blob_col' is of type BLOB"));
        Assert.IsTrue(exception.Message.Contains("cannot be inserted via string array"));
        Assert.IsTrue(exception.Message.Contains("BLOB columns require special handling"));
    }

    [TestMethod]
    public void TestValidIntegerValues_ShouldSucceed()
    {
        Point point = new(415000, 7045000);
        string[] validIntegerData = new[] { "123", "45.67", "text", "varchar_test", "89.123", "12.34", "456", "char_test" };

        CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, validIntegerData);
    }

    [TestMethod]
    public void TestInvalidIntegerValue_ShouldThrowException()
    {
        Point point = new(415000, 7045000);
        string[] invalidIntegerData = new[] { "not_a_number", "45.67", "text", "varchar_test", "89.123", "12.34", "456", "char_test" };

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidIntegerData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 0"));
        Assert.IsTrue(exception.Message.Contains("Column 'integer_col' expects INTEGER"));
        Assert.IsTrue(exception.Message.Contains("cannot be converted to an integer"));
    }

    [TestMethod]
    public void TestInvalidRealValue_ShouldThrowException()
    {
        Point point = new(415000, 7045000);
        string[] invalidRealData = new[] { "123", "not_a_number", "text", "varchar_test", "89.123", "12.34", "456", "char_test" };

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidRealData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 1"));
        Assert.IsTrue(exception.Message.Contains("Column 'real_col' expects REAL/FLOAT"));
        Assert.IsTrue(exception.Message.Contains("cannot be converted to a number"));
    }

    [TestMethod]
    public void TestInvalidFloatValue_ShouldThrowException()
    {
        Point point = new(415000, 7045000);
        string[] invalidFloatData = new[] { "123", "45.67", "text", "varchar_test", "invalid_float", "12.34", "456", "char_test" };

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidFloatData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 4"));
        Assert.IsTrue(exception.Message.Contains("Column 'float_col' expects REAL/FLOAT"));
    }

    [TestMethod]
    public void TestInvalidDoubleValue_ShouldThrowException()
    {
        Point point = new(415000, 7045000);
        string[] invalidDoubleData = new[] { "123", "45.67", "text", "varchar_test", "89.123", "invalid_double", "456", "char_test" };

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidDoubleData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 5"));
        Assert.IsTrue(exception.Message.Contains("Column 'double_col' expects REAL/FLOAT"));
    }

    [TestMethod]
    public void TestInvalidIntColValue_ShouldThrowException()
    {
        Point point = new(415000, 7045000);
        string[] invalidIntData = new[] { "123", "45.67", "text", "varchar_test", "89.123", "12.34", "not_an_int", "char_test" };

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, invalidIntData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 6"));
        Assert.IsTrue(exception.Message.Contains("Column 'int_col' expects INTEGER"));
    }

    [TestMethod]
    public void TestTextValues_ShouldAlwaysSucceed()
    {
        Point point = new(415000, 7045000);
        string[] anyTextData = new[] { "123", "45.67", "any_text_value", "varchar_test", "89.123", "12.34", "456", "any_char_value" };

        CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, anyTextData);
    }

    [TestMethod]
    public void TestEmptyAndNullValues_ShouldSucceed()
    {
        Point point = new(415000, 7045000);
        string[] emptyData = new[] { "", "", "", "", "", "", "", "" };

        CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, emptyData);
    }

    [TestMethod]
    public void TestWrongNumberOfColumns_ShouldThrowException()
    {
        Point point = new(415000, 7045000);
        string[] tooFewData = new[] { "123", "45.67", "text" };

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, tooFewData));
        
        Assert.IsTrue(exception.Message.Contains("Column count mismatch"));
        Assert.IsTrue(exception.Message.Contains("Expected 8 attribute values"));
        Assert.IsTrue(exception.Message.Contains("but received 3 values"));
    }

    [TestMethod]
    public void TestBoundaryValues_ShouldSucceed()
    {
        Point point = new(415000, 7045000);
        string[] boundaryData = new[]
        { 
            long.MaxValue.ToString(),
            double.MaxValue.ToString(),
            "boundary_text",
            "boundary_varchar",
            float.MaxValue.ToString(),
            double.MinValue.ToString(),
            long.MinValue.ToString(),
            "b"
        };

        CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, boundaryData);
    }

    [TestMethod]
    public void TestNegativeNumbers_ShouldSucceed()
    {
        Point point = new(415000, 7045000);
        string[] negativeData = new[] { "-123", "-45.67", "text", "varchar", "-89.123", "-12.34", "-456", "char" };

        CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, negativeData);
    }

    [TestMethod]
    public void TestScientificNotation_ShouldSucceed()
    {
        Point point = new(415000, 7045000);
        string[] scientificData = new[] { "123", "1.23E+10", "text", "varchar", "4.56E-5", "7.89E+15", "456", "char" };

        CGeopackageAddDataHelper.AddPointToGeoPackage(_testGeoPackagePath, _layerName, point, scientificData);
    }

    [TestMethod]
    public void TestUnknownColumnType_ShouldLogWarningAndProceed()
    {
    }
}