using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
		protected override string TokenAuthEndpoint { get => _config["player-service-verify"]; }
		
		private readonly RoomService _roomService;
		private readonly IConfiguration _config;
		public RoomController(RoomService service, IConfiguration config)
		{
			_config = config;
			_roomService = service;
		}

		// Unnecessary with RoomUpdates.
		// [HttpGet, Route(template: "getUsersRooms")]
		// public ActionResult<List<Room>> GetRoomsForUser([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		// {
		// 	TokenInfo token = ValidateToken(auth);
		// 	List<Room> rooms = _roomService.GetRoomsForUser(token.AccountId);
		// 	
		// 	return Ok(new { Rooms = rooms });
		// }

		/// <summary>
		/// Adds a user to a room.  Similar to /global/join, but 'roomId' must be specified.
		/// TODO: Exception when not a global room to prevent misuse?
		/// </summary>
		/// <param name="body">The JSON body.  'playerInfo', 'roomId', and 'language' are required fields.
		/// Expected body example:
		///	{
		///		"language": "en-US",
		///		"playerInfo": {
		///			"avatar": "demon_axe_thrower",
		///			"sn": "Corky Douglas"
		///		},
		///		"roomId": "deadbeefdeadbeefdeadbeef"
		///	}
		/// </param>
		/// <returns>A JSON response containing the Room's data.</returns>
		[HttpPost, Route(template: "join")]
		public ActionResult<Room> Join([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string roomId = ExtractRequiredValue("roomId", body).ToString();
			PlayerInfo player = PlayerInfo.FromJToken(ExtractRequiredValue("playerInfo", body), token.AccountId);

			try
			{
				Room r = _roomService.Get(roomId);
				r.AddMember(player);
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

		/// <summary>
		/// Intended for use when a user is logging out and needs to exit a room, or leaves a guild chat.
		/// </summary>
		/// <param name="auth"></param>
		/// <param name="body">The JSON body.  'playerInfo' and 'roomId' are required fields.
		/// Expected body example:
		///	{
		///		"playerInfo": {
		///			"avatar": "demon_axe_thrower",
		///			"sn": "Corky Douglas"
		///		},
		///		"roomId": "deadbeefdeadbeefdeadbeef"
		///	}
		/// </param>
		[HttpPost, Route(template: "leave")]
		public ActionResult Leave([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();

			try
			{
				Room r = _roomService.Get(roomId);
				r.RemoveMember(token.AccountId);
				_roomService.Update(r);
				return Ok();
			}
			catch (NotInRoomException ex)
			{
				return Problem(ex.Message);
			}
		}

		/// <summary>
		/// Adds or assignes a user to a global room.  Also removes a user from any global rooms they were already in
		/// if it's not the same room.
		/// </summary>
		/// <param name="body">The JSON body.  'playerInfo' and 'language' are required fields.  'roomId' is optional;
		/// if unspecified, the player is assigned to the next available global room.
		/// Expected body example:
		///	{
		///		"language": "en-US",
		///		"playerInfo": {
		///			"avatar": "demon_axe_thrower",
		///			"sn": "Corky Douglas"
		///		},
		///		"roomId": "deadbeefdeadbeefdeadbeef"
		///	}
		/// </param>
		/// <returns>A JSON response containing the Room's data.</returns>
		[HttpPost, Route(template: "global/join")]
		public ActionResult<Room> JoinGlobal([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			// TODO: If roomId is null, return all RoomUpdates?
			TokenInfo info = ValidateToken(auth);
			string language = ExtractRequiredValue("language", body).ToObject<string>();
			string roomId = ExtractOptionalValue("roomId", body)?.ToObject<string>();
			PlayerInfo player = PlayerInfo.FromJToken(ExtractRequiredValue("playerInfo", body), info.AccountId);

			List<Room> globals = _roomService.GetGlobals(language);
			Room joined;

			try
			{
				joined = roomId != null 
					? globals.First(g => g.Id == roomId)
					: globals.First(g => !g.IsFull);
				
				// Remove the player from every global room they're not in.  This is useful if they're switching rooms
				// and to clean up rooms without proper calls to /rooms/leave.  TODO: InRoomSince datetime so we can clean out orphaned users?
				foreach (Room r in globals.Where(g => g.HasMember(player.AccountId) && g.Id != joined.Id))
				{
					r.RemoveMember(player.AccountId);
					_roomService.Update(r);
				}
				joined.AddMember(player);
				_roomService.Update(joined);
			}
			catch (InvalidOperationException)	// All rooms with the same language are full.
			{
				if (roomId != null)
					throw new BadHttpRequestException("That room is unavailable.");
				joined = new Room()
				{
					Capacity = Room.GLOBAL_PLAYER_CAPACITY,
					Language = language,
					Type = Room.TYPE_GLOBAL
				};
				joined.AddMember(player);
				_roomService.Create(joined);
			}
			catch (AlreadyInRoomException ex)
			{
				throw new BadHttpRequestException(ex.Message);
			}

			return Ok(joined.ToResponseObject());
		}
		
#if DEBUG
		[HttpGet, Route(template: "list")]
		public ActionResult<List<Room>> Get() => _roomService.List();
		
		[HttpPost, Route(template: "create")]
		public ActionResult<Room> Create([FromBody] JObject body)
		{
			Room r = body.ToObject<Room>();
			_roomService.Create(r);
			return Ok(r);
		}
		/// <summary>
		/// Here be dragons.  Wipe out ALL rooms.  Only intended for debugging.  Must be removed before Chat goes live.
		/// </summary>
		[HttpPost, Route(template: "nuke")]
		public ActionResult NukeRooms()
		{
			List<Room> rooms = _roomService.List();
			foreach (Room r in rooms)
				_roomService.Remove(r);
			return Ok(new { RoomsDestroyed = rooms.Count });
		}
#endif
	}
}