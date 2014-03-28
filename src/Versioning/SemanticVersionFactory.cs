﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NuGet.Versioning
{
    public partial class SemanticVersion
    {
        private const RegexOptions _flags = RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;

        public static SemanticVersion Parse(string value)
        {
            SemanticVersion ver = null;
            if (!TryParse(value, out ver))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Resources.Invalidvalue, value), "value");
            }

            return ver;
        }

        /// <summary>
        /// Parses a version string using loose semantic versioning rules that allows 2-4 version components followed by an optional special version.
        /// </summary>
        public static bool TryParse(string value, out SemanticVersion version)
        {
            if (!String.IsNullOrEmpty(value))
            {
                var match = Constants.SemanticVersionStrictRegex.Match(value.Trim());

                Version versionValue;
                if (match.Success && Version.TryParse(match.Groups["Version"].Value, out versionValue))
                {
                    Version ver = NormalizeVersionValue(versionValue);

                    version = new SemanticVersion(version: ver,
                                                releaseLabels: ParseReleaseLabels(match.Groups["Release"].Value.TrimStart('-')),
                                                metadata: match.Groups["Metadata"].Value.TrimStart('+'));
                    return true;
                }
            }

            version = null;
            return false;
        }

        private static Version NormalizeVersionValue(Version version)
        {
            return new Version(version.Major,
                               version.Minor,
                               Math.Max(version.Build, 0),
                               Math.Max(version.Revision, 0));
        }

        private static IEnumerable<string> ParseReleaseLabels(string releaseLabels)
        {
            if (!String.IsNullOrEmpty(releaseLabels))
            {
                return releaseLabels.Split('.');
            }

            return null;
        }
    }
}
