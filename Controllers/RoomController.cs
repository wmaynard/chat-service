using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "room"), Produces(contentType: "application/json")]
	public class RoomController : RumbleController
	{
		// TODO: /global/switch
		// TODO: Destroy empty global rooms
		// TODO: JWTs
		// TODO: Squelch bad player (blacklist, delete all posts)
		
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
		public ActionResult<Room> Join([FromHeader] string bearer, [FromBody] JObject body)
		{
			string roomId = body["roomId"].ToString();
			string aid = body["aid"].ToString();
			PlayerInfo author = PlayerInfo.FromJToken(body["author"]);

			try
			{
				Room r = _roomService.Get(roomId);
				r.AddMember(author);
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
			string aid = ExtractRequiredValue("aid", body).ToString();
			string language = ExtractRequiredValue("language", body).ToString();
			List<Room> globals = _roomService.GetGlobals(language);
			PlayerInfo author = PlayerInfo.FromJToken(body["author"]);

			Room output;
			try
			{
				output = globals.First(r => !r.IsFull);
				if (output.AddMember(author))
					_roomService.Update(output);
			}
			catch (InvalidOperationException)	// All rooms with the same language are full.
			{
				output = new Room()
				{
					Capacity = 50, // TODO: Remove Magic Number
					Language = language,
					Type = Room.TYPE_GLOBAL
				};
				output.AddMember(author);
				_roomService.Create(output);
			}
			catch (AlreadyInRoomException ex)
			{
				throw new BadHttpRequestException(ex.Message);
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