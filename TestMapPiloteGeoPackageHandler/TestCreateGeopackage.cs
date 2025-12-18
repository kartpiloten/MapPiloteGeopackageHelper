using System.IO;
using NetTopologySuite.Geometries;
using MapPiloteGeopackageHelper;

namespace TestMapPiloteGeoPackageHandler;

[TestClass]
public sealed class TestCreateGeoPackage
{
    [TestMethod]
    public void TestMethod1SimpleCreate()
    {
        string filePath = @"C:\temp\1SimpleCreate.gpkg";
        if (File.Exists(filePath))
            File.Delete(filePath);

        CMPGeopackageCreateHelper.CreateGeoPackage(filePath);

        Assert.IsTrue(File.Exists(filePath), "GeoPackage file was not created.");
    }

    [TestMethod]
    public void TestMethod2CreateAddPoint()
    {
        string filePath = @"C:\temp\2CreateAndAddPoint.gpkg";
        if (File.Exists(filePath))
            File.Delete(filePath);

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
    }
}
