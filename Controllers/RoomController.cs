using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "room"), Produces(contentType: "application/json")]
	public class RoomController : RumbleController
	{
		private readonly RoomService _roomService;
		public RoomController(RoomService service) => _roomService = service;

		[HttpGet, Route(template: "list")]
		public ActionResult<List<Room>> Get() => _roomService.List();
		

		[HttpPost, Route(template: "create")]
		public ActionResult<Room> Create([FromBody] JObject body)
		{
			Room r = body.ToObject<Room>();
			_roomService.Create(r);
			return Ok(r);
		}

		[HttpPost, Route(template: "join")]
		public ActionResult<Room> Join([FromBody] JObject body)
		{
			string roomId = body["roomId"].ToString();
			string aid = body["aid"].ToString();

			try
			{
				Room r = _roomService.Get(roomId);
				r.AddMember(aid);
				_roomService.Update(r);
				return Ok(r);
			}
			catch (RoomFullException ex)
			{
				return Problem(ex.Message);
			}
			catch (AlreadyInRoomException ex)
			{
				return Problem(ex.Message);
			}
		}

		[HttpPost, Route(template: "leave")]
		public ActionResult<Room> Leave([FromBody] JObject body)
		{
			string roomId = body["roomId"].ToString();
			string aid = body["aid"].ToString();

			try
			{
				Room r = _roomService.Get(roomId);
				r.RemoveMember(aid);
				_roomService.Update(r);
				return Ok();
			}
			catch (NotInRoomException ex)
			{
				return Problem(ex.Message);
			}
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
			catch (InvalidOperationException)
			{
				output = new Room()
				{
					Capacity = 50,	// TODO: Remove Magic Number
					Language = language,
					Type = Room.TYPE_GLOBAL
				};
				output.MemberIds.Add(aid);
				_roomService.Create(output);
			}

			return Ok(output);
		}
		[HttpPost, Route(template: "nuke")]
		public ActionResult NukeRooms()
		{
			List<Room> rooms = _roomService.List();
			foreach (Room r in rooms)
				_roomService.Remove(r);
			return Ok(new { RoomsDestroyed = rooms.Count });
		}
	}
}