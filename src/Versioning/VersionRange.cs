﻿using System;

namespace NuGet.Versioning
{
    /// <summary>
    /// Represents a range of versions.
    /// </summary>
    public partial class VersionRange
    {
        private readonly bool _includeMinVersion;
        private readonly bool _includeMaxVersion;
        private readonly SimpleVersion _minVersion;
        private readonly SimpleVersion _maxVersion;
        private readonly bool _includePrerelease;

        /// <summary>
        /// Creates a VersionRange with the given min and max.
        /// </summary>
        /// <param name="minVersion">Lower bound of the version range.</param>
        /// <param name="includeMinVersion">True if minVersion satisfies the condition.</param>
        /// <param name="maxVersion">Upper bound of the version range.</param>
        /// <param name="includeMaxVersion">True if maxVersion satisfies the condition.</param>
        /// <param name="includePrerelease">True if prerelease versions should satisfy the condition.</param>
        public VersionRange(SimpleVersion minVersion=null, bool includeMinVersion=true, SimpleVersion maxVersion=null, 
            bool includeMaxVersion=false, bool? includePrerelease=null)
        {
            _minVersion = minVersion;
            _maxVersion = maxVersion;
            _includeMinVersion = includeMinVersion;
            _includeMaxVersion = includeMaxVersion;

            if (includePrerelease == null)
            {
                _includePrerelease = (_maxVersion != null && IsPrerelease(_maxVersion) == true) || 
                    (_minVersion != null && IsPrerelease(_minVersion) == true);
            }
            else
            {
                _includePrerelease = includePrerelease == true;
            }
        }

        public bool HasLowerBound
        {
            get
            {
                return _minVersion != null;
            }
        }

        public bool HasUpperBound
        {
            get
            {
                return _maxVersion != null;
            }
        }

        public bool HasLowerAndUpperBounds
        {
            get
            {
                return HasLowerBound && HasUpperBound;
            }
        }

        public bool IsMinInclusive
        {
            get
            {
                return HasLowerBound && _includeMinVersion;
            }
        }

        public bool IsMaxInclusive
        {
            get
            {
                return HasUpperBound && _includeMaxVersion;
            }
        }

        public SimpleVersion MaxVersion
        {
            get
            {
                return _maxVersion;
            }
        }

        public SimpleVersion MinVersion
        {
            get
            {
                return _minVersion;
            }
        }

        /// <summary>
        /// True if pre-release versions are included in this range.
        /// </summary>
        public bool IncludePrerelease
        {
            get
            {
                return _includePrerelease;
            }
        }

        /// <summary>
        /// Determines if an NuGetVersion meets the requirements.
        /// </summary>
        /// <param name="version">SemVer to compare</param>
        /// <returns>True if the given version meets the version requirements.</returns>
        public bool Satisfies(SimpleVersion version)
        {
            // ignore metadata by default when finding a range.
            return Satisfies(version, VersionComparer.VersionRelease);
        }

        /// <summary>
        /// Determines if an NuGetVersion meets the requirements using the given mode.
        /// </summary>
        /// <param name="version">SemVer to compare</param>
        /// <param name="versionComparison">VersionComparison mode used to determine the version range.</param>
        /// <returns>True if the given version meets the version requirements.</returns>
        public bool Satisfies(SimpleVersion version, VersionComparison versionComparison)
        {
            return Satisfies(version, new VersionComparer(versionComparison));
        }

        /// <summary>
        /// Determines if an NuGetVersion meets the requirements using the version comparer.
        /// </summary>
        /// <param name="version">SemVer to compare.</param>
        /// <param name="comparer">Version comparer used to determine if the version criteria is met.</param>
        /// <returns>True if the given version meets the version requirements.</returns>
        public bool Satisfies(SimpleVersion version, IVersionComparer comparer)
        {
            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            // Determine if version is in the given range using the comparer.
            bool condition = true;
            if (HasLowerBound)
            {
                if (IsMinInclusive)
                {
                    condition &= comparer.Compare(MinVersion, version) <= 0;
                }
                else
                {
                    condition &= comparer.Compare(MinVersion, version) < 0;
                }
            }

            if (HasUpperBound)
            {
                if (IsMaxInclusive)
                {
                    condition &= comparer.Compare(MaxVersion, version) >= 0;
                }
                else
                {
                    condition &= comparer.Compare(MaxVersion, version) > 0;
                }
            }

            if (!IncludePrerelease)
            {
                condition &= IsPrerelease(version) != true;
            }

            return condition;
        }

        public override string ToString()
        {
            return ToString("N", new VersionRangeFormatter());
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            string formattedString = null;

            if (formatProvider != null)
            {
                ICustomFormatter formatter = formatProvider.GetFormat(this.GetType()) as ICustomFormatter;
                if (formatter != null)
                {
                    formattedString = formatter.Format(format, this, formatProvider);
                }
            }

            return formattedString;
        }

        public string PrettyPrint()
        {
            return ToString("P", new VersionRangeFormatter());
        }

        private static bool? IsPrerelease(SimpleVersion version)
        {
            bool? b = null;

            SemanticVersion semVer = version as SemanticVersion;
            if (semVer != null)
            {
                b = semVer.IsPrerelease;
            }

            return b;
        }
    }
}