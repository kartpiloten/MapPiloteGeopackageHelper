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

namespace MapPiloteGeopackageHelper;

/// <summary>
/// Helper class for creating layers (tables) in GeoPackage files.
/// </summary>
public class GeopackageLayerCreateHelper
{
    /// <summary>
    /// Creates a spatial layer/table in a GeoPackage with custom table structure.
    /// </summary>
    /// <param name="geoPackageFilePath">Path where the GeoPackage will be created.</param>
    /// <param name="layerName">Name of the layer/table to create (must be a valid SQL identifier).</param>
    /// <param name="tableHeaders">Dictionary of column names and their SQL types.</param>
    /// <param name="geometryType">Type of geometry (POINT, LINESTRING, POLYGON, GEOMETRY, etc.). Default is "GEOMETRY".</param>
    /// <param name="srid">Spatial Reference System Identifier. Default is 3006 (SWEREF99 TM).</param>
    /// <param name="onStatus">Optional callback for status messages.</param>
    /// <param name="onError">Optional callback for error messages.</param>
    /// <exception cref="FileNotFoundException">Thrown when the GeoPackage file doesn't exist.</exception>
    /// <exception cref="ArgumentException">Thrown when layer name or column names are invalid.</exception>
    public static void CreateGeopackageLayer(
        string geoPackageFilePath, 
        string layerName, 
        Dictionary<string, string> tableHeaders, 
        string geometryType = "GEOMETRY", 
        int srid = 3006,
        Action<string>? onStatus = null,
        Action<string>? onError = null)
    {
        try
        {
            // Ensure the GeoPackage file exists
            if (!File.Exists(geoPackageFilePath))
            {
                throw new FileNotFoundException($"GeoPackage file not found: {geoPackageFilePath}");
            }

            // Validate all identifiers to prevent SQL injection
            CMPGeopackageUtils.ValidateIdentifier(layerName, "layer name");
            CMPGeopackageUtils.ValidateSrid(srid);
            
            foreach (string columnName in tableHeaders.Keys)
            {
                CMPGeopackageUtils.ValidateIdentifier(columnName, "column name");
            }

            // Use defaults for geometry column
            const string geometryColumn = "geom";

            using SqliteConnection connection = new($"Data Source={geoPackageFilePath}");
            connection.Open();

            List<string> columnDefinitions = ["id INTEGER PRIMARY KEY AUTOINCREMENT"];

            // Add all columns from tableHeaders
            foreach (KeyValuePair<string, string> header in tableHeaders)
            {
                columnDefinitions.Add($"{header.Key} {header.Value}");
            }

            // Add geometry column
            columnDefinitions.Add($"{geometryColumn} BLOB");

            // Create the table
            string createTable = $@"
            CREATE TABLE {layerName} (
                {string.Join(", ", columnDefinitions)}
            )";
            CMPGeopackageUtils.ExecuteCommand(connection, createTable);

            // Calculate spatial extent for Swedish national grid
            (double minX, double minY, double maxX, double maxY) = CMPGeopackageUtils.CalculateSwedishExtent();

            // Register the table in gpkg_contents
            CMPGeopackageUtils.RegisterTableInContents(connection, layerName, geometryColumn, geometryType, srid, minX, minY, maxX, maxY);

            // Register geometry column in gpkg_geometry_columns
            CMPGeopackageUtils.RegisterGeometryColumn(connection, layerName, geometryColumn, geometryType, srid);

            onStatus?.Invoke($"Successfully created spatial layer '{layerName}' in GeoPackage");
        }
        catch (Exception ex)
        {
            string errorMessage = $"Error creating GeoPackage layer: {ex.Message}";
            onError?.Invoke(errorMessage);
            throw;
        }
    }
}
