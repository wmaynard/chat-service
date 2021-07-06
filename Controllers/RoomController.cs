using System.Collections.Generic;
using chat_service.Models;
using chat_service.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace chat_service.Controllers
{
	[Route(template: "api/room")]
	public class RoomController : ControllerBase
	{
		private readonly RoomService _roomService;
		public RoomController(RoomService service) => _roomService = service;

		[HttpGet, Route(template: "list")]
		public ActionResult<List<Room>> Get() => _roomService.List();

		[HttpPost, Route(template: "create")]
		public void Create([FromBody] JObject body)
		{
			Room r = new Room()
			{
				SomeProperty = "Hello, World!"
			};
			_roomService.Create(r);
		}
	}
}