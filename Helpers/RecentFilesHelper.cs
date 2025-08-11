using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace RCLayoutPreview.Helpers
{
    /// <summary>
    /// Manages recently opened XAML layout files for quick access
    /// </summary>
    public static class RecentFilesHelper
    {
        private const string RegistryKey = @"SOFTWARE\RCLayoutPreview\RecentFiles";
        private const int MaxRecentFiles = 10;

        /// <summary>
        /// Adds a file to the recent files list
        /// </summary>
        /// <param name="filePath">Full path to the file</param>
        public static void AddRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                var recentFiles = GetRecentFiles().ToList();
                
                // Remove if already exists (to move to top)
                recentFiles.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
                
                // Add to beginning
                recentFiles.Insert(0, filePath);
                
                // Keep only the maximum number
                if (recentFiles.Count > MaxRecentFiles)
                {
                    recentFiles = recentFiles.Take(MaxRecentFiles).ToList();
                }
                
                SaveRecentFiles(recentFiles);
            }
            catch (Exception ex)
            {
                // Silently ignore registry errors
                System.Diagnostics.Debug.WriteLine($"Failed to add recent file: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the list of recent files
        /// </summary>
        /// <returns>List of recent file paths</returns>
        public static IEnumerable<string> GetRecentFiles()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey))
                {
                    if (key == null)
                        return new List<string>();

                    var files = new List<string>();
                    for (int i = 0; i < MaxRecentFiles; i++)
                    {
                        string filePath = key.GetValue($"File{i}") as string;
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            files.Add(filePath);
                        }
                    }
                    return files;
                }
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Gets recent files with display names (filename only)
        /// </summary>
        /// <returns>List of RecentFileInfo objects</returns>
        public static IEnumerable<RecentFileInfo> GetRecentFilesInfo()
        {
            return GetRecentFiles().Select(path => new RecentFileInfo
            {
                FullPath = path,
                DisplayName = Path.GetFileName(path),
                Directory = Path.GetDirectoryName(path)
            });
        }

        /// <summary>
        /// Clears all recent files
        /// </summary>
        public static void ClearRecentFiles()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegistryKey, false);
            }
            catch
            {
                // Silently ignore registry errors
            }
        }

        /// <summary>
        /// Removes a specific file from recent files (e.g., if deleted)
        /// </summary>
        /// <param name="filePath">File path to remove</param>
        public static void RemoveRecentFile(string filePath)
        {
            try
            {
                var recentFiles = GetRecentFiles().ToList();
                recentFiles.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
                SaveRecentFiles(recentFiles);
            }
            catch
            {
                // Silently ignore registry errors
            }
        }

        /// <summary>
        /// Saves the recent files list to registry
        /// </summary>
        /// <param name="files">List of file paths</param>
        private static void SaveRecentFiles(List<string> files)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey))
            {
                if (key == null)
                    return;

                // Clear existing entries
                for (int i = 0; i < MaxRecentFiles; i++)
                {
                    try
                    {
                        key.DeleteValue($"File{i}");
                    }
                    catch { }
                }

                // Save new entries
                for (int i = 0; i < files.Count && i < MaxRecentFiles; i++)
                {
                    key.SetValue($"File{i}", files[i]);
                }
            }
        }
    }

    /// <summary>
    /// Information about a recent file
    /// </summary>
    public class RecentFileInfo
    {
        public string FullPath { get; set; }
        public string DisplayName { get; set; }
        public string Directory { get; set; }
        public string ToolTip => $"{DisplayName}\n{Directory}";
    }
}