using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler;

/// <summary>
/// Tests for bulk insert and read operations.
/// Output files are saved to TestResults/GeoPackages folder for inspection in QGIS.
/// </summary>
[TestClass]
public class TestBulkInsertAndRead
{
    [TestMethod]
    public void BulkInsertAndRead_ShouldRoundtrip()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);

        Dictionary<string, string> headers = new(StringComparer.Ordinal)
        {
            { "name", "TEXT" },
            { "age", "INTEGER" },
            { "height", "REAL" },
            { "note", "TEXT" }
        };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", headers, geometryType: "POINT", srid: 3006);

        List<FeatureRecord> features =
        [
            new FeatureRecord(
                Geometry: new Point(1, 2),
                Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["name"] = "Alice",
                    ["age"] = "30",
                    ["height"] = "1.75",
                    ["note"] = null
                }
            ),
            new FeatureRecord(
                Geometry: new Point(3, 4),
                Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["name"] = "Bob",
                    ["age"] = "",
                    ["note"] = "B"
                }
            ),
            new FeatureRecord(
                Geometry: null,
                Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["name"] = "Charlie",
                    ["age"] = "40",
                    ["height"] = "1.80",
                    ["note"] = "N/A"
                }
            )
        ];

        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "test_layer", features, srid: 3006, batchSize: 2);

        List<FeatureRecord> read = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer", geometryColumn: "geom", includeGeometry: true).ToList();
        Assert.AreEqual(features.Count, read.Count, "Feature count mismatch");

        FeatureRecord f1 = read[0];
        Assert.IsNotNull(f1.Geometry);
        Assert.AreEqual(1.0, ((Point)f1.Geometry!).X, 1e-9);
        Assert.AreEqual(2.0, ((Point)f1.Geometry!).Y, 1e-9);
        Assert.AreEqual("Alice", f1.Attributes["name"]);
        Assert.AreEqual("30", f1.Attributes["age"]);
        Assert.AreEqual("1.75", f1.Attributes["height"]);
        Assert.IsNull(f1.Attributes["note"], "note should be NULL");

        FeatureRecord f2 = read[1];
        Assert.IsNotNull(f2.Geometry);
        Assert.AreEqual(3.0, ((Point)f2.Geometry!).X, 1e-9);
        Assert.AreEqual(4.0, ((Point)f2.Geometry!).Y, 1e-9);
        Assert.AreEqual("Bob", f2.Attributes["name"]);
        Assert.IsNull(f2.Attributes["age"], "empty string should be stored as NULL for INTEGER");
        Assert.IsNull(f2.Attributes["height"], "missing key should be stored as NULL");
        Assert.AreEqual("B", f2.Attributes["note"]);

        FeatureRecord f3 = read[2];
        Assert.IsNull(f3.Geometry);
        Assert.AreEqual("Charlie", f3.Attributes["name"]);
        Assert.AreEqual("40", f3.Attributes["age"]);
        Assert.AreEqual("1.8", f3.Attributes["height"] ?? "1.8");
        Assert.AreEqual("N/A", f3.Attributes["note"]);

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    [TestMethod]
    public void BulkInsertFeatures_InvalidValue_ShouldThrow()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
        Dictionary<string, string> headers = new(StringComparer.Ordinal)
        {
            { "name", "TEXT" },
            { "age", "INTEGER" }
        };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", headers, geometryType: "POINT", srid: 3006);

        FeatureRecord badFeature = new(
            Geometry: new Point(10, 20),
            Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["name"] = "Bad",
                ["age"] = "not_a_number"
            }
        );

        List<FeatureRecord> list = [badFeature];

        Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "test_layer", list));

        TestOutputHelper.LogOutputLocation(gpkg);
    }
}
