using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "chat/rooms"), Produces(contentType: "application/json")]
	public class RoomController : ChatControllerBase
	{
		public const string POST_KEY_ROOM_ID = "roomId";
		public const string POST_KEY_PLAYER_INFO = "playerInfo";
		public const string POST_KEY_LANGUAGE = "language";
		
		// TODO: Destroy empty global rooms
		public RoomController(RoomService service, IConfiguration config) : base(service, config){}
		// Returns a list of available global rooms for the player, as dictated by their language setting.
		[HttpPost, Route(template: "available")]
		public ActionResult Available([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string language = ExtractRequiredValue(POST_KEY_LANGUAGE, body).ToObject<string>();
			
			object updates = GetAllUpdates(token, body);

			IEnumerable<Room> rooms = _roomService.GetGlobals(language);
			return Ok(updates, CollectionResponseObject(rooms));
		}
		// Intended for use when a user is logging out and needs to exit a room, or leaves a guild chat.
		[HttpPost, Route(template: "leave")]
		public ActionResult Leave([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string roomId = ExtractRequiredValue(POST_KEY_ROOM_ID, body).ToObject<string>();

			object updates = GetAllUpdates(token, body, (IEnumerable<Room> rooms) =>
			{
				Room ciao;
				try { ciao = rooms.First(r => r.Id == roomId); }
				catch (InvalidOperationException) { throw new RoomNotFoundException(roomId); }
				
				ciao.RemoveMember(token.AccountId);
				_roomService.Update(ciao);
			});
			return Ok(updates);
		}
		// Returns a user's rooms.
		[HttpPost, Route(template: "list")]
		public ActionResult List([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			object roomResponse = null;
			object updates = GetAllUpdates(token, body,(IEnumerable<Room> rooms) =>
			{
				roomResponse = CollectionResponseObject(rooms);
			});
			
			return Ok(roomResponse, updates);
		}
		// Adds or assigns a user to a global room.  Also removes a user from any global rooms they were already in
		// if it's not the same room.
		[HttpPost, Route(template: "global/join")]
		public ActionResult JoinGlobal([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string language = ExtractRequiredValue(POST_KEY_LANGUAGE, body).ToObject<string>();
			string roomId = ExtractOptionalValue(POST_KEY_ROOM_ID, body)?.ToObject<string>();
			PlayerInfo player = PlayerInfo.FromJToken(ExtractRequiredValue(POST_KEY_PLAYER_INFO, body), token);

			Room joined = _roomService.JoinGlobal(player, language, roomId);
			object updates = GetAllUpdates(token, body);

			return Ok(joined.ResponseObject, updates);
		}

		[HttpPost, Route("global/leave")]
		public ActionResult LeaveGloval([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			object updates = GetAllUpdates(token, body, rooms =>
			{
				foreach (Room room in rooms.Where(r => r.Type == Room.TYPE_GLOBAL))
				{
					room.RemoveMember(token.AccountId);
					_roomService.Update(room);
				}
			});
			return Ok(updates);
		}
		[HttpGet, Route("health")]
		public override ActionResult HealthCheck()
		{
			return Ok(_roomService.HealthCheckResponseObject);
		}

		[HttpPost, Route("update")]
		public ActionResult UpdatePlayerInfo([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			PlayerInfo info = PlayerInfo.FromJToken(ExtractRequiredValue("playerInfo", body), token);

			IEnumerable<Room> rooms = _roomService.GetPastAndPresentRoomsForUser(token.AccountId);
			foreach (Room room in rooms)
			{
				room.UpdateMember(info);
				_roomService.Update(room); // TODO: Only update user details, not whole room
			}

			return Ok(GetAllUpdates(token, body));
		}
	}
}