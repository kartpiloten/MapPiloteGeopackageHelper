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
using System.Text.RegularExpressions;

namespace MapPiloteGeopackageHelper;

/// <summary>
/// Utility class for creating and managing GeoPackage files
/// </summary>
internal partial class CMPGeopackageUtils
{
    public const string GEOPACKAGE_MIME_TYPE = "application/geopackage+sqlite3";
    public const string GEOPACKAGE_FILE_EXTENSION = ".gpkg";

    /// <summary>
    /// Validates and sanitizes a SQL identifier (table name, column name, etc.) to prevent SQL injection.
    /// </summary>
    /// <param name="identifier">The identifier to validate</param>
    /// <param name="identifierType">Description of what type of identifier this is (for error messages)</param>
    /// <returns>The validated identifier</returns>
    /// <exception cref="ArgumentException">Thrown when the identifier contains invalid characters</exception>
    internal static string ValidateIdentifier(string identifier, string identifierType = "identifier")
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException($"The {identifierType} cannot be null or empty.", nameof(identifier));

        // SQL identifiers must start with a letter or underscore, followed by letters, digits, or underscores
        if (!IdentifierRegex().IsMatch(identifier))
            throw new ArgumentException(
                $"Invalid {identifierType}: '{identifier}'. Identifiers must start with a letter or underscore " +
                "and contain only letters, digits, and underscores.", nameof(identifier));

        // Check for SQLite reserved words (basic set)
        string[] reservedWords = ["SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TABLE", "INDEX", "FROM", "WHERE", "AND", "OR", "NOT", "NULL", "TRUE", "FALSE"];
        if (reservedWords.Contains(identifier.ToUpperInvariant()))
            throw new ArgumentException(
                $"Invalid {identifierType}: '{identifier}' is a reserved SQL keyword.", nameof(identifier));

        return identifier;
    }

    /// <summary>
    /// Validates that an SRID is within acceptable range.
    /// </summary>
    /// <param name="srid">The SRID to validate</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the SRID is invalid</exception>
    internal static void ValidateSrid(int srid)
    {
        // SRID can be -1 (undefined cartesian), 0 (undefined geographic), or positive EPSG codes
        if (srid < -1)
            throw new ArgumentOutOfRangeException(nameof(srid), srid, 
                "SRID must be -1 (undefined cartesian), 0 (undefined geographic), or a positive EPSG code.");
    }

    /// <summary>
    /// Validates batch size for bulk operations.
    /// </summary>
    /// <param name="batchSize">The batch size to validate</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the batch size is invalid</exception>
    internal static void ValidateBatchSize(int batchSize)
    {
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, 
                "Batch size must be at least 1.");
        if (batchSize > 100000)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, 
                "Batch size cannot exceed 100,000 for performance reasons.");
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex IdentifierRegex();

    internal static void CreateGeoPackageMetadataTables(SqliteConnection connection)
    {
        ExecuteCommand(connection, "PRAGMA application_id = 1196444487");

        const string createSrsTable = @"
        CREATE TABLE gpkg_spatial_ref_sys (
            srs_name TEXT NOT NULL,
            srs_id INTEGER NOT NULL PRIMARY KEY,
            organization TEXT NOT NULL,
            organization_coordsys_id INTEGER NOT NULL,
            definition TEXT NOT NULL,
            description TEXT
        )";
        ExecuteCommand(connection, createSrsTable);

        const string createContentsTable = @"
        CREATE TABLE gpkg_contents (
            table_name TEXT NOT NULL PRIMARY KEY,
            data_type TEXT NOT NULL,
            identifier TEXT UNIQUE,
            description TEXT DEFAULT '',
            last_change DATETIME NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
            min_x DOUBLE,
            min_y DOUBLE,
            max_x DOUBLE,
            max_y DOUBLE,
            srs_id INTEGER,
            CONSTRAINT fk_gc_r_srs_id FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id)
        )";
        ExecuteCommand(connection, createContentsTable);

        const string createGeomColumnsTable = @"
        CREATE TABLE gpkg_geometry_columns (
            table_name TEXT NOT NULL,
            column_name TEXT NOT NULL,
            geometry_type_name TEXT NOT NULL,
            srs_id INTEGER NOT NULL,
            z TINYINT NOT NULL,
            m TINYINT NOT NULL,
            CONSTRAINT pk_geom_cols PRIMARY KEY (table_name, column_name),
            CONSTRAINT uk_gc_table_name UNIQUE (table_name),
            CONSTRAINT fk_gc_tn FOREIGN KEY (table_name) REFERENCES gpkg_contents(table_name),
            CONSTRAINT fk_gc_srs FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id)
        )";
        ExecuteCommand(connection, createGeomColumnsTable);
    }

    internal static void SetupSpatialReferenceSystem(SqliteConnection connection, int srid)
    {
        ValidateSrid(srid);

        // Always insert the standard undefined SRS entries
        const string insertUndefined = @"
        INSERT OR REPLACE INTO gpkg_spatial_ref_sys 
        (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
        VALUES 
        ('Undefined cartesian SRS', -1, 'NONE', -1, 'undefined', 'undefined cartesian coordinate reference system')";
        ExecuteCommand(connection, insertUndefined);

        const string insertUndefinedGeographic = @"
        INSERT OR REPLACE INTO gpkg_spatial_ref_sys 
        (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
        VALUES 
        ('Undefined geographic SRS', 0, 'NONE', 0, 'undefined', 'undefined geographic coordinate reference system')";
        ExecuteCommand(connection, insertUndefinedGeographic);

        // Insert the requested SRID based on the parameter
        switch (srid)
        {
            case 3006:
                InsertSweref99Tm(connection);
                InsertWgs84(connection); // Also add WGS84 as commonly used
                break;
            case 4326:
                InsertWgs84(connection);
                break;
            case -1:
            case 0:
                // Already inserted above
                break;
            default:
                // For other SRIDs, insert a placeholder entry
                InsertGenericSrid(connection, srid);
                break;
        }
    }

    private static void InsertSweref99Tm(SqliteConnection connection)
    {
        const string insertSrs = @"
        INSERT OR REPLACE INTO gpkg_spatial_ref_sys 
        (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
        VALUES 
        ('SWEREF99 TM', 3006, 'EPSG', 3006, 
        'PROJCS[""SWEREF99 TM"",GEOGCS[""SWEREF99"",DATUM[""SWEREF99"",SPHEROID[""GRS 1980"",6378137,298.257222101,AUTHORITY[""EPSG"",""7019""]],TOWGS84[0,0,0,0,0,0,0],AUTHORITY[""EPSG"",""6619""]],PRIMEM[""Greenwich"",0,AUTHORITY[""EPSG"",""8901""]],UNIT[""degree"",0.01745329251994328,AUTHORITY[""EPSG"",""9122""]],AUTHORITY[""EPSG"",""4619""]],UNIT[""metre"",1,AUTHORITY[""EPSG"",""9001""]],PROJECTION[""Transverse_Mercator""],PARAMETER[""latitude_of_origin"",0],PARAMETER[""central_meridian"",15],PARAMETER[""scale_factor"",0.9996],PARAMETER[""false_easting"",500000],PARAMETER[""false_northing"",0],AUTHORITY[""EPSG"",""3006""],AXIS[""Y"",NORTH],AXIS[""X"",EAST]]',
        'Swedish national coordinate system')";
        ExecuteCommand(connection, insertSrs);
    }

    private static void InsertWgs84(SqliteConnection connection)
    {
        const string insertWgs84 = @"
        INSERT OR REPLACE INTO gpkg_spatial_ref_sys 
        (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
        VALUES 
        ('WGS 84', 4326, 'EPSG', 4326, 
        'GEOGCS[""WGS 84"",DATUM[""WGS_1984"",SPHEROID[""WGS 84"",6378137,298.257223563,AUTHORITY[""EPSG"",""7030""]],AUTHORITY[""EPSG"",""6326""]],PRIMEM[""Greenwich"",0,AUTHORITY[""EPSG"",""8901""]],UNIT[""degree"",0.01745329251994328,AUTHORITY[""EPSG"",""9122""]],AUTHORITY[""EPSG"",""4326""]]',
        'World Geodetic System 1984')";
        ExecuteCommand(connection, insertWgs84);
    }

    private static void InsertGenericSrid(SqliteConnection connection, int srid)
    {
        const string insertGeneric = @"
        INSERT OR REPLACE INTO gpkg_spatial_ref_sys 
        (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
        VALUES 
        (@srs_name, @srs_id, 'EPSG', @srs_id, 'undefined', @description)";
        
        using SqliteCommand command = new(insertGeneric, connection);
        command.Parameters.AddWithValue("@srs_name", $"EPSG:{srid}");
        command.Parameters.AddWithValue("@srs_id", srid);
        command.Parameters.AddWithValue("@description", $"Coordinate reference system EPSG:{srid}");
        command.ExecuteNonQuery();
    }

    internal static void RegisterTableInContents(SqliteConnection connection, string tableName,
        string geometryColumn, string geometryType, int srid, double? minX = null, double? minY = null,
        double? maxX = null, double? maxY = null)
    {
        // Validate identifiers
        ValidateIdentifier(tableName, "table name");
        ValidateIdentifier(geometryColumn, "geometry column name");
        ValidateSrid(srid);

        const string insertContents = @"
        INSERT INTO gpkg_contents 
        (table_name, data_type, identifier, description, srs_id, min_x, min_y, max_x, max_y)
        VALUES (@table_name, 'features', @table_name, @description, @srs_id, @min_x, @min_y, @max_x, @max_y)";

        using SqliteCommand command = new(insertContents, connection);
        command.Parameters.AddWithValue("@table_name", tableName);
        command.Parameters.AddWithValue("@description", $"Spatial table {tableName}");
        command.Parameters.AddWithValue("@srs_id", srid);
        command.Parameters.AddWithValue("@min_x", minX ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@min_y", minY ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@max_x", maxX ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@max_y", maxY ?? (object)DBNull.Value);
        command.ExecuteNonQuery();
    }

    internal static void RegisterGeometryColumn(SqliteConnection connection, string tableName,
        string geometryColumn, string geometryType, int srid)
    {
        // Validate identifiers
        ValidateIdentifier(tableName, "table name");
        ValidateIdentifier(geometryColumn, "geometry column name");
        ValidateSrid(srid);

        const string insertGeomColumn = @"
        INSERT INTO gpkg_geometry_columns 
        (table_name, column_name, geometry_type_name, srs_id, z, m)
        VALUES (@table_name, @column_name, @geometry_type, @srs_id, 0, 0)";

        using SqliteCommand command = new(insertGeomColumn, connection);
        command.Parameters.AddWithValue("@table_name", tableName);
        command.Parameters.AddWithValue("@column_name", geometryColumn);
        command.Parameters.AddWithValue("@geometry_type", geometryType);
        command.Parameters.AddWithValue("@srs_id", srid);
        command.ExecuteNonQuery();
    }

    internal static void ExecuteCommand(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = new(sql, connection);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates a GeoPackage binary blob from WKB data.
    /// This is the canonical implementation - use this instead of duplicating.
    /// </summary>
    /// <param name="wkb">Well-Known Binary geometry data</param>
    /// <param name="srid">Spatial Reference System Identifier</param>
    /// <returns>GeoPackage binary blob</returns>
    internal static byte[] CreateGpkgBlob(byte[] wkb, int srid)
    {
        byte[] result = new byte[8 + wkb.Length];
        
        result[0] = 0x47;  // 'G'
        result[1] = 0x50;  // 'P'
        result[2] = 0x00;  // Version
        result[3] = 0x00;  // Flags (no envelope)
        BitConverter.TryWriteBytes(result.AsSpan(4, 4), srid);
        wkb.CopyTo(result, 8);

        return result;
    }

    internal static (double minX, double minY, double maxX, double maxY) CalculateSwedishExtent()
    {
        return (181750.0, 6090250.0, 1086500.0, 7689500.0);
    }

    /// <summary>
    /// Updates the layer extent in gpkg_contents based on actual geometry data.
    /// This ensures QGIS "Zoom to Layer" works correctly.
    /// Adds a 5% buffer around the extent for better visualization.
    /// </summary>
    /// <param name="connection">Open SQLite connection</param>
    /// <param name="layerName">Name of the layer to update</param>
    /// <param name="geometryColumn">Name of the geometry column (default: "geom")</param>
    /// <param name="bufferPercent">Percentage buffer to add around extent (default: 5%)</param>
    internal static void UpdateLayerExtent(SqliteConnection connection, string layerName, string geometryColumn = "geom", double bufferPercent = 5.0)
    {
        ValidateIdentifier(layerName, "layer name");
        ValidateIdentifier(geometryColumn, "geometry column name");

        // Calculate extent from actual geometry data
        string sql = $"SELECT {geometryColumn} FROM {layerName} WHERE {geometryColumn} IS NOT NULL";
        
        double? minX = null, minY = null, maxX = null, maxY = null;
        
        using (SqliteCommand cmd = new(sql, connection))
        using (SqliteDataReader reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                byte[] gpkgBlob = (byte[])reader.GetValue(0);
                NetTopologySuite.Geometries.Geometry? geom = CMPGeopackageReadDataHelper.ReadGeometryFromGpkgBlob(gpkgBlob);
                
                if (geom == null || geom.IsEmpty)
                    continue;

                NetTopologySuite.Geometries.Envelope env = geom.EnvelopeInternal;
                
                if (minX == null || env.MinX < minX) minX = env.MinX;
                if (minY == null || env.MinY < minY) minY = env.MinY;
                if (maxX == null || env.MaxX > maxX) maxX = env.MaxX;
                if (maxY == null || env.MaxY > maxY) maxY = env.MaxY;
            }
        }

        // Update gpkg_contents with the calculated extent (with buffer)
        if (minX != null && minY != null && maxX != null && maxY != null)
        {
            // Calculate buffer size based on extent dimensions
            double width = maxX.Value - minX.Value;
            double height = maxY.Value - minY.Value;
            
            // For point features or very small extents, use a minimum buffer
            // This ensures the map canvas shows a reasonable area around the features
            const double minBuffer = 100.0; // 100 meters minimum buffer for projected CRS like SWEREF99
            
            double bufferX = Math.Max(width * bufferPercent / 100.0, minBuffer);
            double bufferY = Math.Max(height * bufferPercent / 100.0, minBuffer);
            
            // Apply buffer to extent
            double bufferedMinX = minX.Value - bufferX;
            double bufferedMinY = minY.Value - bufferY;
            double bufferedMaxX = maxX.Value + bufferX;
            double bufferedMaxY = maxY.Value + bufferY;

            const string updateSql = @"
                UPDATE gpkg_contents 
                SET min_x = @min_x, min_y = @min_y, max_x = @max_x, max_y = @max_y,
                    last_change = strftime('%Y-%m-%dT%H:%M:%fZ','now')
                WHERE table_name = @table_name";
            
            using SqliteCommand updateCmd = new(updateSql, connection);
            updateCmd.Parameters.AddWithValue("@table_name", layerName);
            updateCmd.Parameters.AddWithValue("@min_x", bufferedMinX);
            updateCmd.Parameters.AddWithValue("@min_y", bufferedMinY);
            updateCmd.Parameters.AddWithValue("@max_x", bufferedMaxX);
            updateCmd.Parameters.AddWithValue("@max_y", bufferedMaxY);
            updateCmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Updates the layer extent in gpkg_contents based on actual geometry data.
    /// </summary>
    /// <param name="geoPackagePath">Path to the GeoPackage file</param>
    /// <param name="layerName">Name of the layer to update</param>
    /// <param name="geometryColumn">Name of the geometry column (default: "geom")</param>
    /// <param name="bufferPercent">Percentage buffer to add around extent (default: 5%)</param>
    public static void UpdateLayerExtent(string geoPackagePath, string layerName, string geometryColumn = "geom", double bufferPercent = 5.0)
    {
        if (!File.Exists(geoPackagePath))
            throw new FileNotFoundException($"GeoPackage file not found: {geoPackagePath}");

        using SqliteConnection connection = new($"Data Source={geoPackagePath}");
        connection.Open();
        UpdateLayerExtent(connection, layerName, geometryColumn, bufferPercent);
    }

    /// <summary>
    /// Gets the declared geometry type for a layer from gpkg_geometry_columns
    /// </summary>
    internal static string? GetLayerGeometryType(SqliteConnection connection, string layerName)
    {
        const string sql = "SELECT geometry_type_name FROM gpkg_geometry_columns WHERE table_name = @table_name";
        using SqliteCommand cmd = new(sql, connection);
        cmd.Parameters.AddWithValue("@table_name", layerName);
        return cmd.ExecuteScalar()?.ToString();
    }

    /// <summary>
    /// Validates that a geometry matches the declared layer geometry type.
    /// </summary>
    /// <param name="geometry">The geometry to validate</param>
    /// <param name="declaredType">The declared geometry type from gpkg_geometry_columns (e.g., "POINT", "LINESTRING")</param>
    /// <returns>True if the geometry is compatible with the declared type</returns>
    internal static bool IsGeometryTypeCompatible(NetTopologySuite.Geometries.Geometry? geometry, string? declaredType)
    {
        if (geometry == null || string.IsNullOrEmpty(declaredType))
            return true; // No validation possible

        string actualType = geometry.GeometryType.ToUpperInvariant();
        string declared = declaredType.ToUpperInvariant();

        // GEOMETRY type accepts any geometry
        if (declared == "GEOMETRY" || declared == "GEOMETRYCOLLECTION")
            return true;

        // Direct match
        if (actualType == declared)
            return true;

        // Handle Multi* variants
        // A layer declared as MULTIPOINT should accept both Point and MultiPoint
        // But a POINT layer should NOT accept MultiPoint
        return declared switch
        {
            "MULTIPOINT" => actualType is "POINT" or "MULTIPOINT",
            "MULTILINESTRING" => actualType is "LINESTRING" or "MULTILINESTRING",
            "MULTIPOLYGON" => actualType is "POLYGON" or "MULTIPOLYGON",
            _ => false
        };
    }

    /// <summary>
    /// Validates geometry type and throws if incompatible.
    /// </summary>
    /// <param name="geometry">The geometry to validate</param>
    /// <param name="declaredType">The declared geometry type</param>
    /// <param name="layerName">Layer name for error message</param>
    /// <exception cref="ArgumentException">Thrown when geometry type doesn't match</exception>
    internal static void ValidateGeometryType(NetTopologySuite.Geometries.Geometry? geometry, string? declaredType, string layerName)
    {
        if (geometry == null || string.IsNullOrEmpty(declaredType))
            return;

        if (!IsGeometryTypeCompatible(geometry, declaredType))
        {
            throw new ArgumentException(
                $"Geometry type mismatch: Layer '{layerName}' is declared as '{declaredType}' " +
                $"but received a '{geometry.GeometryType}'. " +
                "This will cause display issues in GIS applications like QGIS.");
        }
    }
}
