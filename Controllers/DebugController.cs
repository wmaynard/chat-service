using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "chat/debug"), Produces(contentType: "application/json")]
	public class DebugController : ChatControllerBase
	{
		private readonly SettingsService _settingsService;
		public DebugController(RoomService rooms, SettingsService settings, IConfiguration config) : base(rooms, config)
		{
			_settingsService = settings;
		}

		[HttpPost, Route(template: "settings/globalUnmute")]
		public ActionResult UmuteAllThePlayers([FromBody] JObject body)
		{
			foreach (ChatSettings setting in _settingsService.List())
			{
				setting.UnmuteAll();
				_settingsService.Update(setting);
			}

			return Ok();
		}
		
#if DEBUG
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

			return Ok(CollectionResponseObject(rooms));
		}
		[HttpPost, Route(template: "rooms/create")]
		public ActionResult Create([FromBody] JObject body)
		{
			Room r = body.ToObject<Room>();
			_roomService.Create(r);
			return Ok(r.ResponseObject);
		}
		/// <summary>
		/// Adds a user to a room.  Similar to /global/join, but 'roomId' must be specified.
		/// </summary>
		[HttpPost, Route(template: "rooms/join")]
		public ActionResult Join([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string roomId = ExtractRequiredValue("roomId", body).ToString();
			PlayerInfo player = PlayerInfo.FromJToken(ExtractRequiredValue("playerInfo", body), token);

			Room room = _roomService.Get(roomId);
			room.AddMember(player);
			_roomService.Update(room);

			object updates = GetAllUpdates(token, body);
			return Ok(updates, room.ResponseObject);
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
        [HttpPost, Route(template: "settings/nuke")]
        public ActionResult NukeSettings()
        {
        	_settingsService.Nuke();
        	return Ok();
        }
#endif
		[HttpGet, Route("health")]
		public override ActionResult HealthCheck()
		{
			return Ok(_roomService.HealthCheckResponseObject);
		}
	}
}