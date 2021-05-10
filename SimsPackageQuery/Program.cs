using CASPartResource;
using s4pi.Interfaces;
using s4pi.Package;
using s4pi.Resource.Commons.CatalogTags;
using s4pi.WrapperDealer;
using S4Studio.Data.IO.Package;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimsPackageQuery
{
    class Program
    {
        static string BASE = @"C:\Users\jeremy\Documents\SIMS\TsrDownloads";
        static object SyncLock = new object();
        static Dictionary<string, HashSet<string>> datem = new Dictionary<string, HashSet<string>>();

        static void Main(string[] args)
        {
            var sqlQuery = new List<string>();

            S4PIDemoFE.MainForm.IsPackageQuery = true;
            var form = new S4PIDemoFE.MainForm();

            var db = new TheSimsDownloaderEntities();

            var ids = db.DataItemDetails.Where(o => o.Category == "Bottom" && o.Tag == "Skirt").Select(o => o.DataItemId);

            var filenames = db.DataItems.Where(o => ids.Contains(o.ID)).ToList().Select(o => Path.Combine(BASE, o.FILENAME)).ToArray();

            foreach (var filename in filenames)
            {
                var dbpf = new DBPFPackage(filename);
                foreach (var resourcePointer in dbpf.FindAll())
                {
                    Console.WriteLine($"{resourcePointer}");
                }
            }

            return;

            var colorCategory = CatalogTagRegistry.AllCategories().FirstOrDefault(o => o.Value == "Color");


            using (var sqlConnection = new SqlConnection(db.Database.Connection.ConnectionString))
            {
                sqlConnection.Open();

                using (var sqlCommand = sqlConnection.CreateCommand())
                {
                    sqlCommand.CommandText = @"SELECT ID, FILENAME, FILESIZE FROM DataItem DI WHERE GAME = 4 AND FILENAME LIKE '%.package'
                                                    AND NOT EXISTS (SELECT 1 FROM DataItemDetail DID WHERE DI.ID = DID.DataItemId)";

                    var items = new List<Tuple<int, string, int>>();

                    using (var sqlDataReader = sqlCommand.ExecuteReader())
                        while (sqlDataReader.Read())
                            items.Add(new Tuple<int, string, int>(
                                sqlDataReader.GetInt32(0),
                                Path.Combine(@"C:\Users\jeremy\Documents\SIMS\TsrDownloads", sqlDataReader.GetString(1)),
                                sqlDataReader.GetInt32(2)));

                    var count = 0;

                    Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = 8 }, item =>
                    {
                        var id = item.Item1;
                        var filename = item.Item2;
                        var filesize = item.Item3;

                        // Detect Incoming File

                        int byte1;
                        int byte2;

                        if (!File.Exists(filename))
                            return;

                        Console.Write($"{decimal.Divide(count, items.Count):P2} | {Path.GetFileName(filename)}");

                        using (var filestream = File.OpenRead(Path.Combine(@"C:\Users\jeremy\Documents\SIMS\TsrDownloads", filename)))
                        {
                            byte1 = filestream.ReadByte();
                            byte2 = filestream.ReadByte();
                        }

                        var isTemp = false;
                        var isGzip = false;

                        if (byte1 == 0x1F && byte2 == 0x8B) // GZIP
                        {
                            isGzip = true;
                            //var oldFilename = Path.Combine(@"C:\Users\jeremy\Documents\SIMS\TsrDownloads", filename);
                            //filename = Path.GetTempFileName();
                            //Decompress(oldFilename, filename);
                            //isTemp = true;
                            Console.Write(" <~ file was gzipped");
                        }
                        else if (byte1 == 'D' && byte2 == 'B')
                        {
                            Console.Write(" <~ file is a package");
                        }
                        else
                        {
                            Console.Write(" <~ UNKNOWN FILE");
                            return;
                        }

                        try
                        {
                            int crap;
                            using (var package = (isGzip ? new Package(0, Decompress(filename)) : Package.OpenPackage(0, filename)))
                            {
                                using (var sqlConnection2 = new SqlConnection(db.Database.Connection.ConnectionString))
                                {
                                    sqlConnection2.Open();
                                    using (var sqlCommand2 = sqlConnection2.CreateCommand())
                                    {
                                        sqlCommand2.CommandText = "dbo.AddDetail";
                                        sqlCommand2.CommandType = System.Data.CommandType.StoredProcedure;
                                        sqlCommand2.Prepare();

                                        var success = false;
                                        foreach (var resource in package.GetResourceList)
                                        {
                                            try
                                            {
                                                var thing = WrapperDealer.GetResource(0, package, resource);
                                                if (thing is CASPartResource.CASPartResource)
                                                {
                                                    var casp = thing as CASPartResource.CASPartResource;

                                                    foreach (var caspDetail in casp.CASFlagList)
                                                    {
                                                        if (int.TryParse(caspDetail.CompoundTag.Category.Value, out crap)) continue;

                                                        sqlCommand2.Parameters.Clear();
                                                        sqlCommand2.Parameters.AddWithValue("@Category", caspDetail.CompoundTag.Category.Value);
                                                        sqlCommand2.Parameters.AddWithValue("@Tag", caspDetail.CompoundTag.Value.Value.Replace(caspDetail.CompoundTag.Category.Value + "_", ""));
                                                        sqlCommand2.Parameters.AddWithValue("@DataItemId", id);
                                                        sqlCommand2.ExecuteNonQuery();

                                                        success = true;
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                //Console.Write($" <~ ${ex.Message}");
                                            }
                                        }

                                        if (!success)
                                        {
                                            sqlCommand2.Parameters.Clear();
                                            sqlCommand2.Parameters.AddWithValue("@Category", "Failure");
                                            sqlCommand2.Parameters.AddWithValue("@Tag", "Failure");
                                            sqlCommand2.Parameters.AddWithValue("@DataItemId", id);
                                            sqlCommand2.ExecuteNonQuery();
                                        }

                                        Console.WriteLine($" <~ {(success ? "success" : "failure")}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(" <~ ERROR");
                        }
                        finally
                        {
                            //if (isTemp)
                            //    File.Delete(filename);
                        }

                        count++;
                    });
                };
            }
        }

        private static void WriteDetails(object state)
        {
            try
            {
                using (var output = File.CreateText("DataItemDetail.txt"))
                    foreach (var key in datem.Keys)
                        output.WriteLine($"{key} ~> {string.Join(",", datem[key])}");
            }
            catch
            {
            }
        }

        public static void Decompress(string original, string temp)
        {
            using (FileStream originalFileStream = File.OpenRead(original))
            using (FileStream decompressedFileStream = File.Create(temp))
            using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                decompressionStream.CopyTo(decompressedFileStream);
        }
        public static Stream Decompress(string file)
        {
            var source = File.OpenRead(file);
            var memoryStream = new MemoryStream();
            new GZipStream(source, CompressionMode.Decompress).CopyTo(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}
