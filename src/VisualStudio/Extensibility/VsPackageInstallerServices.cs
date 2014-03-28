﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using EnvDTE;
using NuGet.VisualStudio.Resources;
using NuGet.Versioning;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstallerServices))]
    public class VsPackageInstallerServices : IVsPackageInstallerServices
    {
        private readonly IVsPackageManagerFactory _packageManagerFactory;

        [ImportingConstructor]
        public VsPackageInstallerServices(IVsPackageManagerFactory packageManagerFactory)
        {
            _packageManagerFactory = packageManagerFactory;
        }

        public IEnumerable<IVsPackageMetadata> GetInstalledPackages()
        {
            var packageManager = _packageManagerFactory.CreatePackageManager();

            return from package in packageManager.LocalRepository.GetPackages()
                   select new VsPackageMetadata(package, packageManager.PathResolver.GetInstallPath(package));
        }

        public IEnumerable<IVsPackageMetadata> GetInstalledPackages(Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            var packageManager = _packageManagerFactory.CreatePackageManager();
            IProjectManager projectManager = packageManager.GetProjectManager(project);

            return from package in projectManager.LocalRepository.GetPackages()
                   select new VsPackageMetadata(package, packageManager.PathResolver.GetInstallPath(package));
        }

        public bool IsPackageInstalled(Project project, string packageId)
        {
            return IsPackageInstalled(project, packageId, version: null);
        }

        public bool IsPackageInstalledEx(Project project, string packageId, string versionString)
        {
            NuGetVersion version;
            if (versionString == null)
            {
                version = null;
            }
            else if (!NuGetVersion.TryParse(versionString, out version))
            {
                throw new ArgumentException(VsResources.InvalidSemanticVersionString, "versionString");
            }

            return IsPackageInstalled(project, packageId, version);
        }

        public bool IsPackageInstalled(Project project, string packageId, NuGetVersion version)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageId");
            }

            var packageManager = _packageManagerFactory.CreatePackageManager();
            IProjectManager projectManager = packageManager.GetProjectManager(project);
            if (projectManager == null)
            {
                throw new ArgumentException(VsResources.DTE_InvalidProject, "project");
            }

            return projectManager.LocalRepository.Exists(packageId, version);
        }
    }
}