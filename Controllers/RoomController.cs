using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
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
		// TODO: Update player info
		// TODO: PreviousMembers | delete when messages are dropped
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
			return Ok(Merge(new { Rooms = rooms }, updates));	// TODO: ResponseObject
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
				catch (InvalidOperationException) { throw new NotInRoomException(); }
				
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
				roomResponse = new { Rooms = rooms };
			});
			
			return Ok(Merge(roomResponse, updates));	// TODO: ResponseObject
		}
		// Adds or assigns a user to a global room.  Also removes a user from any global rooms they were already in
		// if it's not the same room.
		[HttpPost, Route(template: "global/join")]
		public ActionResult JoinGlobal([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string language = ExtractRequiredValue(POST_KEY_LANGUAGE, body).ToObject<string>();
			string roomId = ExtractOptionalValue(POST_KEY_ROOM_ID, body)?.ToObject<string>();
			PlayerInfo player = PlayerInfo.FromJToken(ExtractRequiredValue(POST_KEY_PLAYER_INFO, body), token.AccountId);

			Room joined = _roomService.JoinGlobal(player, language, roomId);
			object updates = GetAllUpdates(token, body);

			return Ok(joined.ToResponseObject(), updates);
			//
			// try
			// {
			// 	TokenInfo token = ValidateToken(auth);
			// 	string language = ExtractRequiredValue(POST_KEY_LANGUAGE, body).ToObject<string>();
			// 	PlayerInfo player = PlayerInfo.FromJToken(ExtractRequiredValue(POST_KEY_PLAYER_INFO, body), token.AccountId);
			//
			// 	// If this is specified, the user is switching global rooms.
			// 	string roomId = ExtractOptionalValue(POST_KEY_ROOM_ID, body)?.ToObject<string>();
			//
			// 	List<Room> globals = _roomService.GetGlobals(language);
			// 	Room joined = null;
			//
			// 	try
			// 	{
			// 		joined = roomId != null
			// 			? globals.First(g => g.Id == roomId)
			// 			: globals.First(g => !g.IsFull || g.HasMember(token.AccountId));
			//
			// 		// Remove the player from every global room they're not in.  This is useful if they're switching rooms
			// 		// and to clean up rooms without proper calls to /rooms/leave.  TODO: Clean out orphaned users
			// 		foreach (Room r in globals.Where(g => g.HasMember(player.AccountId) && g.Id != joined.Id))
			// 		{
			// 			r.RemoveMember(player.AccountId);
			// 			_roomService.Update(r);
			// 		}
			//
			// 		joined.AddMember(player);
			// 		_roomService.Update(joined);
			// 	}
			// 	catch (InvalidOperationException) // All rooms with the same language are full
			// 	{
			// 		if (roomId != null)
			// 			throw new BadHttpRequestException(
			// 				"That room does not exist or does not match your language setting.");
			// 		joined = new Room()
			// 		{
			// 			Capacity = Room.GLOBAL_PLAYER_CAPACITY,
			// 			Language = language,
			// 			Type = Room.TYPE_GLOBAL
			// 		};
			// 		joined.AddMember(player);
			// 		_roomService.Create(joined);
			// 	}
			// 	catch (AlreadyInRoomException)
			// 	{
			// 		// Do nothing.
			// 		// The client didn't leave the room properly, but we don't want to send an error to it, either.
			// 	}
			// 	
			// 	object updates = GetAllUpdates(token, body);
			// 	return Ok(updates, joined.ToResponseObject());
			// }
			// catch (Exception me)
			// {
			// 	string error = "";
			// 	do
			// 	{
			// 		error += me.Message + "\n";
			// 	} while ((me = me.InnerException) != null);
			//
			// 	return Problem(new {Error = error, Exception = me});
			// }
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
	}
}