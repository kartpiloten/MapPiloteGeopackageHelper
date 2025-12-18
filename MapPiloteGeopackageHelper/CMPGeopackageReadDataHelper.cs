/* Licence...
 * MIT License
 *
 * Copyright (c) 2025 Anders Dahlgren
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy 
 * of this software and associated documentation files (the "Software"), to deal 
 * in the Software without restriction, including without limitation the rights 
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is 
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
 * SOFTWARE.
 */
using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Globalization;

namespace MapPiloteGeopackageHelper;

/// <summary>
/// Helper class for reading data from GeoPackage files.
/// </summary>
public static class CMPGeopackageReadDataHelper
{
    internal static List<string> GetContentTableNames(string geoPackageFilePath)
    {
        List<string> tableNames = [];
        using SqliteConnection connection = new($"Data Source={geoPackageFilePath}");
        connection.Open();

        const string queryFile = "SELECT table_name FROM gpkg_contents";
        using SqliteCommand commandFile = new(queryFile, connection);
        using SqliteDataReader readerFile = commandFile.ExecuteReader();
        while (readerFile.Read())
        {
            tableNames.Add(readerFile.GetString(0));
        }

        return tableNames;
    }

    /// <summary>
    /// Gets comprehensive information about a GeoPackage file including layers and spatial reference systems.
    /// </summary>
    /// <param name="geoPackageFilePath">Path to the GeoPackage file.</param>
    /// <returns>GeopackageInfo containing layer and SRS information.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
    public static GeopackageInfo GetGeopackageInfo(string geoPackageFilePath)
    {
        if (!File.Exists(geoPackageFilePath))
            throw new FileNotFoundException($"GeoPackage file not found: {geoPackageFilePath}");

        using SqliteConnection connection = new($"Data Source={geoPackageFilePath}");
        connection.Open();

        List<SrsInfo> srsList = [];
        const string srsSql = "SELECT srs_id, srs_name, organization, organization_coordsys_id, definition, description FROM gpkg_spatial_ref_sys";
        using (SqliteCommand srsCmd = new(srsSql, connection))
        using (SqliteDataReader srsReader = srsCmd.ExecuteReader())
        {
            while (srsReader.Read())
            {
                srsList.Add(new SrsInfo(
                    srsReader.GetInt32(0),
                    srsReader.GetString(1),
                    srsReader.GetString(2),
                    srsReader.GetInt32(3),
                    srsReader.GetString(4),
                    srsReader.IsDBNull(5) ? null : srsReader.GetString(5)));
            }
        }

        Dictionary<string, (string Column, string Type, int Srid)> geomInfoByTable = [];
        const string geomSql = "SELECT table_name, column_name, geometry_type_name, srs_id FROM gpkg_geometry_columns";
        using (SqliteCommand gCmd = new(geomSql, connection))
        using (SqliteDataReader gReader = gCmd.ExecuteReader())
        {
            while (gReader.Read())
            {
                string t = gReader.GetString(0);
                string c = gReader.GetString(1);
                string gt = gReader.GetString(2);
                int s = gReader.GetInt32(3);
                geomInfoByTable[t] = (c, gt, s);
            }
        }

        List<LayerInfo> layers = [];
        const string contentsSql = "SELECT table_name, data_type, srs_id, min_x, min_y, max_x, max_y FROM gpkg_contents ORDER BY table_name";
        using (SqliteCommand cCmd = new(contentsSql, connection))
        using (SqliteDataReader cReader = cCmd.ExecuteReader())
        {
            while (cReader.Read())
            {
                string tableName = cReader.GetString(0);
                string dataType = cReader.GetString(1);
                int? srid = cReader.IsDBNull(2) ? null : cReader.GetInt32(2);
                double? minX = cReader.IsDBNull(3) ? null : cReader.GetDouble(3);
                double? minY = cReader.IsDBNull(4) ? null : cReader.GetDouble(4);
                double? maxX = cReader.IsDBNull(5) ? null : cReader.GetDouble(5);
                double? maxY = cReader.IsDBNull(6) ? null : cReader.GetDouble(6);

                List<ColumnInfo> columns = [];
                using (SqliteCommand tCmd = new($"PRAGMA table_info({tableName})", connection))
                using (SqliteDataReader tReader = tCmd.ExecuteReader())
                {
                    while (tReader.Read())
                    {
                        string colName = tReader.GetString(1);
                        string colType = tReader.IsDBNull(2) ? string.Empty : tReader.GetString(2);
                        bool notNull = !tReader.IsDBNull(3) && tReader.GetInt32(3) == 1;
                        bool isPk = !tReader.IsDBNull(5) && tReader.GetInt32(5) == 1;
                        columns.Add(new ColumnInfo(colName, colType, notNull, isPk));
                    }
                }

                string? geomColumn = null;
                string? geomType = null;
                int? geomSrid = null;
                if (geomInfoByTable.TryGetValue(tableName, out (string Column, string Type, int Srid) gi))
                {
                    geomColumn = gi.Column;
                    geomType = gi.Type;
                    geomSrid = gi.Srid;
                    if (srid is null) srid = geomSrid;
                }

                List<ColumnInfo> attributeColumns = columns
                    .Where(c => !string.Equals(c.Name, "id", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(c.Name, geomColumn, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                layers.Add(new LayerInfo(
                    TableName: tableName,
                    DataType: dataType,
                    Srid: srid,
                    GeometryColumn: geomColumn,
                    GeometryType: geomType,
                    MinX: minX,
                    MinY: minY,
                    MaxX: maxX,
                    MaxY: maxY,
                    Columns: columns,
                    AttributeColumns: attributeColumns));
            }
        }

        return new GeopackageInfo(layers, srsList);
    }

    internal static IEnumerable<(int PiposId, byte[]? GeometryWkb)> ReadFeaturesWithGeometryWkb(string geoPackageFilePath, string tableName)
    {
        CMPGeopackageUtils.ValidateIdentifier(tableName, "table name");
        
        using SqliteConnection connection = new($"Data Source={geoPackageFilePath}");
        connection.Open();

        string sql = $"SELECT * FROM {tableName} ORDER BY pipos_id";
        using SqliteCommand command = new(sql, connection);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            int piposIndex = reader.GetOrdinal("pipos_id");
            int piposId = reader.GetInt32(piposIndex);

            byte[]? wkb = null;
            int geomOrdinal = reader.GetOrdinal("geom");
            if (geomOrdinal >= 0)
            {
                byte[]? geomBlob = reader.GetValue(geomOrdinal) as byte[];
                if (geomBlob is not null)
                {
                    try
                    {
                        wkb = StripGpkgHeader(geomBlob);
                    }
                    catch (ArgumentException)
                    {
                        wkb = null;
                    }
                }
            }

            yield return (piposId, wkb);
        }
    }

    /// <summary>
    /// Strips the GeoPackage header from a geometry binary blob to get the WKB data.
    /// </summary>
    /// <param name="gpkgBinary">The GeoPackage geometry binary.</param>
    /// <returns>The WKB portion of the geometry.</returns>
    /// <exception cref="ArgumentException">Thrown when the binary is invalid.</exception>
    public static byte[] StripGpkgHeader(byte[] gpkgBinary)
    {
        if (gpkgBinary.Length < 8)
            throw new ArgumentException("Invalid GPKG geometry header: binary too short.");

        byte flags = gpkgBinary[3];
        int envelopeIndicator = (flags >> 1) & 0x07;
        int envelopeBytes = envelopeIndicator switch
        {
            0 => 0,
            1 => 32,
            2 => 48,
            3 => 64,
            _ => throw new ArgumentException($"Invalid envelope indicator ({envelopeIndicator}) in GPKG header.")
        };

        int headerSize = 8 + envelopeBytes;
        if (gpkgBinary.Length < headerSize)
            throw new ArgumentException("Incomplete GPKG geometry header.");

        byte[] wkb = new byte[gpkgBinary.Length - headerSize];
        Array.Copy(gpkgBinary, headerSize, wkb, 0, wkb.Length);
        return wkb;
    }

    /// <summary>
    /// Reads a geometry from a GeoPackage binary blob.
    /// </summary>
    /// <param name="gpkgBinary">The GeoPackage geometry binary.</param>
    /// <returns>The parsed geometry, or null if parsing fails.</returns>
    public static Geometry? ReadGeometryFromGpkgBlob(byte[] gpkgBinary)
    {
        try
        {
            byte[] wkb = StripGpkgHeader(gpkgBinary);
            WKBReader reader = new();
            return reader.Read(wkb);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Executes a spatial query and returns matching features.
    /// </summary>
    /// <param name="geoPackageFilePath">Path to the GeoPackage file.</param>
    /// <param name="tableName">Name of the table to query.</param>
    /// <param name="whereClause">Optional WHERE clause (without the WHERE keyword).</param>
    /// <param name="geometryColumn">Name of the geometry column. Default is "geom".</param>
    /// <returns>Enumerable of matching features.</returns>
    public static IEnumerable<FeatureRecord> ExecuteSpatialQuery(
        string geoPackageFilePath,
        string tableName,
        string? whereClause = null,
        string geometryColumn = "geom")
    {
        CMPGeopackageUtils.ValidateIdentifier(tableName, "table name");
        CMPGeopackageUtils.ValidateIdentifier(geometryColumn, "geometry column name");
        
        using SqliteConnection connection = new($"Data Source={geoPackageFilePath}");
        connection.Open();

        string sql = $"SELECT * FROM {tableName}";
        if (!string.IsNullOrEmpty(whereClause))
            sql += $" WHERE {whereClause}";

        using SqliteCommand command = new(sql, connection);
        using SqliteDataReader reader = command.ExecuteReader();

        List<string> columnNames = [];
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columnNames.Add(reader.GetName(i));
        }

        List<string> attributeColumns = columnNames
            .Where(name => !name.Equals("id", StringComparison.OrdinalIgnoreCase) 
                        && !name.Equals(geometryColumn, StringComparison.OrdinalIgnoreCase))
            .ToList();

        int geomOrdinal = -1;
        try { geomOrdinal = reader.GetOrdinal(geometryColumn); } 
        catch (IndexOutOfRangeException) { geomOrdinal = -1; }

        while (reader.Read())
        {
            Dictionary<string, string?> attributes = new(StringComparer.Ordinal);
            foreach (string colName in attributeColumns)
            {
                int ord = reader.GetOrdinal(colName);
                attributes[colName] = reader.IsDBNull(ord) ? null : 
                    Convert.ToString(reader.GetValue(ord), CultureInfo.InvariantCulture);
            }

            Geometry? geometry = null;
            if (geomOrdinal >= 0 && !reader.IsDBNull(geomOrdinal))
            {
                byte[] gpkgBlob = (byte[])reader.GetValue(geomOrdinal);
                geometry = ReadGeometryFromGpkgBlob(gpkgBlob);
            }

            yield return new FeatureRecord(geometry, attributes);
        }
    }

    /// <summary>
    /// Column information for a GeoPackage table.
    /// </summary>
    /// <param name="Name">Column name.</param>
    /// <param name="Type">Column data type.</param>
    /// <param name="NotNull">Whether the column has a NOT NULL constraint.</param>
    /// <param name="IsPrimaryKey">Whether the column is a primary key.</param>
    public sealed record ColumnInfo(string Name, string Type, bool NotNull, bool IsPrimaryKey);

    /// <summary>
    /// Information about a layer (table) in a GeoPackage.
    /// </summary>
    /// <param name="TableName">Name of the table.</param>
    /// <param name="DataType">Data type (usually "features" for vector layers).</param>
    /// <param name="Srid">Spatial Reference System Identifier.</param>
    /// <param name="GeometryColumn">Name of the geometry column.</param>
    /// <param name="GeometryType">Type of geometry (POINT, LINESTRING, etc.).</param>
    /// <param name="MinX">Minimum X extent.</param>
    /// <param name="MinY">Minimum Y extent.</param>
    /// <param name="MaxX">Maximum X extent.</param>
    /// <param name="MaxY">Maximum Y extent.</param>
    /// <param name="Columns">All columns in the table.</param>
    /// <param name="AttributeColumns">Non-system columns (excluding id and geometry).</param>
    public sealed record LayerInfo(
        string TableName,
        string DataType,
        int? Srid,
        string? GeometryColumn,
        string? GeometryType,
        double? MinX,
        double? MinY,
        double? MaxX,
        double? MaxY,
        List<ColumnInfo> Columns,
        List<ColumnInfo> AttributeColumns);

    /// <summary>
    /// Spatial Reference System information.
    /// </summary>
    /// <param name="SrsId">SRS identifier.</param>
    /// <param name="SrsName">Human-readable name.</param>
    /// <param name="Organization">Organization that defined the SRS (e.g., "EPSG").</param>
    /// <param name="OrganizationCoordsysId">Organization's ID for this SRS.</param>
    /// <param name="Definition">WKT definition of the SRS.</param>
    /// <param name="Description">Optional description.</param>
    public sealed record SrsInfo(
        int SrsId,
        string SrsName,
        string Organization,
        int OrganizationCoordsysId,
        string Definition,
        string? Description);

    /// <summary>
    /// Complete information about a GeoPackage file.
    /// </summary>
    /// <param name="Layers">List of layers in the GeoPackage.</param>
    /// <param name="SpatialRefSystems">List of spatial reference systems defined in the GeoPackage.</param>
    public sealed record GeopackageInfo(
        List<LayerInfo> Layers,
        List<SrsInfo> SpatialRefSystems);

    /// <summary>
    /// Reads all features from a layer.
    /// </summary>
    /// <param name="geoPackageFilePath">Path to the GeoPackage file.</param>
    /// <param name="tableName">Name of the table to read.</param>
    /// <param name="geometryColumn">Name of the geometry column. Default is "geom".</param>
    /// <param name="includeGeometry">Whether to include geometry data. Default is true.</param>
    /// <returns>Enumerable of features.</returns>
    public static IEnumerable<FeatureRecord> ReadFeatures(
        string geoPackageFilePath,
        string tableName,
        string geometryColumn = "geom",
        bool includeGeometry = true)
    {
        CMPGeopackageUtils.ValidateIdentifier(tableName, "table name");
        CMPGeopackageUtils.ValidateIdentifier(geometryColumn, "geometry column name");
        
        using SqliteConnection connection = new($"Data Source={geoPackageFilePath}");
        connection.Open();

        List<ColumnInfo> columns = [];
        using (SqliteCommand tCmd = new($"PRAGMA table_info({tableName})", connection))
        using (SqliteDataReader tReader = tCmd.ExecuteReader())
        {
            while (tReader.Read())
            {
                string colName = tReader.GetString(1);
                string colType = tReader.IsDBNull(2) ? string.Empty : tReader.GetString(2);
                bool notNull = !tReader.IsDBNull(3) && tReader.GetInt32(3) == 1;
                bool isPk = !tReader.IsDBNull(5) && tReader.GetInt32(5) == 1;
                columns.Add(new ColumnInfo(colName, colType, notNull, isPk));
            }
        }

        List<string> attributeColumns = columns
            .Where(c => !string.Equals(c.Name, "id", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(c.Name, geometryColumn, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .ToList();

        using SqliteCommand cmd = new($"SELECT * FROM {tableName}", connection);
        using SqliteDataReader reader = cmd.ExecuteReader();

        int geomOrdinal = -1;
        try { geomOrdinal = reader.GetOrdinal(geometryColumn); } 
        catch (IndexOutOfRangeException) { geomOrdinal = -1; }

        WKBReader? wkbReader = includeGeometry ? new WKBReader() : null;

        while (reader.Read())
        {
            Dictionary<string, string?> attrs = new(StringComparer.Ordinal);
            foreach (string name in attributeColumns)
            {
                int ord = reader.GetOrdinal(name);
                attrs[name] = reader.IsDBNull(ord) ? null : Convert.ToString(reader.GetValue(ord), CultureInfo.InvariantCulture);
            }

            Geometry? geometry = null;
            if (includeGeometry && geomOrdinal >= 0 && !reader.IsDBNull(geomOrdinal))
            {
                byte[] gpkgBlob = (byte[])reader.GetValue(geomOrdinal);
                byte[] wkb = StripGpkgHeader(gpkgBlob);
                geometry = wkbReader!.Read(wkb);
            }

            yield return new FeatureRecord(geometry, attrs);
        }
    }
}
