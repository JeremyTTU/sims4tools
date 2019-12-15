using CASPartResource;
using s4pi.ImageResource;
using s4pi.Package;
using s4pi.Settings;
using s4pi.WrapperDealer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace S4PkgToolsCli
{
    class Program
    {
        public static string BASEFOLDER = @"D:\SimsFileShareDump";
        public static Dictionary<string, Type> TypeMap = null;


        static void Main(string[] args)
        {
            Settings.DisableChecking();

            TypeMap = new Dictionary<string, Type>();
            foreach (var entry in WrapperDealer.TypeMap)
                if (!TypeMap.ContainsKey(entry.Key))
                    TypeMap.Add(entry.Key, entry.Value);

            Console.WriteLine($"{TypeMap.Count} Types Loaded...");

            var f = Directory.EnumerateFiles(@"D:\SimsFileShareDump\FileContents", "*.package", SearchOption.AllDirectories);
            var s2 = 0;
            var s3 = 0;
            var s4 = 0;
            var u = 0;
            Console.Clear();
            foreach (var fn in f)
            {
                try
                {
                    using (var p = Package.OpenPackage(0, fn, false))
                    {
                        var version = PackageVersion.GetVersion(p);

                        switch (version)
                        {
                            case PackageVersion.SimsPackageVersion.Sims2:
                                s2++;
                                break;
                            case PackageVersion.SimsPackageVersion.Sims3:
                                s3++;
                                break;
                            case PackageVersion.SimsPackageVersion.Sims4:
                                s4++;
                                break;
                            case PackageVersion.SimsPackageVersion.Unknown:
                                u++;
                                break;
                        }
                    }
                }
                catch
                {
                }
                if ((s2 + s3 + s4 + u) % 100 == 0)
                {
                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine($"S2: {s2}");
                    Console.WriteLine($"S3: {s3}");
                    Console.WriteLine($"S4: {s4}");
                    Console.WriteLine($"UK: {u}");
                }
            }



            Application.Run(new PackageBrowser());

            return;
            var trh = new ThumbnailResourceHandler();
            foreach (var listing in ProcessedPackage.Listing.Where(o => o.PackageName.IndexOf("Merge", StringComparison.CurrentCultureIgnoreCase) == -1))
            {
                try
                {

                    if (Directory.Exists(Path.Combine(@"D:\SimsFileShareDump\FileThumbnails", listing.FolderId, listing.PackageName)) &&
                        Directory.EnumerateFiles(Path.Combine(@"D:\SimsFileShareDump\FileThumbnails", listing.FolderId, listing.PackageName)).Count() > 0)
                    {
                    }
                    else
                        using (var package = Package.OpenPackage(0, listing.Path, false))
                        {
                            if (PackageVersion.GetVersion(package) != PackageVersion.SimsPackageVersion.Sims4) continue;

                            Console.Write(listing.PackageKey);

                            var resources = package.GetResourceList.Where(o => trh[typeof(ThumbnailResource)].Contains(o.ResourceId));


                            var images = new Dictionary<ulong, List<Image>>();

                            foreach (var resource in resources)
                            {
                                var re = WrapperDealer.GetResource(0, package, resource);
                                var tr = re as ThumbnailResource;

                                if (images.ContainsKey(resource.Instance))
                                    images[resource.Instance].Add(Image.FromStream(tr.Stream));
                                else
                                    images.Add(resource.Instance, new List<Image>(new[] { Image.FromStream(tr.Stream) }));
                            }

                            var hashed = new Dictionary<string, Image>();

                            foreach (var key in images.Keys)
                            {
                                var maxHeight = images[key].Max(o => o.Height);
                                var bigImage = images[key].FirstOrDefault(o => o.Height == maxHeight);
                                if (bigImage == null) continue;

                                using (var memoryStream = new MemoryStream())
                                {
                                    bigImage.Save(memoryStream, ImageFormat.Png);
                                    memoryStream.Position = 0;
                                    var md5sum = ToHex(MD5.Create().ComputeHash(memoryStream), true);
                                    if (!hashed.ContainsKey(md5sum))
                                        hashed.Add(md5sum, bigImage);
                                }
                            }

                            var imageCount = 1;
                            foreach (var final in hashed.Values)
                            {
                                if (!Directory.Exists(Path.Combine(@"D:\SimsFileShareDump\FileThumbnails", listing.FolderId, listing.PackageName)))
                                    Directory.CreateDirectory(Path.Combine(@"D:\SimsFileShareDump\FileThumbnails", listing.FolderId, listing.PackageName));
                                using (var stream = File.Create(Path.Combine(@"D:\SimsFileShareDump\FileThumbnails", listing.FolderId, listing.PackageName, imageCount.ToString().PadLeft(5, '0') + ".png")))
                                {
                                    final.Save(stream, ImageFormat.Png);
                                    final.Dispose();
                                }
                                Console.Write(".");
                                imageCount++;
                            }


                            images.Clear();
                            images = null;

                            Console.WriteLine("!");
                        }
                }
                catch
                {
                    Console.WriteLine("FAIL");
                }
            }

            return;

            var d = new Dictionary<Type, int>();
            var cas = new Dictionary<BodyType, HashSet<string>>();
            var binaryFormatter = new BinaryFormatter();
            var count = 0;
            var packageFiles = ProcessedPackage.Listing.Where(o => o.IsValid);
            var total = packageFiles.Count();

            foreach (var packageFile in packageFiles)
            {
                try
                {
                    using (var package = Package.OpenPackage(0, packageFile.Path, false))
                    {
                        foreach (var resource in package.GetResourceList.Where(o => o.ResourceId == "0x034AEECB"))
                        {
                            try
                            {
                                var re = WrapperDealer.GetResource(0, package, resource);
                                var cpr = re as CASPartResource.CASPartResource;

                                if (cas.ContainsKey(cpr.BodyType))
                                    cas[cpr.BodyType].Add(packageFile.Path);
                                else
                                    cas.Add(cpr.BodyType, new HashSet<string>(new[] { packageFile.Path }.ToList()));
                            }
                            catch
                            {

                            }
                        }
                    }

                    //Console.SetCursorPosition(0, 0);
                    //foreach (var kvp in d)
                    //{
                    //    Console.WriteLine($"{kvp.Key.FullName.PadRight(60)} - {kvp.Value}");
                    //}
                    //foreach (var kvp in cas)
                    //{
                    //    Console.WriteLine($"{kvp.Key.ToString().PadRight(60)} - {kvp.Value}");
                    //}


                }
                catch
                {

                }
                count++;

                if (count % 100 == 0)
                {
                    Console.WriteLine($"{count}/{total}   {decimal.Divide(count, total).ToString("P2")}");

                }
            }

            using (var stream = File.Create(@"D:\SimsFileShareDump\CasParts.data"))
                binaryFormatter.Serialize(stream, cas);
        }

        private static string ToHex(byte[] bytes, bool upperCase)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

            return result.ToString();
        }
    }
}
