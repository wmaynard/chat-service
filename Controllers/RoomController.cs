using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "chat/rooms"), Produces(contentType: "application/json"), RequireAuth]
	public class RoomController : ChatControllerBase
	{
		// TODO: Inconsistency: Either clean these out or convert other controllers to use constants as well.
		public const string KEY_ROOM_ID = "roomId";
		// public const string POST_KEY_PLAYER_INFO = "playerInfo";
		
		// TODO: Destroy empty global rooms
		public RoomController(RoomService service, IConfiguration config) : base(service, config){}
		
		#region GLOBAL
		// Adds or assigns a user to a global room.  Also removes a user from any global rooms they were already in
		// if it's not the same room.
		[HttpPost, Route(template: "global/join")]
		public ActionResult JoinGlobal()
		{
			string language = Require<string>(Room.FRIENDLY_KEY_LANGUAGE);//ExtractRequiredValue(POST_KEY_LANGUAGE, body).ToObject<string>();
			string roomId = Optional<string>(KEY_ROOM_ID);//ExtractOptionalValue(POST_KEY_ROOM_ID, body)?.ToObject<string>();
			PlayerInfo player = PlayerInfo.FromJToken(Require<JToken>(PlayerInfo.FRIENDLY_KEY_SELF), Token);//ExtractRequiredValue(POST_KEY_PLAYER_INFO, body), Token);

			Room joined = _roomService.JoinGlobal(player, language, roomId);
			
			object updates = GetAllUpdates(Token, Body);
			return Ok(joined.ResponseObject, updates);
		}

		[HttpPost, Route("global/leave")]
		public ActionResult LeaveGlobal()
		{
			object updates = GetAllUpdates(Token, Body, rooms =>
			{
				foreach (Room room in rooms.Where(r => r.Type == Room.TYPE_GLOBAL))
				{
					room.RemoveMember(Token.AccountId);
					_roomService.Update(room);
				}
			});
			return Ok(updates);
		}
		#endregion GLOBAL
		
		#region GENERAL
		// Returns a list of available global rooms for the player, as dictated by their language setting.
		[HttpPost, Route(template: "available")]
		public ActionResult Available()
		{
			string language = Require<string>(Room.FRIENDLY_KEY_LANGUAGE);//ExtractRequiredValue(POST_KEY_LANGUAGE, body).ToObject<string>();
			
			object updates = GetAllUpdates(Token, Body);

			IEnumerable<Room> rooms = _roomService.GetGlobals(language); // TODO: Only return IDs with this instead of the entire room objects.
			return Ok(updates, CollectionResponseObject(rooms));
		}
		
		// Intended for use when a user is logging out and needs to exit a room, or leaves a guild chat.
		[HttpPost, Route(template: "leave")]
		public ActionResult Leave()
		{
			string roomId = Require<string>(KEY_ROOM_ID);//ExtractRequiredValue(POST_KEY_ROOM_ID, body).ToObject<string>();

			object updates = GetAllUpdates(Token, Body, (IEnumerable<Room> rooms) =>
			{
				Room ciao;
				try { ciao = rooms.First(r => r.Id == roomId); }
				catch (InvalidOperationException) { throw new RoomNotFoundException(roomId); }
				
				ciao.RemoveMember(Token.AccountId);
				_roomService.Update(ciao);
			});
			return Ok(updates);
		}
		
		// Returns a user's rooms.
		[HttpPost, Route(template: "list")]
		public ActionResult List()
		{
			object roomResponse = null;
			// Since the RoomUpdates needs to get all of the rooms anyway, grab the room response object from them.
			object updates = GetAllUpdates(Token, Body,(IEnumerable<Room> rooms) =>
			{
				roomResponse = CollectionResponseObject(rooms);
			});
			
			return Ok(roomResponse, updates);
		}

		[HttpPost, Route("update")]
		public ActionResult UpdatePlayerInfo()
		{
			PlayerInfo info = PlayerInfo.FromJToken(Require<JToken>("playerInfo"), Token);//ExtractRequiredValue("playerInfo", body), Token);

			IEnumerable<Room> rooms = _roomService.GetPastAndPresentRoomsForUser(Token.AccountId);
			foreach (Room room in rooms)
			{
				room.UpdateMember(info);
				_roomService.Update(room); // TODO: Only update user details, not whole room
			}

			return Ok(GetAllUpdates(Token, Body));
		}
		#endregion GENERAL
		
		#region LOAD BALANCER
		[HttpGet, Route("health"), NoAuth]
		public override ActionResult HealthCheck()
		{
			return Ok(_roomService.HealthCheckResponseObject);
		}
		#endregion
	}
}