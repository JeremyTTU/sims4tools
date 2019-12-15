using CASPartResource;
using s4pi.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace S4PIDemoFE
{
    [Serializable]
    public class PackageCache
    {
        public List<BasicItem> Items { get; set; } = new List<BasicItem>();
        public string PackageName { get; set; }
        public string FolderId { get; set; }

        public PackageCache(string packagePath)
        {
            PackageName = Path.GetFileName(packagePath);
            FolderId = Path.GetFileName(Path.GetDirectoryName(packagePath));
        }

        public void Save()
        {
            if (!Directory.Exists(Path.Combine(MainForm.BASEPATH, "PackageCache")))
                Directory.CreateDirectory(Path.Combine(MainForm.BASEPATH, "PackageCache"));
            if (!Directory.Exists(Path.Combine(MainForm.BASEPATH, "PackageCache", FolderId)))
                Directory.CreateDirectory(Path.Combine(MainForm.BASEPATH, "PackageCache", FolderId));
            using (var output = File.Create(Path.Combine(MainForm.BASEPATH, "PackageCache", FolderId, Path.GetFileNameWithoutExtension(PackageName) + ".cache")))
                new BinaryFormatter().Serialize(output, this);
        }

        public static PackageCache Load(string packageCache)
        {
            using (var output = File.OpenRead(packageCache))
                return (PackageCache)new BinaryFormatter().Deserialize(output);
        }

        public override string ToString()
        {
            return $"{FolderId}/{PackageName}";
        }
    }

    [Serializable]
    public class BasicItem
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string PropertyID { get; set; }
        public uint PartTitleKey { get; set; }
        public byte ParentKey { get; internal set; }
        public uint PartDescriptionKey { get; internal set; }
    }

    [Serializable]
    public class ElementItem : BasicItem
    {
    }

    [Serializable]
    public class CasItem : BasicItem
    {
        public AgeGenderFlags AgeGender { get; set; }
        public BodyType BodyType { get; internal set; }
        public int BodySubType { get; internal set; }
    }
}
