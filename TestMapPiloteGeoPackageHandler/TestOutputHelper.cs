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
using System.Runtime.CompilerServices;

namespace TestMapPiloteGeoPackageHandler;

/// <summary>
/// Helper class for managing test output files.
/// GeoPackage files are saved to the TestResults folder with consistent naming
/// and are NOT deleted after tests, allowing inspection in QGIS.
/// </summary>
public static class TestOutputHelper
{
    private static readonly string OutputDirectory;

    static TestOutputHelper()
    {
        // Get the test results directory - use TestResults folder in the solution directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        
        // Navigate up from bin\Debug\net9.0 to the solution directory
        string solutionDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        OutputDirectory = Path.Combine(solutionDir, "TestResults", "GeoPackages");
        
        // Ensure the output directory exists
        if (!Directory.Exists(OutputDirectory))
        {
            Directory.CreateDirectory(OutputDirectory);
        }
    }

    /// <summary>
    /// Gets the output directory for test GeoPackage files.
    /// </summary>
    public static string GetOutputDirectory() => OutputDirectory;

    /// <summary>
    /// Creates a path for a test output GeoPackage file.
    /// The file will be named: output_{testMethodName}.gpkg
    /// If the file already exists, it will be deleted first.
    /// </summary>
    /// <param name="testMethodName">Name of the test method (automatically captured)</param>
    /// <returns>Full path to the GeoPackage file</returns>
    public static string GetTestOutputPath([CallerMemberName] string testMethodName = "")
    {
        string fileName = $"output_{testMethodName}.gpkg";
        string filePath = Path.Combine(OutputDirectory, fileName);
        
        // Delete existing file if present
        DeleteIfExists(filePath);
        
        return filePath;
    }

    /// <summary>
    /// Creates a path for a test output GeoPackage file with a custom suffix.
    /// The file will be named: output_{testMethodName}_{suffix}.gpkg
    /// If the file already exists, it will be deleted first.
    /// </summary>
    /// <param name="suffix">Custom suffix to append to the filename</param>
    /// <param name="testMethodName">Name of the test method (automatically captured)</param>
    /// <returns>Full path to the GeoPackage file</returns>
    public static string GetTestOutputPath(string suffix, [CallerMemberName] string testMethodName = "")
    {
        string fileName = $"output_{testMethodName}_{suffix}.gpkg";
        string filePath = Path.Combine(OutputDirectory, fileName);
        
        // Delete existing file if present
        DeleteIfExists(filePath);
        
        return filePath;
    }

    /// <summary>
    /// Deletes a file if it exists, with retry logic for locked files.
    /// </summary>
    /// <param name="filePath">Path to the file to delete</param>
    public static void DeleteIfExists(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        // Try to delete with retries for locked files
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                // Force garbage collection to release any file handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                File.Delete(filePath);
                return;
            }
            catch (IOException)
            {
                if (attempt < 2)
                {
                    Thread.Sleep(100 * (attempt + 1));
                }
                // On final attempt, ignore the error - test will fail if file can't be created
            }
        }
    }

    /// <summary>
    /// Cleans up all test output files in the output directory.
    /// Useful for running before a full test suite.
    /// </summary>
    public static void CleanAllOutputFiles()
    {
        if (!Directory.Exists(OutputDirectory))
            return;

        foreach (string file in Directory.GetFiles(OutputDirectory, "output_*.gpkg"))
        {
            DeleteIfExists(file);
        }
    }

    /// <summary>
    /// Logs the output file location to the console for easy access.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    public static void LogOutputLocation(string filePath)
    {
        Console.WriteLine($"Test output saved to: {filePath}");
        Console.WriteLine($"Open in QGIS: {filePath}");
    }
}
