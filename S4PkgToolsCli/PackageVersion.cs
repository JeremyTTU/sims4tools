using s4pi.Interfaces;
using s4pi.Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace S4PkgToolsCli
{
    public static class PackageVersion
    {
        public enum SimsPackageVersion { Sims2, Sims3, Sims4, Unknown };

        public static SimsPackageVersion GetVersion(IPackage package)
        {
            if (package.Major == 2)
            {
                if (package.Minor == 0)
                    return SimsPackageVersion.Sims3;
                else
                    return SimsPackageVersion.Sims4;
            }
            else if (package.Major == 1)
                return SimsPackageVersion.Sims2;
            else
                return SimsPackageVersion.Unknown;
        }

    }
}
