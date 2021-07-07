using System;
using System.Collections.Generic;
using System.Linq;
using chat_service.Models;
using chat_service.Services;
using chat_service.Utilities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace chat_service.Controllers
{
	[ApiController, Route(template: "room"), Produces(contentType: "application/json")]
	public class RoomController : RumbleController
	{
		private readonly RoomService _roomService;
		public RoomController(RoomService service) => _roomService = service;

		[HttpGet, Route(template: "list")]
		public ActionResult<List<Room>> Get() => _roomService.List();
		

		[HttpPost, Route(template: "create")]
		public void Create([FromBody] JObject body)
		{
			Room r = body.ToObject<Room>();
			_roomService.Create(r);
		}

		[HttpPost, Route(template: "join")]
		public void Join([FromBody] JObject body)
		{
			string roomId = body["roomId"].ToString();
			string aid = body["aid"].ToString();
		}

		[HttpPost, Route(template: "global/join")]
		public ActionResult<Room> JoinGlobal([FromBody] JObject body)
		{
			string aid = body["aid"].ToString();	// TODO: Get from JWT
			string language = body["language"].ToString();
			List<Room> globals = _roomService.GetGlobals(language);

			Room output;
			try
			{
				output = globals.First(r => r.MemberIds.Count < r.Capacity);
				if (output.MemberIds.Add(aid))
					_roomService.Update(output);
			}
			catch (Exception ex)
			{
				output = new Room()
				{
					Language = language, 
					Capacity = 50	// TODO: Remove Magic Number
				};
				output.MemberIds.Add(aid);
				_roomService.Create(output);
			}

			return Ok(output);
		}

		[HttpPost, Route(template: "nuke")]
		public void NukeRooms()
		{
			List<Room> rooms = _roomService.List();
			foreach (Room r in rooms)
				_roomService.Remove(r);
		}
	}
}