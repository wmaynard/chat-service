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
using Platform.CSharp.Common.Web;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: PATH_BASE), Produces(contentType: "application/json")]
	public class RoomController : ChatControllerBase
	{
		public const string PATH_BASE = "room";
		public const string PATH_JOIN = "join";
		public const string PATH_JOIN_GLOBAL = "global/join";
		public const string PATH_LEAVE = "leave";

		public const string POST_KEY_ROOM_ID = "roomId";
		public const string POST_KEY_PLAYER_INFO = "playerInfo";
		public const string POST_KEY_LANGUAGE = "language";
		
		// TODO: Destroy empty global rooms
		// TODO: Squelch bad player (blacklist, delete all posts)
		// protected override string TokenAuthEndpoint => _config["player-service-verify"];
		
		// private readonly RoomService _roomService;
		// private readonly IConfiguration _config;
		public RoomController(RoomService service, IConfiguration config) : base(service, config){}

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
		[HttpPost, Route(template: PATH_JOIN)]
		public ActionResult<Room> Join([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string roomId = ExtractRequiredValue(POST_KEY_ROOM_ID, body).ToString();
			PlayerInfo player = PlayerInfo.FromJToken(ExtractRequiredValue(POST_KEY_PLAYER_INFO, body), token.AccountId);

			Room room = _roomService.Get(roomId);
			room.AddMember(player);
			_roomService.Update(room);

			object updates = GetAllUpdates(token, body);	// TODO: This causes a second hit to mongo, which isn't ideal.
			object output = Merge(updates, room.ToResponseObject());
			return Ok(Merge(updates, room.ToResponseObject()));
		}

		/// <summary>
		/// Intended for use when a user is logging out and needs to exit a room, or leaves a guild chat.
		/// </summary>
		/// <param name="auth">The token issued from player-service's /player/launch.</param>
		/// <param name="body">The JSON body.  'playerInfo' and 'roomId' are required fields.
		/// Expected body example:
		///	{
		///		"lastRead": 1625704809,
		///		"roomId": "deadbeefdeadbeefdeadbeef"
		///	}
		/// </param>
		[HttpPost, Route(template: PATH_LEAVE)]
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

		/// <summary>
		/// Adds or assigns a user to a global room.  Also removes a user from any global rooms they were already in
		/// if it's not the same room.
		/// </summary>
		/// <param name="auth">The token issued from player-service's /player/launch.</param>
		/// <param name="body">The JSON body.  'playerInfo' and 'language' are required fields.  'roomId' is optional;
		/// if unspecified, the player is assigned to the next available global room.
		/// Expected body example:
		///	{
		///		"language": "en-US",
		///		"lastRead": 1625704809,
		///		"playerInfo": {
		///			"avatar": "demon_axe_thrower",
		///			"sn": "Corky Douglas"
		///		},
		///		"roomId": "deadbeefdeadbeefdeadbeef"
		///	}
		/// </param>
		/// <returns>A JSON response containing the Room's data.</returns>
		[HttpPost, Route(template: PATH_JOIN_GLOBAL)]
		public ActionResult<Room> JoinGlobal([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			try
			{
				TokenInfo token = ValidateToken(auth);
				string language = ExtractRequiredValue(POST_KEY_LANGUAGE, body).ToObject<string>();
				PlayerInfo player =
					PlayerInfo.FromJToken(ExtractRequiredValue(POST_KEY_PLAYER_INFO, body), token.AccountId);

				// If this is specified, the user is switching global rooms.
				string roomId = ExtractOptionalValue(POST_KEY_ROOM_ID, body)?.ToObject<string>();

				List<Room> globals = _roomService.GetGlobals(language);
				Room joined;

				try
				{
					joined = roomId != null
						? globals.First(g => g.Id == roomId)
						: globals.First(g => !g.IsFull);

					// Remove the player from every global room they're not in.  This is useful if they're switching rooms
					// and to clean up rooms without proper calls to /rooms/leave.  TODO: Clean out orphaned users
					foreach (Room r in globals.Where(g => g.HasMember(player.AccountId) && g.Id != joined.Id))
					{
						r.RemoveMember(player.AccountId);
						_roomService.Update(r);

					}

					joined.AddMember(player);
					_roomService.Update(joined);
				}
				catch (InvalidOperationException) // All rooms with the same language are full
				{
					if (roomId != null)
						throw new BadHttpRequestException(
							"That room does not exist or does not match your language setting.");
					joined = new Room()
					{
						Capacity = Room.GLOBAL_PLAYER_CAPACITY,
						Language = language,
						Type = Room.TYPE_GLOBAL
					};
					joined.AddMember(player);
					_roomService.Create(joined);
				}
				
				object updates = GetAllUpdates(token, body);
				return Ok(Merge(updates, joined.ToResponseObject()));
			}
			catch (Exception me)
			{
				string error = "";
				do
				{
					error += me.Message + "\n";
				} while ((me = me.InnerException) != null);

				return Problem(new {Error = error, Exception = me});
			}
		}
		
		[HttpGet, Route(template: "list")]
		public ActionResult<List<Room>> Get() => _roomService.List();
		
#region Debug Only Functions
// TODO: These MUST be removed before going live
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


		[HttpPost, Route(template: "clear")]
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
#endregion Debug Only Functions
	}
}