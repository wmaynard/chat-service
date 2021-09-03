using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	// TODO: Magic Values
	// TODO: Documentation
	[EnableCors(Startup.CORS_SETTINGS_NAME)]
	[ApiController, Route(template: "chat/admin"), Produces(contentType: "application/json")]
	public class AdminController : ChatControllerBase
	{
		private readonly BanService _banService;
		private readonly ReportService _reportService;

		public AdminController(BanService bans, ReportService reports, RoomService rooms, IConfiguration config) : base(rooms, config)
		{
			_banService = bans;
			_reportService = reports;
		}

		[HttpGet, Route(template: "rooms/list")]
		public ActionResult ListAllRooms([FromHeader(Name = AUTH)] string auth)
		{
			TokenInfo token = ValidateAdminToken(auth);
			return Ok(CollectionResponseObject(_roomService.List()));
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

			return Ok(room.ResponseObject);
		}

		[HttpPost, Route(template: "messages/sticky")]
		public ActionResult Sticky([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			Message message = Message.FromJToken(ExtractRequiredValue("message", body), token.AccountId);
			message.Type = Message.TYPE_STICKY;
			string language = ExtractOptionalValue("language", body)?.ToObject<string>();

			Log.Info(Owner.Will, "New sticky message issued.", token, data: message);
			Room stickies = null;
			try
			{
				stickies = _roomService.GetStickyRoom();
			}
			catch (RoomNotFoundException)
			{
				Log.Info(Owner.Will, "Sticky room not found; creating it now.");
				stickies = new Room()
				{
					Capacity = Room.MESSAGE_CAPACITY,
					Type = Room.TYPE_STICKY,
					Language = language
				};
				_roomService.Create(stickies);
			}
			
			if (string.IsNullOrEmpty(message.Text))
			{
				_roomService.DeleteStickies();
				return Ok();
			}
			stickies.AddMessage(message);
			foreach (Room r in _roomService.GetGlobals())
			{
				r.AddMessage(message);
				_roomService.Update(r);
			}
			_roomService.Update(stickies);
			return Ok(stickies.ResponseObject);
		}

		[HttpPost, Route("reports/ignore")]
		public ActionResult IgnoreReport([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			string reportId = ExtractRequiredValue("reportId", body).ToObject<string>();
			
			Report report = _reportService.Get(reportId);
			report.Status = Report.STATUS_BENIGN;
			_reportService.Update(report);
			return Ok(report.ResponseObject);
		}

		[HttpPost, Route("reports/delete")]
		public ActionResult DeleteReport([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			string reportId = ExtractRequiredValue("reportId", body).ToObject<string>();

			Report report = _reportService.Get(reportId);
			_reportService.Remove(report);
			return Ok(report.ResponseObject);
		}
		
		[HttpPost, Route(template: "messages/unsticky")]
		public ActionResult Unsticky([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			string messageId = ExtractRequiredValue("messageId", body).ToObject<string>();

			Room room = _roomService.GetStickyRoom();
			room.Messages.Remove(room.Messages.First(m => m.Id == messageId));
			_roomService.Update(room);
			
			return Ok(room.ResponseObject);
		}
		#endregion messages

		#region players
		[HttpPost, Route(template: "ban/player")]
		public ActionResult Ban([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			string accountId = ExtractRequiredValue("aid", body).ToObject<string>();
			string reason = ExtractRequiredValue("reason", body).ToObject<string>();
			string reportId = ExtractOptionalValue("reportId", body)?.ToObject<string>();
			long? duration = ExtractOptionalValue("durationInSeconds", body)?.ToObject<long?>();
			long? expiration = duration == null ? null : DateTimeOffset.Now.AddSeconds((double)duration).ToUnixTimeSeconds();

			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(accountId);
			Ban ban = new Ban(accountId, reason, expiration, rooms);
			_banService.Create(ban);

			
			foreach (Room r in rooms)
			{
				r.AddMessage(new Message()
				{
					AccountId = accountId,
					Text = $"Player {accountId} was banned by an administrator.",
					Type = Message.TYPE_BAN_ANNOUNCEMENT
				});
				_roomService.Update(r);
			}

			return Ok(ban.ResponseObject);
		}

		[HttpPost, Route(template: "ban/lift")]
		public ActionResult Unban([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateAdminToken(auth);
			string banId = ExtractRequiredValue("banId", body).ToObject<string>();

			Ban ban = _banService.Get(banId);
			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(ban.AccountId);
			foreach (Room r in rooms)
			{
				r.AddMessage(new Message()
				{
					AccountId = ban.AccountId,
					Text = $"Ban {ban.Id} was lifted by an administrator.",
					Type = Message.TYPE_UNBAN_ANNOUNCEMENT
				});
				_roomService.Update(r);
			}
			
			_banService.Remove(banId);

			return Ok();
		}

		[HttpGet, Route(template: "ban/list")]
		public ActionResult ListBans([FromHeader(Name = AUTH)] string auth)
		{
			TokenInfo token = ValidateAdminToken(auth);

			return Ok(CollectionResponseObject(_banService.List()));
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

		[HttpGet, Route(template: "environment")]
		public ActionResult EnvironmentLst([FromHeader(Name = AUTH)] string auth)
		{
			TokenInfo token = ValidateAdminToken(auth);

			IDictionary vars = Environment.GetEnvironmentVariables();
			
			List<object> output = new List<object>();
			foreach (string key in vars.Keys)
			{
				output.Add(new
				{
					Key = key,
					Value = vars[key]
				});
			}

			return Ok(new {EnvironmentVariables = output});
		}

		[HttpPost, Route("slackHandler")]
		public ActionResult SlackHandler()
		{
			// TODO: Send slack interactions here (e.g. button presses)
			// Can serialize necessary information (e.g. tokens) in the value field when creating buttons
			// And deserialize it to accomplish admin things in a secure way
			return Ok();
		}
	}

	[BindProperties(SupportsGet = false)]
	public class Fugal
	{
		[BindProperty]
		public string Param1 { get; set; }
		[BindProperty]
		public string Param2 { get; set; }
	}
}