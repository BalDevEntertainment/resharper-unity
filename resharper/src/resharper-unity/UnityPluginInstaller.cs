using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Plugins.Unity.ProjectModel;
using JetBrains.Util;

namespace JetBrains.ReSharper.Plugins.Unity
{
    [SolutionComponent]
    public class UnityPluginInstaller
    {
        private static readonly string[] ourPluginFiles = {"RiderAssetPostprocessor.cs", "RiderPlugin.cs"};
        
        private readonly object mySyncObj = new object();

        public UnityPluginInstaller(ProjectReferenceChangeTracker changeTracker)
        {
#if RIDER
            changeTracker.RegisterProjectChangeHandler(InstallPluginIfRequired);
#endif
        }

        private void InstallPluginIfRequired(Lifetime lifetime, [NotNull] IProject project)
        {
            if (IsPluginNeeded(project))
            {
                Install(project);
            }
        }
        
        public bool IsPluginNeeded([NotNull] IProject project)
        {
            var assets = GetAssetsFolder(project);
            if (assets == null)
                return false; // not a Unity project

            var jetBrainsDir =
                assets.GetChildDirectories("Plugins").SingleItem()
                     ?.GetChildDirectories("Editor").SingleItem()
                     ?.GetChildDirectories("JetBrains").SingleItem();

            if (jetBrainsDir == null)
            {
                return true;
            }

            var existingFiles = new JetHashSet<string>(jetBrainsDir.GetChildFiles().Select(f => f.Name));
            return ourPluginFiles.Any(f => !existingFiles.Contains(f));
        }

        public void Install([NotNull] IProject project)
        {
            var assetsDir = GetAssetsFolder(project);
            if (assetsDir == null)
            {
                throw new ArgumentException("Project missing assets dir");
            }

            var pluginDir = FileSystemPath.TryParse(Path.Combine(assetsDir.FullPath, "Plugins", "Editor", "JetBrains"));
            pluginDir.CreateDirectory();
            
            lock (mySyncObj)
            {
                var existingPluginFiles = pluginDir.GetChildFiles();
                foreach (var filename in ourPluginFiles)
                {
                    var path = pluginDir.Combine(filename);
                    if (!existingPluginFiles.Contains(path))
                    {
                        var ns = typeof(UnityPluginInstaller).Namespace;
                        using (var resourceStream = Assembly.GetExecutingAssembly()
                            .GetManifestResourceStream(ns + ".Unity3dRider." + filename))
                        {
                            Assertion.AssertNotNull(resourceStream, "stream != null");
                            using (var fileStream = new FileStreamWrapper(path.ToString(), FileMode.CreateNew,
                                FileAccess.Write, FileShare.None))
                            {
                                resourceStream.CopyTo(fileStream);
                            }
                        }
                    }
                }
            }
        }

        [CanBeNull]
        private static FileSystemPath GetAssetsFolder([NotNull] IProject project)
        {
            return project.ProjectFileLocation?.Directory.GetChildDirectories("Assets").SingleItem();
        }
    }
}