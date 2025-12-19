using NetTopologySuite.Geometries;
using MapPiloteGeopackageHelper;
using System.Runtime.CompilerServices;

namespace TestMapPiloteGeoPackageHandler;

/// <summary>
/// Tests for data type validation.
/// Output files are saved to TestResults/GeoPackages folder for inspection in QGIS.
/// </summary>
[TestClass]
public class TestDataTypeValidation
{
    private static string GetTestPath(string suffix, [CallerMemberName] string testMethod = "")
    {
        return TestOutputHelper.GetTestOutputPath(suffix, testMethod);
    }

    private static (string mainPath, string blobPath) SetupGeoPackages([CallerMemberName] string testMethod = "")
    {
        string mainPath = TestOutputHelper.GetTestOutputPath("main", testMethod);
        string blobPath = TestOutputHelper.GetTestOutputPath("blob", testMethod);

        CMPGeopackageCreateHelper.CreateGeoPackage(mainPath);
        
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
        
        GeopackageLayerCreateHelper.CreateGeopackageLayer(mainPath, "TestValidationLayer", columns, "POINT");

        CMPGeopackageCreateHelper.CreateGeoPackage(blobPath);
        
        Dictionary<string, string> blobColumns = new()
        {
            { "text_col", "TEXT" },
            { "blob_col", "BLOB" }
        };
        
        GeopackageLayerCreateHelper.CreateGeopackageLayer(blobPath, "TestBlobLayer", blobColumns, "POINT");

        return (mainPath, blobPath);
    }

    [TestMethod]
    public void TestBlobColumn_ShouldThrowException()
    {
        var (_, blobPath) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] blobData = ["text_value", "blob_value"];

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(blobPath, "TestBlobLayer", point, blobData));
        
        Assert.IsTrue(exception.Message.Contains("Column 'blob_col' is of type BLOB"));
        Assert.IsTrue(exception.Message.Contains("cannot be inserted via string array"));
        Assert.IsTrue(exception.Message.Contains("BLOB columns require special handling"));

        TestOutputHelper.LogOutputLocation(blobPath);
    }

    [TestMethod]
    public void TestValidIntegerValues_ShouldSucceed()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] validIntegerData = ["123", "45.67", "text", "varchar_test", "89.123", "12.34", "456", "char_test"];

        CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, validIntegerData);
        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestInvalidIntegerValue_ShouldThrowException()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] invalidIntegerData = ["not_a_number", "45.67", "text", "varchar_test", "89.123", "12.34", "456", "char_test"];

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, invalidIntegerData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 0"));
        Assert.IsTrue(exception.Message.Contains("Column 'integer_col' expects INTEGER"));
        Assert.IsTrue(exception.Message.Contains("cannot be converted to an integer"));

        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestInvalidRealValue_ShouldThrowException()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] invalidRealData = ["123", "not_a_number", "text", "varchar_test", "89.123", "12.34", "456", "char_test"];

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, invalidRealData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 1"));
        Assert.IsTrue(exception.Message.Contains("Column 'real_col' expects REAL/FLOAT"));
        Assert.IsTrue(exception.Message.Contains("cannot be converted to a number"));

        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestInvalidFloatValue_ShouldThrowException()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] invalidFloatData = ["123", "45.67", "text", "varchar_test", "invalid_float", "12.34", "456", "char_test"];

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, invalidFloatData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 4"));
        Assert.IsTrue(exception.Message.Contains("Column 'float_col' expects REAL/FLOAT"));

        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestInvalidDoubleValue_ShouldThrowException()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] invalidDoubleData = ["123", "45.67", "text", "varchar_test", "89.123", "invalid_double", "456", "char_test"];

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, invalidDoubleData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 5"));
        Assert.IsTrue(exception.Message.Contains("Column 'double_col' expects REAL/FLOAT"));

        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestInvalidIntColValue_ShouldThrowException()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] invalidIntData = ["123", "45.67", "text", "varchar_test", "89.123", "12.34", "not_an_int", "char_test"];

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, invalidIntData));
        
        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 6"));
        Assert.IsTrue(exception.Message.Contains("Column 'int_col' expects INTEGER"));

        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestTextValues_ShouldAlwaysSucceed()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] anyTextData = ["123", "45.67", "any_text_value", "varchar_test", "89.123", "12.34", "456", "any_char_value"];

        CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, anyTextData);
        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestEmptyAndNullValues_ShouldSucceed()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] emptyData = ["", "", "", "", "", "", "", ""];

        CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, emptyData);
        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestWrongNumberOfColumns_ShouldThrowException()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] tooFewData = ["123", "45.67", "text"];

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, tooFewData));
        
        Assert.IsTrue(exception.Message.Contains("Column count mismatch"));
        Assert.IsTrue(exception.Message.Contains("Expected 8 attribute values"));
        Assert.IsTrue(exception.Message.Contains("but received 3 values"));

        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestBoundaryValues_ShouldSucceed()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] boundaryData =
        [ 
            long.MaxValue.ToString(),
            double.MaxValue.ToString(),
            "boundary_text",
            "boundary_varchar",
            float.MaxValue.ToString(),
            double.MinValue.ToString(),
            long.MinValue.ToString(),
            "b"
        ];

        CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, boundaryData);
        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestNegativeNumbers_ShouldSucceed()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] negativeData = ["-123", "-45.67", "text", "varchar", "-89.123", "-12.34", "-456", "char"];

        CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, negativeData);
        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestScientificNotation_ShouldSucceed()
    {
        var (mainPath, _) = SetupGeoPackages();
        Point point = new(415000, 7045000);
        string[] scientificData = ["123", "1.23E+10", "text", "varchar", "4.56E-5", "7.89E+15", "456", "char"];

        CGeopackageAddDataHelper.AddPointToGeoPackage(mainPath, "TestValidationLayer", point, scientificData);
        TestOutputHelper.LogOutputLocation(mainPath);
    }

    [TestMethod]
    public void TestUnknownColumnType_ShouldLogWarningAndProceed()
    {
        // This test verifies that unknown column types don't cause crashes
        // The test is currently a placeholder
    }
}