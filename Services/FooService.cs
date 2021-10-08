using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Services
{
	public class Foo : PlatformCollectionDocument
	{
		[BsonElement("data")]
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public object Data { get; set; }

		public Foo() {}
	}
	
	[ApiController, Route("foos"), RequireAuth]
	public class FooController : PlatformController
	{
		private readonly FooService _fooService;
		
		public FooController(FooService fooService, IConfiguration config) : base(config)
		{
			_fooService = fooService;
		}

		[HttpGet, Route("health")]
		public override ActionResult HealthCheck()
		{
			return Ok(_fooService.HealthCheckResponseObject);
		}

		[HttpPost, Route("new")]
		public ObjectResult MakeNew()
		{
			Foo foo = new Foo()
			{
				Data = JsonHelper.RawJsonFrom(Require<JObject>("data"))//Body)
			};
			_fooService.Create(foo);

			return Ok(foo.ResponseObject);
		}
		
		[HttpGet, Route("list")]
		public ObjectResult List()
		{
			return Ok(CollectionResponseObject(_fooService.List())); // TODO: Automatically do this
		}

		[HttpDelete, Route("delete")]
		public ObjectResult Delete()
		{
			_fooService.DeleteAll();
			return Ok();
		}
		
		public class FooService : PlatformMongoService<Foo>
		{
			public FooService() : base("foos") { }
		}
	}
}