using System.IO;
using MapPiloteGeopackageHelper;

namespace TestMapPiloteGeoPackageHandler;

[TestClass]
public class TestGeopackageInfo
{
    private string _gpkgPath = string.Empty;

    [TestInitialize]
    public void Init()
    {
        string ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        string guid = Guid.NewGuid().ToString("N")[..8];
        _gpkgPath = Path.Combine(Path.GetTempPath(), $"GpkgInfo_{ts}_{guid}.gpkg");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { if (File.Exists(_gpkgPath)) File.Delete(_gpkgPath); } catch { }
    }

    [TestMethod]
    public void CreateFourLayers_And_ReadGeopackageInfo_ShouldMatch()
    {
        // Arrange: create gpkg and 4 layers with different schemas and geometry types
        CMPGeopackageCreateHelper.CreateGeoPackage(_gpkgPath);

        GeopackageLayerCreateHelper.CreateGeopackageLayer(
            _gpkgPath,
            layerName: "points_a",
            tableHeaders: new Dictionary<string, string>
            {
                { "name", "TEXT" },
                { "value", "INTEGER" }
            },
            geometryType: "POINT",
            srid: 3006);

        GeopackageLayerCreateHelper.CreateGeopackageLayer(
            _gpkgPath,
            layerName: "lines_b",
            tableHeaders: new Dictionary<string, string>
            {
                { "length_m", "REAL" }
            },
            geometryType: "LINESTRING",
            srid: 3006);

        GeopackageLayerCreateHelper.CreateGeopackageLayer(
            _gpkgPath,
            layerName: "polygons_c",
            tableHeaders: new Dictionary<string, string>
            {
                { "area", "REAL" },
                { "code", "VARCHAR" }
            },
            geometryType: "POLYGON",
            srid: 3006);

        GeopackageLayerCreateHelper.CreateGeopackageLayer(
            _gpkgPath,
            layerName: "mix_d",
            tableHeaders: new Dictionary<string, string>
            {
                { "blob_col", "BLOB" },
                { "text_col", "TEXT" },
                { "int_col", "INT" }
            },
            geometryType: "MULTIPOINT",
            srid: 3006);

        // Act
        CMPGeopackageReadDataHelper.GeopackageInfo info = CMPGeopackageReadDataHelper.GetGeopackageInfo(_gpkgPath);

        // Assert: 4 layers present
        Assert.AreEqual(4, info.Layers.Count, "Expected exactly 4 user layers in gpkg_contents.");

        string[] layerNames = info.Layers.Select(l => l.TableName).OrderBy(n => n).ToArray();
        CollectionAssert.AreEquivalent(
            new[] { "points_a", "lines_b", "polygons_c", "mix_d" },
            layerNames,
            "Layer names mismatch.");

        // Assert SRS list contains standard entries we insert at creation
        HashSet<int> srsIds = info.SpatialRefSystems.Select(s => s.SrsId).ToHashSet();
        Assert.IsTrue(srsIds.Contains(3006), "Missing SRID 3006.");
        Assert.IsTrue(srsIds.Contains(4326), "Missing SRID 4326.");
        Assert.IsTrue(srsIds.Contains(-1), "Missing SRID -1.");
        Assert.IsTrue(srsIds.Contains(0), "Missing SRID 0.");

        // Per-layer checks
        void AssertLayer(string name, string geomType, int attributeCount)
        {
            CMPGeopackageReadDataHelper.LayerInfo layer = info.Layers.Single(l => l.TableName == name);
            Assert.AreEqual("features", layer.DataType, true, "DataType should be 'features'.");
            Assert.AreEqual(3006, layer.Srid, $"Unexpected SRID for {name}");
            Assert.AreEqual("geom", layer.GeometryColumn, $"Unexpected geometry column for {name}");
            Assert.AreEqual(geomType, layer.GeometryType, $"Unexpected geometry type for {name}");
            Assert.AreEqual(attributeCount, layer.AttributeColumns.Count, $"Unexpected attribute column count for {name}");

            // Ensure id and geom exist among Columns
            Assert.IsTrue(layer.Columns.Any(c => c.Name == "id" && c.IsPrimaryKey));
            Assert.IsTrue(layer.Columns.Any(c => c.Name == "geom"));
        }

        AssertLayer("points_a", "POINT", 2);
        AssertLayer("lines_b", "LINESTRING", 1);
        AssertLayer("polygons_c", "POLYGON", 2);
        AssertLayer("mix_d", "MULTIPOINT", 3);
    }
}
