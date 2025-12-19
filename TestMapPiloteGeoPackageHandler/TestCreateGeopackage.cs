using NetTopologySuite.Geometries;
using MapPiloteGeopackageHelper;

namespace TestMapPiloteGeoPackageHandler;

/// <summary>
/// Tests for basic GeoPackage creation.
/// Output files are saved to TestResults/GeoPackages folder for inspection in QGIS.
/// </summary>
[TestClass]
public sealed class TestCreateGeoPackage
{
    [TestMethod]
    public void TestMethod1SimpleCreate()
    {
        string filePath = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(filePath);

        Assert.IsTrue(File.Exists(filePath), "GeoPackage file was not created.");
        TestOutputHelper.LogOutputLocation(filePath);
    }

    [TestMethod]
    public void TestMethod2CreateAddPoint()
    {
        string filePath = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(filePath);

        Dictionary<string, string> allColumns = new()
        {
            { "test1integer", "INTEGER" },
            { "test2integer", "INTEGER" }
        };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(filePath, "MyTestPoint", allColumns, "POINT");

        Point point = new(415000, 7045000);
        string[] attributdata = ["4211", "42"];
        CGeopackageAddDataHelper.AddPointToGeoPackage(filePath, "MyTestPoint", point, attributdata);

        Assert.IsTrue(File.Exists(filePath), "GeoPackage file was not created.");
        TestOutputHelper.LogOutputLocation(filePath);
    }
}
