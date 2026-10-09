using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SW2URDF.URDFExport
{
    /// <summary>
    /// Stages a package beside its destination, retaining the previous package until
    /// all generated files are ready. Does not access SolidWorks objects.
    /// </summary>
    public sealed class PackageExportTransaction : IDisposable
    {
        private readonly string root;
        private readonly string stagingDirectory;
        private readonly string backupDirectory;
        private readonly string destination;
        private bool committed;
        private bool disposed;

        public URDFPackage Package { get; }
        public string CleanupWarning { get; private set; }

        public PackageExportTransaction(string packageName, string savePath)
        {
            if (String.IsNullOrWhiteSpace(packageName) ||
                !Regex.IsMatch(packageName, @"\A[a-z][a-z0-9_]*\z") || !Path.IsPathRooted(savePath))
            {
                throw new ArgumentException("A valid package name and absolute export directory are required.");
            }
            root = Path.GetFullPath(savePath);
            destination = Path.Combine(root, packageName);
            string id = Guid.NewGuid().ToString("N");
            stagingDirectory = Path.Combine(root, ".sw2urdf-stage-" + id);
            backupDirectory = Path.Combine(root, ".sw2urdf-backup-" + id);
            Package = new URDFPackage(packageName, stagingDirectory);
            try
            {
                Directory.CreateDirectory(stagingDirectory);
                // Preserve existing meshes for URDF-only export and user-added files.
                if (Directory.Exists(destination))
                {
                    CopyDirectory(destination, Package.WindowsPackageDirectory);
                }
            }
            catch (Exception error)
            {
                Dispose();
                if (!String.IsNullOrEmpty(CleanupWarning))
                {
                    throw new IOException(error.Message + "\n" + CleanupWarning, error);
                }
                throw;
            }
        }

        public void Commit()
        {
            if (disposed || committed)
            {
                throw new InvalidOperationException("The package export transaction is no longer active.");
            }
            bool hadDestination = Directory.Exists(destination);
            if (hadDestination)
            {
                RejectReparsePoint(destination);
                Directory.Move(destination, backupDirectory);
            }
            try
            {
                Directory.Move(Package.WindowsPackageDirectory.TrimEnd(Path.DirectorySeparatorChar), destination);
            }
            catch (Exception exportError)
            {
                if (hadDestination)
                {
                    try
                    {
                        Directory.Move(backupDirectory, destination);
                    }
                    catch (Exception rollbackError)
                    {
                        // Never delete this backup: it is the only remaining previous package.
                        throw new IOException("Could not replace or restore the package. The previous package is at " +
                            backupDirectory, new AggregateException(exportError, rollbackError));
                    }
                }
                throw;
            }
            committed = true;
            if (hadDestination)
            {
                TryDeleteOwnedDirectory(backupDirectory);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            TryDeleteOwnedDirectory(stagingDirectory);
        }

        private void TryDeleteOwnedDirectory(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                // Recursive cleanup is limited to this transaction's two direct sibling folders.
                if ((!String.Equals(fullPath, stagingDirectory, StringComparison.OrdinalIgnoreCase) &&
                    !String.Equals(fullPath, backupDirectory, StringComparison.OrdinalIgnoreCase)) ||
                    !String.Equals(Path.GetDirectoryName(fullPath).TrimEnd(Path.DirectorySeparatorChar),
                        root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(fullPath, destination, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Refusing to clean up a directory outside this export transaction.");
                }
                if (Directory.Exists(fullPath))
                {
                    CheckDirectoryTree(fullPath);
                    Directory.Delete(fullPath, true);
                }
            }
            catch (Exception e)
            {
                CleanupWarning = (CleanupWarning == null ? "" : CleanupWarning + "\n") +
                    "Could not remove temporary export directory " + path + ": " + e.Message;
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            RejectReparsePoint(source);
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
            {
                RejectReparsePoint(file);
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }
            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
            }
        }

        private static void CheckDirectoryTree(string directory)
        {
            RejectReparsePoint(directory);
            foreach (string file in Directory.GetFiles(directory))
            {
                RejectReparsePoint(file);
            }
            foreach (string child in Directory.GetDirectories(directory))
            {
                CheckDirectoryTree(child);
            }
        }

        private static void RejectReparsePoint(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Export cannot replace or copy a symbolic link or junction: " + path);
            }
        }
    }
}
