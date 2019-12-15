using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace S4PkgToolsCli
{
    [Serializable]
    public class ProcessedPackage
    {
        public string FolderId { get; set; }
        public string PackageName { get; set; }
        public bool IsValid { get; set; }

        public string Path => System.IO.Path.Combine(Program.BASEFOLDER, "FileContents", FolderId, PackageName);

        public ProcessedPackage(string input)
        {
            var split = input.Split('|');
            FolderId = split[0];
            PackageName = split[1];
            IsValid = bool.Parse(split[2]);
        }

        public string PackageKey => $"{FolderId}/{PackageName}";

        public override string ToString()
        {
            return $"{FolderId}/{PackageName}/{IsValid}";
        }

        public static IEnumerable<ProcessedPackage> Listing
        {
            get
            {
                var packageDatas = File.ReadAllLines(@"D:\SimsFileShareDump\Packages.data");
                foreach (var packageData in packageDatas)
                    yield return new ProcessedPackage(packageData);
            }
        }
    }
}
