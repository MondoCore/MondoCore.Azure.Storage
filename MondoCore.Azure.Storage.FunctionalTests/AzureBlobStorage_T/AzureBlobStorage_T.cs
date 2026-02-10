using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.IO;

using MondoCore.Common;
using Azure;
using System.Reflection.Metadata;
using System.Collections;

namespace MondoCore.Azure.Storage.FunctionalTests
{
    [TestClass]
    [TestCategory("Functional Tests")]
    public class AzureBlobStorage_TTests
    {
        private string _container = "test2";

        #region Reader

        [TestMethod]
        [DataRow("AzureBlobStorage", 525)]
        [DataRow("AzureAppendBlobStorage", 525)]
        [DataRow("AzurePageBlobStorage", 525)]
        [DataRow("AzureBlobStorage", 2047)]
        [DataRow("AzureAppendBlobStorage", 2047)]
        [DataRow("AzurePageBlobStorage", 2047)]
        public async Task AzureBlobStorage_GetBytes(string type, int length)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;
            var encoding = UTF8Encoding.UTF8;
            var sb = new StringBuilder();

            while(sb.Length < length)
                sb.AppendLine(Guid.NewGuid().ToString());

            var content = sb.ToString();

            await writer.Put("bob", content);

            var bytes = await reader.GetBytes("bob");

            if(type == "AzurePageBlobStorage")
            { 
                var stripped = bytes.StripNulls();
                Assert.AreEqual(content, encoding.GetString(stripped.Bytes, 0, stripped.Length));
            }
            else
                Assert.AreEqual(content, encoding.GetString(bytes));

            await writer.Delete("bob");
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_Get(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;

            await writer.Put("bob", "fred");

            Assert.AreEqual("fred", await reader.Get("bob"));

            await writer.Delete("bob");
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorageGet_notfound(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;

            await Assert.ThrowsAsync<FileNotFoundException>(async ()=> await reader.Get("george"));
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorageGetBytes_notfound(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;

            await Assert.ThrowsAsync<FileNotFoundException>(async ()=> await reader.GetBytes("george"));
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_Get_stream(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;

            var uid = Guid.NewGuid().ToString();

            await writer.Put(uid, "fred");

            using(var strm = new MemoryStream())
            { 
                await reader.Get(uid, strm);

                Assert.AreEqual("fred", await strm.ReadStringAsync());
            }

            await writer.Delete(uid);
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_OpenRead(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;
            var uid    = Guid.NewGuid().ToString();

            await writer.Put(uid, "fred");

            using(var strm = await reader.OpenRead(uid))
            { 
                var canSeek = strm.CanSeek;

                Assert.AreEqual("fred", await strm.ReadStringAsync());
            }

            await writer.Delete(uid);
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_FindAll(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;

            await writer.Put("bio.doc",       "fred");
            await writer.Put("photo.jpg",     "flintstone");
            await writer.Put("resume.pdf",    "bedrock");
            await writer.Put("portfolio.pdf", "stuff");

            var result = await reader.Find("*.*");

            Assert.AreEqual(4, result.Count());

            result = await reader.Find("*.*");

            Assert.AreEqual(4, result.Count());

            await writer.Delete("bio.doc");
            await writer.Delete("photo.jpg");
            await writer.Delete("resume.pdf");
            await writer.Delete("portfolio.pdf");
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_Find(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;

            await writer.Put("bio.doc",       "fred");
            await writer.Put("photo.jpg",     "flintstone");
            await writer.Put("resume.pdf",    "bedrock");
            await writer.Put("portfolio.pdf", "stuff");

            var result = await reader.Find("*.*");

            Assert.AreEqual(4, result.Count());

            result = await reader.Find("*.pdf");

            Assert.AreEqual(2, result.Count());

            await writer.Delete("bio.doc");
            await writer.Delete("photo.jpg");
            await writer.Delete("resume.pdf");
            await writer.Delete("portfolio.pdf");
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_Enumerate(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;

            await writer.Put("docs/bio.doc",       "fred");
            await writer.Put("photos/photo.jpg",     "flintstone");
            await writer.Put("resumes/resume.pdf",    "bedrock");
            await writer.Put("stuff/portfolio.pdf", "stuff");

            var result = new List<string>();

            await reader.Enumerate("*.*", async (blob)=>
            {
                result.Add(blob.Name);

                await Task.CompletedTask;
            }, 
            false);

            Assert.AreEqual(4, result.Count());

            Assert.Contains("docs/bio.doc", result);
            Assert.Contains("photos/photo.jpg", result);
            Assert.Contains("resumes/resume.pdf", result);
            Assert.Contains("stuff/portfolio.pdf", result);

            await writer.Delete("docs/bio.doc");
            await writer.Delete("photos\\photo.jpg");
            await writer.Delete("resumes/resume.pdf");
            await writer.Delete("stuff/portfolio.pdf");
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_Enumerate_folder(string type)
        {
            var store   = CreateStorage(type, "cars/chevy");
            var store2  = CreateStorage(type, "cars/pontiac");
            var reader  = store.Reader;
            var writer  = store.Writer;
            var reader2 = store2.Reader;
            var writer2 = store2.Writer;


            await writer2.Put("firebird.tiff",       "fred");
            await writer.Put("docs/bio.doc",       "fred");
            await writer.Put("photos/photo.jpg",     "flintstone");
            await writer.Put("resumes/resume.pdf",    "bedrock");
            await writer.Put("stuff/portfolio.pdf", "stuff");

            var result = new List<string>();

            await reader.Enumerate("*.*", async (blob)=>
            {
                result.Add(blob.Name);

                await Task.CompletedTask;
            }, 
            false);

            Assert.AreEqual(4, result.Count());

            Assert.Contains("docs/bio.doc", result);
            Assert.Contains("photos/photo.jpg", result);
            Assert.Contains("resumes/resume.pdf", result);
            Assert.Contains("stuff/portfolio.pdf", result);

            await writer.Delete("docs/bio.doc");
            await writer.Delete("photos/photo.jpg");
            await writer.Delete("resumes/resume.pdf");
            await writer.Delete("stuff/portfolio.pdf");
            await writer2.Delete("firebird.tiff");
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_AsAsyncEnumerable(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;

            await writer.Put("docs/bio.doc",         "fred");
            await writer.Put("photos/photo.jpg",     "flintstone");
            await writer.Put("resumes/resume.pdf",   "bedrock");
            await writer.Put("stuff/portfolio.pdf",  "stuff");

            var list = new List<IBlob>();

            var blobs = reader.AsAsyncEnumerable();
        
            await foreach(var blob in blobs)
            {
                list.Add(blob);
            }

            var result = list.Select(b=> b.Name).ToList();

            Assert.AreEqual(4, result.Count());

            Assert.Contains("docs/bio.doc", result);
            Assert.Contains("photos/photo.jpg", result);
            Assert.Contains("resumes/resume.pdf", result);;
            Assert.Contains("stuff/portfolio.pdf", result);

            await writer.Delete("docs/bio.doc");
            await writer.Delete("photos\\photo.jpg");
            await writer.Delete("resumes/resume.pdf");
            await writer.Delete("stuff/portfolio.pdf");
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_AsAsyncEnumerable_w_prefix(string type)
        {
            var store  = CreateStorage(type, "", "");
            var reader = store.Reader;
            var writer = store.Writer;

            await writer.Put("frank_docs/bio.doc",         "fred");
            await writer.Put("frank_photos/photo.jpg",     "flintstone");
            await writer.Put("frank_resumes/resume.pdf",   "bedrock");
            await writer.Put("stuff/portfolio.pdf",  "stuff");

            var list = new List<IBlob>();

            var blobs = reader.AsAsyncEnumerable("frank");
        
            await foreach(var blob in blobs)
            {
                list.Add(blob);
            }

            var result = list.Select(b=> b.Name).ToList();

            Assert.AreEqual(3, result.Count());

            Assert.Contains("frank_docs/bio.doc", result);
            Assert.Contains("frank_photos/photo.jpg", result);
            Assert.Contains("frank_resumes/resume.pdf", result);;

            await writer.Delete("frank_docs/bio.doc");
            await writer.Delete("frank_photos/photo.jpg");
            await writer.Delete("frank_resumes/resume.pdf");
            await writer.Delete("stuff/portfolio.pdf");
        }

        #endregion

        #region Writer

        [TestMethod]
        [DataRow("AzureBlobStorage", 1)]
        [DataRow("AzureBlobStorage", 10)]
        [DataRow("AzureBlobStorage", 100)]
        [DataRow("AzureAppendBlobStorage", 1)]
        [DataRow("AzureAppendBlobStorage", 10)]
        [DataRow("AzureAppendBlobStorage", 100)]
        //[DataRow("AzureBlobStorage", 1000)]
        [DataRow("AzurePageBlobStorage", 1)]
        [DataRow("AzurePageBlobStorage", 10)]
        [DataRow("AzurePageBlobStorage", 100)]
        //[DataRow("AzurePageBlobStorage", 1000)]
        public async Task AzureBlobStorage_Put_string(string type, int numItems)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;
            var ids = new string[numItems];
            var idList = ids.Select( i=> Guid.NewGuid().ToString()).ToList();
            var tasks = new List<Task>();

            foreach(var id in idList)
                tasks.Add(writer.Put(id, id));

            await Task.WhenAll(tasks);

            foreach(var id in idList)
            { 
                Assert.AreEqual(id, await reader.Get(id));
                await writer.Delete(id);
            }
        }

        [TestMethod]
        [DataRow("AzureBlobStorage", 1)]
        [DataRow("AzureBlobStorage", 10)]
        [DataRow("AzureBlobStorage", 100)]
        [DataRow("AzureAppendBlobStorage", 1)]
        [DataRow("AzureAppendBlobStorage", 10)]
        [DataRow("AzureAppendBlobStorage", 100)]
        //[DataRow("AzureBlobStorage", 1000)]
        [DataRow("AzurePageBlobStorage", 1)]
        [DataRow("AzurePageBlobStorage", 10)]
        [DataRow("AzurePageBlobStorage", 100)]
        //[DataRow("AzurePageBlobStorage", 1000)]
        public async Task AzureBlobStorage_Put_string_w_lease(string type, int numItems)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;
            var ids = new string[numItems];
            var idList = ids.Select( i=> Guid.NewGuid().ToString()).ToList();
            var tasks = new List<Task>();

            foreach(var id in idList)
                tasks.Add(PutLease(writer, id, id));

            await Task.WhenAll(tasks);

            foreach(var id in idList)
            { 
                Assert.AreEqual(id, await reader.Get(id));
                await writer.Delete(id);
            }
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(10)]
        [DataRow(100)]
        [DataRow(1000)]
        [DataRow(5000)]
        public async Task AzureBlobStorage_Append_string(int numItems)
        {
            var store  = CreateStorage("AzureAppendBlobStorage");
            var reader = store.Reader;
            var writer = store.Writer;
            var ids = new string[numItems];
            var idList = ids.Select( i=> Guid.NewGuid().ToString()).ToList();
            var tasks = new List<Task>();

            foreach(var id in idList)
                tasks.Add(Append(writer, id));

            await Task.WhenAll(tasks);

            var content = await reader.Get("bob");

            foreach(var id in idList)
            { 
                Assert.Contains(id, content);
            }

            await writer.Delete("bob");
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(10)]
        [DataRow(100)]
        public async Task AzureBlobStorage_Append_string_w_lease_sync(int numItems)
        {
            var store  = CreateStorage("AzureAppendBlobStorage");
            var reader = store.Reader;
            var writer = store.Writer;
            var ids = new string[numItems];
            var idList = ids.Select( i=> Guid.NewGuid().ToString()).ToList();

            foreach(var id in idList)
                await PutLease(writer, "bob", id + "\r\n");

            var content = await reader.Get("bob");

            foreach(var id in idList)
            { 
                Assert.Contains(id, content);
            }

            await writer.Delete("bob");
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(3)]
        [DataRow(10)]
        [DataRow(100)]
        //[DataRow(1000)] // This will most likely fail
        public async Task AzureBlobStorage_Append_string_w_lease_async(int numItems)
        {
            var store  = CreateStorage("AzureAppendBlobStorage");
            var reader = store.Reader;
            var writer = store.Writer;
            var ids = new string[numItems];
            var idList = ids.Select( i=> Guid.NewGuid().ToString()).ToList();
            var tasks = new List<Task>();

            foreach(var id in idList)
                tasks.Add(PutLease(writer, "bob", id + "\r\n"));

            await Task.WhenAll(tasks);

            var content = await reader.Get("bob");

            foreach(var id in idList)
            { 
                Assert.Contains(id, content);
            }

            await writer.Delete("bob");
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_Put_stream(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;
            var encoding = UTF8Encoding.UTF8;

            using(var stream = new MemoryStream(encoding.GetBytes("fred")))
            { 
                await writer.Put("bob", stream);
            }

            Assert.AreEqual("fred", await reader.Get("bob"));

            await writer.Delete("bob");
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_Delete(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;

            await writer.Delete("bob");
            await writer.Put("bob", "fred");

            Assert.AreEqual("fred", await reader.Get("bob"));

            await writer.Delete("bob");

            await Task.Delay(100);

            Assert.IsFalse(await reader.Exists("bob"));
        }

        [TestMethod]
        [DataRow("AzureBlobStorage")]
        [DataRow("AzureAppendBlobStorage")]
        [DataRow("AzurePageBlobStorage")]
        public async Task AzureBlobStorage_OpenWrite(string type)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;

            await writer.Delete("bob");
            
            var ids = new string[32];
            var idList = ids.Select( i=> Guid.NewGuid().ToString()).ToList();
            
            await using(var stream = await writer.OpenWrite("bob"))
            { 
                foreach(var id in idList)
                    await stream.WriteAsync(id + "\r\n");
            }

            var content = await reader.Get("bob");

            foreach(var id in idList)
            { 
                Assert.Contains(id, content);
            }
        }

        [TestMethod]
        [DataRow("AzureBlobStorage", 1)]
        [DataRow("AzureAppendBlobStorage", 1)]
        [DataRow("AzurePageBlobStorage", 1)] 
        public async Task AzureBlobStorage_OpenWrite_w_lease(string type, int numItems)
        {
            var store  = CreateStorage(type);
            var reader = store.Reader;
            var writer = store.Writer;
          
            var content = new string[32];
            var contentLilst = content.Select( i=> Guid.NewGuid().ToString()).ToList();
            var ids = new string[numItems];
            var idList = ids.Select( i=> Guid.NewGuid().ToString()).ToList();

            var tasks = new List<Task>();

            foreach(var id in idList)
                await writer.Delete(id);

            foreach(var id in idList)
            { 
                tasks.Add(OpenWriteLease(writer, id, contentLilst));
            }

            await Task.WhenAll(tasks);

            foreach(var id in idList)
            { 
                var contents = await reader.Get(id);

                foreach(var idContent in contentLilst)
                { 
                    Assert.Contains(idContent, contents);
                }
            }
        }

        #endregion
        
        private async Task Append(IBlobStoreWriter<object> writer, string id)
        {
            Random r = new();
            var delay = r.Next(500);

            await Task.Delay(delay);

            await writer.Put("bob", id + "\r\n");
        }

        private async Task OpenWriteLease(IBlobStoreWriter<object> writer, string id, IEnumerable<string> ids)
        {
            Random r = new();
            var delay = r.Next(500);

            await Task.Delay(delay);

            DateTime dtStart = DateTime.Now;

            while(true)
            { 
                try
                { 
                    await using IBlobLease lease = await writer.AcquireLease(id);

                    await using(var stream = await lease.OpenWrite())
                    { 
                        foreach(var content in ids)
                            await stream.WriteAsync(content + "\r\n");
                    }

                    return;
                }
                catch(LeaseException ex)
                { 
                    var duration = (DateTime.Now - dtStart).TotalSeconds;

                    if(duration > 120)
                        throw;

                    await Task.Delay(200);
                }
            }        
        }

        private async Task PutLease(IBlobStoreWriter<object> writer, string id, string content)
        {
            Random r = new();
            var delay = r.Next(500);

            await Task.Delay(delay);

            DateTime dtStart = DateTime.Now;

            while(true)
            { 
                try
                { 
                    await using IBlobLease lease = await writer.AcquireLease(id);

                    await lease.Put(content);
                    return;
                }
                catch(LeaseException ex)
                { 
                    var duration = (DateTime.Now - dtStart).TotalSeconds;

                    if(duration > 120)
                        throw;

                    await Task.Delay(200);
                }
            }        
        }

        private IBlobStore<object> CreateStorage(string type, string folder = "", string? prefix = null)
        { 
            prefix ??= Guid.NewGuid().ToString();
            var path   = Path.Combine(_container, prefix, folder).Replace("\\", "/");
            var config = TestConfiguration.Load();

            switch(type)
            { 
                case "AzureBlobStorage":       return new AzureBlobStorage<Car>(config.ConnectionString, path);
                case "AzurePageBlobStorage":   return new AzurePageBlobStorage<Car>(config.ConnectionString, path);
                case "AzureAppendBlobStorage": return new AzureAppendBlobStorage<Car>(config.ConnectionString, path);
                default: throw new ArgumentException("Unknown storage type");
            }
        }

        public class Car
        {
            public string? Make      {get; set;}
            public string? Model     {get; set;}
            public string? Color     {get; set;}
            public int     Year      {get; set;}
        }
    }
}
