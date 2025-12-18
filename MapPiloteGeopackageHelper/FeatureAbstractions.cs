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
using NetTopologySuite.Geometries;

namespace MapPiloteGeopackageHelper;

/// <summary>
/// Represents a geographic feature with geometry and attributes.
/// This is the standard data transfer object used throughout the library for reading and writing features.
/// </summary>
/// <param name="Geometry">
/// The geometry of the feature (Point, LineString, Polygon, etc.) or null for features without geometry.
/// Uses NetTopologySuite geometry types.
/// </param>
/// <param name="Attributes">
/// Dictionary of attribute names to string values. 
/// Null values indicate NULL in the database.
/// All values are represented as strings and converted to appropriate types during insert.
/// </param>
/// <example>
/// <code>
/// // Create a point feature
/// var feature = new FeatureRecord(
///     new Point(674188, 6580251),
///     new Dictionary&lt;string, string?&gt; 
///     { 
///         ["name"] = "Stockholm", 
///         ["population"] = "975000" 
///     });
/// 
/// // Create a feature without geometry
/// var attributeOnly = new FeatureRecord(
///     null,
///     new Dictionary&lt;string, string?&gt; { ["id"] = "123" });
/// </code>
/// </example>
public sealed record FeatureRecord(
    Geometry? Geometry,
    IReadOnlyDictionary<string, string?> Attributes);
