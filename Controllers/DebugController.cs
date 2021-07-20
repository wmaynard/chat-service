using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp.Serialization;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "debug"), Produces(contentType: "application/json")]
	public class DebugController : ChatControllerBase
	{
		public DebugController(RoomService rooms, IConfiguration config) : base(rooms, config){}
		
		[HttpPost, Route(template: "rooms/clear")]
		public ActionResult ClearRooms()
		{
			List<Room> rooms = _roomService.List();
			foreach (Room r in rooms)
			{
				r.Members.Clear();
				r.Messages.Clear();
				_roomService.Update(r);
			}

			return Ok();
		}
		[HttpPost, Route(template: "rooms/create")]
		public ActionResult Create([FromBody] JObject body)
		{
			Room r = body.ToObject<Room>();
			_roomService.Create(r);
			return Ok(new { Room = r});
		}
		/// <summary>
		/// Adds a user to a room.  Similar to /global/join, but 'roomId' must be specified.
		/// TODO: Exception when not a global room to prevent misuse?
		/// </summary>
		/// <param name="auth">The token issued from player-service's /player/launch.</param>
		/// <param name="body">The JSON body.  'playerInfo', 'roomId', and 'language' are required fields.
		/// Expected body example:
		///	{
		///		"lastRead": 1625704809,
		///		"playerInfo": {
		///			"avatar": "demon_axe_thrower",
		///			"sn": "Corky Douglas"
		///		},
		///		"roomId": "deadbeefdeadbeefdeadbeef"
		///	}
		/// </param>
		/// <returns>A JSON response containing the Room's data.</returns>
		[HttpPost, Route(template: "rooms/join")]
		public ActionResult Join([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string roomId = ExtractRequiredValue("roomId", body).ToString();
			PlayerInfo player = PlayerInfo.FromJToken(ExtractRequiredValue("playerInfo", body), token.AccountId);

			Room room = _roomService.Get(roomId);
			room.AddMember(player);
			_roomService.Update(room);

			object updates = GetAllUpdates(token, body);	// TODO: This causes a second hit to mongo, which isn't ideal.
			object output = Merge(updates, room.ToResponseObject());
			return Ok(Merge(updates, room.ToResponseObject()));
		}
		/// <summary>
		/// Here be dragons.  Wipe out ALL rooms.  Only intended for debugging.  Must be removed before Chat goes live.
		/// </summary>
		[HttpPost, Route(template: "rooms/nuke")]
		public ActionResult NukeRooms()
		{
			List<Room> rooms = _roomService.List();
			foreach (Room r in rooms)
				_roomService.Remove(r);
			return Ok(new { RoomsDestroyed = rooms.Count });
		}
	}
}