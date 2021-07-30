using System;
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
	// TODO: Magic Values
	// TODO: Documentation
	// TODO: Check bans on chat login
	[ApiController, Route(template: "chat/admin"), Produces(contentType: "application/json")]
	public class AdminController : ChatControllerBase
	{
		private readonly BanService _banService;

		public AdminController(BanService bans, RoomService rooms, IConfiguration config) : base(rooms, config)
		{
			_banService = bans;
		}

		[HttpGet, Route(template: "rooms/list")]
		public ActionResult ListAllRooms([FromHeader(Name = AUTH)] string auth)
		{
			TokenInfo token = ValidateToken(auth);
			return Ok(new {Rooms = _roomService.List()});
		}

		#region messages
		[HttpPost, Route(template: "messages/delete")]
		public ActionResult DeleteMessage([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			string[] messageIds = ExtractRequiredValue("messageIds", body).ToObject<string[]>();
			string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();

			Room room = _roomService.Get(roomId);
			room.Messages = room.Messages.Where(m => !messageIds.Contains(m.Id)).ToList();
			_roomService.Update(room);

			return Ok(room.ToResponseObject());
		}

		[HttpPost, Route(template: "messages/sticky")]
		public ActionResult Sticky([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			Message message = Message.FromJToken(ExtractRequiredValue("message", body), token.AccountId);
			long expires = ExtractRequiredValue("expiration", body).ToObject<long>();
			long visibleFrom = ExtractOptionalValue("visibleFrom", body)?.ToObject<long>() ?? 0;

			message.Expiration = expires;
			message.VisibleFrom = visibleFrom;
			string language = ExtractOptionalValue("language", body)?.ToObject<string>();

			Room room;
			try
			{
				room = _roomService.GetStickyRoom();
				room.Messages.Add(message);
				_roomService.Update(room);
			}
			catch (RoomNotFoundException)
			{
				room = new Room()
				{
					Capacity = Int32.MaxValue,
					Type = Room.TYPE_STICKY,
					Language = language
				};
				room.Messages.Add(message);
				_roomService.Create(room);
			}

			return Ok(room.ToResponseObject());
		}
		
		[HttpPost, Route(template: "messages/unsticky")]
		public ActionResult Unsticky([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			string messageId = ExtractRequiredValue("messageId", body).ToObject<string>();

			Room room = _roomService.GetStickyRoom();
			room.Messages.Remove(room.Messages.First(m => m.Id == messageId));
			_roomService.Update(room);
			
			return Ok(room.ToResponseObject());
		}
		#endregion messages

		#region players
		[HttpPost, Route(template: "ban/player")]
		public ActionResult Ban([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			string accountId = ExtractRequiredValue("accountId", body).ToObject<string>();
			string reason = ExtractRequiredValue("reason", body).ToObject<string>();
			long? expiration = ExtractOptionalValue("expiration", body)?.ToObject<long>();

			Ban ban = new Ban(accountId, reason, expiration, _roomService.GetRoomsForUser(accountId));
			_banService.Create(ban);

			return Ok(ban.ResponseObject);
		}

		[HttpPost, Route(template: "ban/lift")]
		public ActionResult Unban([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			string banId = ExtractRequiredValue("banId", body).ToObject<string>();
			
			_banService.Remove(banId);

			return Ok();
		}

		[HttpGet, Route(template: "ban/list")]
		public ActionResult ListBans([FromHeader(Name = AUTH)] string auth)
		{
			TokenInfo token = ValidateAdminToken(auth);
			
			return Ok(new { Bans = _banService.List() }); // TODO: ResponseObject
		}
		#endregion players

		[HttpGet, Route(template: "health")]
		public override ActionResult HealthCheck()
		{
			return Ok(
				_banService.HealthCheckResponseObject,
				_roomService.HealthCheckResponseObject
			);
		}
	}
}