using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Data.Services.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace NuGet
{
    [DataServiceKey("Id", "Version")]
    [EntityPropertyMapping("LastUpdated", SyndicationItemProperty.Updated, SyndicationTextContentKind.Plaintext, keepInContent: false)]
    [EntityPropertyMapping("Id", SyndicationItemProperty.Title, SyndicationTextContentKind.Plaintext, keepInContent: false)]
    [EntityPropertyMapping("Authors", SyndicationItemProperty.AuthorName, SyndicationTextContentKind.Plaintext, keepInContent: false)]
    [EntityPropertyMapping("Summary", SyndicationItemProperty.Summary, SyndicationTextContentKind.Plaintext, keepInContent: false)]
    [CLSCompliant(false)]
    public class DataServicePackage : IPackage
    {
        private IHashProvider _hashProvider;
        private bool _usingMachineCache;
        private string _licenseNames;
        internal IPackage _package;

        public string Id
        {
            get;
            set;
        }

        public string Version
        {
            get;
            set;
        }

        public string Title
        {
            get;
            set;
        }

        public string Authors
        {
            get;
            set;
        }

        public string Owners
        {
            get;
            set;
        }

        public Uri IconUrl
        {
            get;
            set;
        }

        public Uri LicenseUrl
        {
            get;
            set;
        }

        public Uri ProjectUrl
        {
            get;
            set;
        }

        public Uri ReportAbuseUrl
        {
            get;
            set;
        }

        public Uri GalleryDetailsUrl
        {
            get;
            set;
        }

        public string LicenseNames 
        {
            get { return _licenseNames; }
            set
            {
                _licenseNames = value;
                LicenseNameCollection = 
                    String.IsNullOrEmpty(value) ? new string[0] : value.Split(';').ToArray();
            }
        }

        public ICollection<string> LicenseNameCollection { get; private set; }

        public Uri LicenseReportUrl { get; set; }

        public Uri DownloadUrl
        {
            get
            {
                return Context.GetReadStreamUri(this);
            }
        }

        public bool Listed
        {
            get;
            set;
        }

        public DateTimeOffset? Published
        {
            get;
            set;
        }

        public DateTimeOffset LastUpdated
        {
            get;
            set;
        }

        public int DownloadCount
        {
            get;
            set;
        }

        public bool RequireLicenseAcceptance
        {
            get;
            set;
        }

        public bool DevelopmentDependency
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }

        public string Summary
        {
            get;
            set;
        }

        public string ReleaseNotes
        {
            get;
            set;
        }

        public string Language
        {
            get;
            set;
        }

        public string Tags
        {
            get;
            set;
        }

        public string Dependencies
        {
            get;
            set;
        }

        public string PackageHash
        {
            get;
            set;
        }

        public string PackageHashAlgorithm
        {
            get;
            set;
        }

        public bool IsLatestVersion
        {
            get;
            set;
        }

        public bool IsAbsoluteLatestVersion
        {
            get;
            set;
        }

        public string Copyright
        {
            get;
            set;
        }

        public string MinClientVersion
        {
            get;
            set;
        }

        private string OldHash { get; set; }

        private IPackage Package
        {
            get
            {
                EnsurePackage(MachineCache.Default);
                return _package;
            }
        }

        internal IDataServiceContext Context
        {
            get;
            set;
        }

        internal PackageDownloader Downloader { get; set; }

        internal IHashProvider HashProvider
        {
            get { return _hashProvider ?? new CryptoHashProvider(PackageHashAlgorithm); }
            set { _hashProvider = value; }
        }

        bool IPackage.Listed
        {
            get
            {
                return Listed;
            }
        }

        IEnumerable<string> IPackageMetadata.Authors
        {
            get
            {
                if (String.IsNullOrEmpty(Authors))
                {
                    return Enumerable.Empty<string>();
                }
                return Authors.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        IEnumerable<string> IPackageMetadata.Owners
        {
            get
            {
                if (String.IsNullOrEmpty(Owners))
                {
                    return Enumerable.Empty<string>();
                }
                return Owners.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public IEnumerable<PackageDependencySet> DependencySets
        {
            get
            {
                if (String.IsNullOrEmpty(Dependencies))
                {
                    return Enumerable.Empty<PackageDependencySet>();
                }

                return ParseDependencySet(Dependencies);
            }
        }

        public ICollection<PackageReferenceSet> PackageAssemblyReferences
        {
            get 
            {
                return Package.PackageAssemblyReferences;
            }
        }

        NuGetVersion IPackageName.Version
        {
            get
            {
                if (Version != null)
                {
                    return NuGetVersion.Parse(Version);
                }
                return null;
            }
        }

        Version IPackageMetadata.MinClientVersion
        {
            get
            {
                if (!String.IsNullOrEmpty(MinClientVersion))
                {
                    return new Version(MinClientVersion);
                }

                return null;
            }
        }

        public IEnumerable<IPackageAssemblyReference> AssemblyReferences
        {
            get
            {
                return Package.AssemblyReferences;
            }
        }

        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies
        {
            get
            {
                return Package.FrameworkAssemblies;
            }
        }

        public virtual IEnumerable<FrameworkName> GetSupportedFrameworks()
        {
            return Package.GetSupportedFrameworks();
        }

        public IEnumerable<IPackageFile> GetFiles()
        {
            return Package.GetFiles();
        }

        public Stream GetStream()
        {
            return Package.GetStream();
        }

        public override string ToString()
        {
            return this.GetFullName();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        internal void EnsurePackage(IPackageCacheRepository cacheRepository)
        {
            // OData caches instances of DataServicePackage while updating their property values. As a result, 
            // the ZipPackage that we downloaded may no longer be valid (as indicated by a newer hash). 
            // When using MachineCache, once we've verified that the hashes match (which happens the first time around),
            // we'll simply verify the file exists between successive calls.
            IPackageMetadata packageMetadata = this;
            bool refreshPackage = _package == null || 
                                  (_package is OptimizedZipPackage && !((OptimizedZipPackage)_package).IsValid) ||
                                  !String.Equals(OldHash, PackageHash, StringComparison.OrdinalIgnoreCase) || 
                                  (_usingMachineCache && !cacheRepository.Exists(Id, packageMetadata.Version));
            if (refreshPackage && 
                TryGetPackage(cacheRepository, packageMetadata, out _package) && 
                _package.GetHash(HashProvider).Equals(PackageHash, StringComparison.OrdinalIgnoreCase))
            {
                OldHash = PackageHash;

                // Reset the flag so that we no longer need to download the package since it exists and is valid.
                refreshPackage = false;

                // Make a note that the backing store for the ZipPackage is the machine cache.
                _usingMachineCache = true;
            }

            if (refreshPackage)
            {
                // We either do not have a package available locally or they are invalid. Download the package from the server.
                _usingMachineCache = cacheRepository.InvokeOnPackage(packageMetadata.Id, packageMetadata.Version,
                    (stream) => Downloader.DownloadPackage(DownloadUrl, this, stream)
                    );

                if (_usingMachineCache)
                {
                    _package = cacheRepository.FindPackage(packageMetadata.Id, packageMetadata.Version);
                    Debug.Assert(_package != null);
                }
                else
                {
                    // this can happen when access to the %LocalAppData% directory is blocked, e.g. on Windows Azure Web Site build
                    using (var targetStream = new MemoryStream())
                    {
                        Downloader.DownloadPackage(DownloadUrl, this, targetStream);
                        targetStream.Seek(0, SeekOrigin.Begin);
                        _package = new ZipPackage(targetStream);
                    }
                }

                OldHash = PackageHash;
            }
        }

        private static List<PackageDependencySet> ParseDependencySet(string value)
        {
            var dependencySets = new List<PackageDependencySet>();

            var dependencies = value.Split('|').Select(ParseDependency).ToList();

            // group the dependencies by target framework
            var groups = dependencies.GroupBy(d => d.Item3);

            dependencySets.AddRange(
                groups.Select(g => new PackageDependencySet(
                                           g.Key,   // target framework 
                                           g.Where(pair => !String.IsNullOrEmpty(pair.Item1))       // the Id is empty when a group is empty.
                                            .Select(pair => new PackageDependency(pair.Item1, pair.Item2)))));     // dependencies by that target framework
            return dependencySets;
        }

        /// <summary>
        /// Parses a dependency from the feed in the format:
        /// id or id:VersionRange, or id:VersionRange:targetFramework
        /// </summary>
        private static Tuple<string, NuGetVersionRange, FrameworkName> ParseDependency(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // IMPORTANT: Do not pass StringSplitOptions.RemoveEmptyEntries to this method, because it will break 
            // if the version spec is null, for in that case, the Dependencies string sent down is "<id>::<target framework>".
            // We do want to preserve the second empty element after the split.
            string[] tokens = value.Trim().Split(new[] { ':' });

            if (tokens.Length == 0)
            {
                return null;
            }

            // Trim the id
            string id = tokens[0].Trim();
            
            NuGetVersionRange versionRange = null;
            if (tokens.Length > 1)
            {
                // Attempt to parse the version
                NuGetVersionRange.TryParse(tokens[1], out versionRange);
            }

            var targetFramework = (tokens.Length > 2 && !String.IsNullOrEmpty(tokens[2]))
                                    ? VersionUtility.ParseFrameworkName(tokens[2])
                                    : null;

            return Tuple.Create(id, versionRange, targetFramework);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to return null if any error occurred while trying to find the package.")]
        private static bool TryGetPackage(IPackageRepository repository, IPackageMetadata packageMetadata, out IPackage package)
        {
            try
            {
                package = repository.FindPackage(packageMetadata.Id, packageMetadata.Version);
            }
            catch
            {
                // If the package in the repository is corrupted then return null
                package = null;
            }
            return package != null;
        }
    }
}