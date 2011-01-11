﻿using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using LoveSeat;
using MindTouch.Tasking;
using Newtonsoft.Json.Linq;
using System.Text;
using MindTouch.Dream;

#if NUNIT
using NUnit.Framework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using TestFixtureAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestFixtureSetUpAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.ClassInitializeAttribute;
using TestFixtureTearDownAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.ClassCleanupAttribute;
#endif

namespace LoveSeat.IntegrationTest
{
	[TestFixture]
	public class CouchClientTest
	{
		private static CouchClient client;
		private const string baseDatabase = "love-seat-test-base";
		private const string replicateDatabase = "love-seat-test-repli";

		private static readonly string host = ConfigurationManager.AppSettings["Host"].ToString();
		private static readonly int port = int.Parse(ConfigurationManager.AppSettings["Port"].ToString());
		private static readonly string username = ConfigurationManager.AppSettings["UserName"].ToString();
		private static readonly string password = ConfigurationManager.AppSettings["Password"].ToString();

		[TestFixtureSetUp]
#if NUNIT
		public static void Setup()
#else
		public static void Setup(TestContext o)
#endif
		{
			client = new CouchClient();//, username, password);
			client.Authenticate(username, password, new Result<bool>()).Wait();
			if (client.HasDatabase(baseDatabase, new Result<bool>()).Wait())
			{
				client.DeleteDatabase(baseDatabase, new Result<JObject>()).Wait();
			}
			client.CreateDatabase(baseDatabase, new Result<JObject>()).Wait();

			if (client.HasDatabase(replicateDatabase, new Result<bool>()).Wait())
			{
				client.DeleteDatabase(replicateDatabase, new Result<JObject>()).Wait();
			}
			client.CreateDatabase(replicateDatabase, new Result<JObject>()).Wait();
		}
		[TestFixtureTearDown]
		public static void TearDown()
		{
			//delete the test database
			if (client.HasDatabase(baseDatabase, new Result<bool>()).Wait())
			{
				client.DeleteDatabase(baseDatabase, new Result<JObject>()).Wait();
			}
			if (client.HasDatabase(replicateDatabase, new Result<bool>()).Wait())
			{
				client.DeleteDatabase(replicateDatabase, new Result<JObject>()).Wait();
			}
			if (client.HasUser("Leela"))
			{
				client.DeleteAdminUser("Leela");
			}
		}

		[Test]
		public void Should_Trigger_Replication()
		{
			var obj = client.TriggerReplication("http://" + host + ":5984/" + replicateDatabase, baseDatabase, new MindTouch.Tasking.Result<Newtonsoft.Json.Linq.JObject>()).Wait();
			Assert.IsTrue(obj != null);
		}
		[Test]
		public void Should_Create_Document_From_String()
		{
			string obj = @"{""test"": ""prop""}";
			var db = client.GetDatabase(baseDatabase);
			string id = Guid.NewGuid().ToString("N");
			var result = db.CreateDocument(id, obj, new Result<Document>()).Wait();
			Assert.IsNotNull(db.GetDocument(id,new Result<Document>()).Wait());
		}
		[Test]
		public void Should_Save_Existing_Document()
		{
			
			string obj = @"{""test"": ""prop""}";
			var db = client.GetDatabase(baseDatabase);
			string id = Guid.NewGuid().ToString("N");
			var result = db.CreateDocument(id, obj, new Result<Document>()).Wait();
			var doc = db.GetDocument(id, new Result<Document>()).Wait();
			doc["test"] = "newprop";
			var newresult = db.SaveDocument(doc, new Result<Document>()).Wait();
			Assert.AreEqual(newresult.Value<string>("test"), "newprop");
		}

		[Test]
		public void Should_Delete_Document()
		{
			var db = client.GetDatabase(baseDatabase);
			string id = Guid.NewGuid().ToString("N");
			db.CreateDocument(id, "{}", new Result<Document>()).Wait();
			var doc = db.GetDocument(id, new Result<Document>()).Wait();
			var result = db.DeleteDocument(doc.Id, doc.Rev,new Result<JObject>()).Wait();
			Assert.IsNull(db.GetDocument(id,new Result<Document>()).Wait());
		}


		[Test]
		public void Should_Determine_If_Doc_Has_Attachment()
		{
			var db = client.GetDatabase(baseDatabase);
			string id = Guid.NewGuid().ToString("N");
			db.CreateDocument(id,"{}",new Result<Document>()).Wait();
			byte[] attachment = Encoding.UTF8.GetBytes("This is a text document");
			db.AddAttachment(id, attachment, "martin.txt", "text/plain",new Result<JObject>()).Wait();
			var doc = db.GetDocument(id, new Result<Document>()).Wait();
			Assert.IsTrue(doc.HasAttachment);
		}
		[Test]
		public void Should_Return_Attachment_Names()
		{
			var db = client.GetDatabase(baseDatabase);
			db.CreateDocument(@"{""_id"":""upload""}", new Result<Document>()).Wait();
			byte[] attachment = Encoding.UTF8.GetBytes("This is a text document");
			db.AddAttachment("upload", attachment, "martin.txt", "text/plain", new Result<JObject>()).Wait();
			var doc = db.GetDocument("upload", new Result<Document>()).Wait();
			Assert.IsTrue(doc.GetAttachmentNames().Contains("martin.txt"));
		}

		[Test]
		public void Should_Create_Admin_User()
		{
			client.CreateAdminUser("Leela", "Turanga");
		}

		//[Test]
		public void Should_Delete_Admin_User()
		{
			client.DeleteAdminUser("Leela");
		}

		[Test]
		public void Should_Get_Attachment()
		{
			var db = client.GetDatabase(baseDatabase);
			db.CreateDocument(@"{""_id"":""test_upload""}", new Result<Document>()).Wait();
			var doc = db.GetDocument("test_upload", new Result<Document>()).Wait();
			var attachment = Encoding.UTF8.GetBytes("test");
			db.AddAttachment("test_upload", attachment, "test_upload.txt", "text/html", new Result<JObject>()).Wait();
			using(var stream = db.GetAttachmentStream(doc, "test_upload.txt", new Result<Stream>()).Wait())
			using (StreamReader sr = new StreamReader(stream))
			{
				string result = sr.ReadToEnd();
				Assert.IsTrue(result == "test");
			}
		}
		[Test]
		public void Should_Delete_Attachment()
		{
			var db = client.GetDatabase(baseDatabase);
			db.CreateDocument(@"{""_id"":""test_delete""}", new Result<Document>()).Wait();
			var doc = db.GetDocument("test_delete", new Result<Document>()).Wait();
			var attachment = Encoding.UTF8.GetBytes("test");
			db.AddAttachment("test_delete", attachment, "test_upload.txt", "text/html", new Result<JObject>()).Wait();
			db.DeleteAttachment("test_delete", "test_upload.txt", new Result<JObject>()).Wait();
			var retrieved = db.GetDocument("test_delete", new Result<Document>()).Wait();
			Assert.IsFalse(retrieved.HasAttachment);
		}
		[Test]
		public void Should_Return_Etag_In_ViewResults()
		{
			var db = client.GetDatabase(baseDatabase);
			db.CreateDocument(@"{""_id"":""test_eTag""}", new Result<Document>()).Wait();
			ViewResult result = db.GetAllDocuments(new Result<ViewResult>()).Wait();
			Assert.IsTrue(!string.IsNullOrEmpty(result.Etag));
		}
		[Test]
		public void Should_Get_304_If_ETag_Matches()
		{
			var db = client.GetDatabase(baseDatabase);
			db.CreateDocument(@"{""_id"":""test_eTag_exception""}", new Result<Document>()).Wait();
			ViewResult result = db.GetAllDocuments(new Result<ViewResult>()).Wait();
			ViewResult cachedResult = db.GetAllDocuments(new ViewOptions { Etag = result.Etag }, new Result<ViewResult>()).Wait();
			Assert.AreEqual(DreamStatus.NotModified, cachedResult.StatusCode);
		}

		[Test]
		public void Should_Get_Results_Quickly()
		{
			var db = client.GetDatabase("accounting");
			var startTime = DateTime.Now;
			var options = new ViewOptions { Limit = 20 };
			var result = db.View<Company>("companies_by_name", options, "accounting",new Result<ViewResult<Company>>()).Wait();
			foreach (var item in result.Items)
			{
				Console.WriteLine(item.Name);
			}
			var endTime = DateTime.Now;
			Assert.IsTrue((endTime - startTime).TotalMilliseconds < 80);
		}
	}
	public class Company
	{
		public string Name { get; set; }
	}
}
