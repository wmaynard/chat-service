using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[EnableCors(PlatformStartup.CORS_SETTINGS_NAME)]
	[ApiController, Route(template: "chat/admin"), Produces(contentType: "application/json"), RequireAuth(TokenType.ADMIN)]
	public class AdminController : ChatControllerBase
	{
		private readonly BanService _banService;
		private readonly ReportService _reportService;

		public AdminController(BanService bans, ReportService reports, RoomService rooms, IConfiguration config) : base(rooms, config)
		{
			_banService = bans;
			_reportService = reports;
		}

		[HttpPost, Route("playerDetails")]
		public ActionResult PlayerDetails()
		{
			string aid = Require<string>("aid");
			Ban[] bans = _banService.GetBansForUser(aid, true).ToArray();
			Report[] reports = _reportService.GetReportsForPlayer(aid);

			return Ok(new
			{
				Bans = bans,
				Reports = reports
			});
		}

		#region BANS
		[HttpPost, Route(template: "ban/player")]
		public ActionResult Ban()
		{
			string accountId = Require<string>("aid");
			string reason = Require<string>("reason");
			string reportId = Optional<string>("reportId"); // TODO: ReportId not used in player bans
			long? duration = Optional<long?>("durationInSeconds");
			long? expiration = duration == null ? null : DateTimeOffset.Now.AddSeconds((double)duration).ToUnixTimeSeconds();

			// IEnumerable<Room> rooms = _roomService.GetRoomsForUser(accountId);
			Room[] rooms = _roomService.GetSnapshotRooms(accountId);
			Ban ban = new Ban(accountId, reason, expiration, rooms);
			_banService.Create(ban);

			
			foreach (Room r in rooms)
			{
				r.AddMessage(new Message()
				{
					AccountId = accountId,
					Text = $"Player {accountId} was banned by an administrator.",
					Type = Message.TYPE_BAN_ANNOUNCEMENT
				}, allowPreviousMemberPost: true);
				_roomService.Update(r);
			}

			return Ok(ban.ResponseObject);
		}

		[HttpPost, Route(template: "ban/lift")]
		public ActionResult Unban()
		{
			string banId = Require<string>("banId");// ExtractRequiredValue("banId", body).ToObject<string>();

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
		public ActionResult ListBans()
		{
			return Ok(CollectionResponseObject(_banService.List()));
		}
		#endregion BANS
		
		#region ROOMS
		[HttpGet, Route(template: "rooms/list")]
		public ActionResult ListAllRooms()
		{
			return Ok(CollectionResponseObject(_roomService.List()));
		}

		[HttpPost, Route(template: "rooms/removePlayers")]
		public ActionResult RemovePlayers()
		{
			string[] aids = Require<string[]>("aids");// ExtractRequiredValue("aids", body).ToObject<string[]>();

			Room[] rooms = _roomService.GetGlobals().Where(room => room.HasMember(aids)).ToArray();
			foreach (Room room in rooms)
			{
				room.RemoveMembers(aids);
				_roomService.Update(room);
			}
			
			return Ok(new { Message = $"Removed {aids.Length} players from {rooms.Length} unique rooms."});
		}
		#endregion ROOMS
		
		#region MESSAGES
		[HttpPost, Route(template: "messages/delete")]
		public ActionResult DeleteMessage()
		{
			string[] messageIds = Require<string[]>("messageIds");// ExtractRequiredValue("messageIds", body).ToObject<string[]>();
			string roomId = Require<string>("roomId");//ExtractRequiredValue("roomId", body).ToObject<string>();

			Room room = _roomService.Get(roomId);
			room.Messages = room.Messages.Where(m => !messageIds.Contains(m.Id)).ToList();
			_roomService.Update(room);

			return Ok(room.ResponseObject);
		}

		[HttpGet, Route(template: "messages/sticky")]
		public ActionResult StickyList()
		{
			IEnumerable<Message> stickies = _roomService.GetStickyMessages(all: true);

			return Ok(new { Stickies = stickies });
		}

		[HttpPost, Route(template: "messages/sticky")]
		public ActionResult Sticky()
		{
			// Message message = Message.FromJToken(ExtractRequiredValue("message", body), Token.AccountId);
			Message message = Message.FromJToken(Require<JToken>("message"), Token.AccountId);
			message.Type = Message.TYPE_STICKY;
			string language = Optional<string>("language");//ExtractOptionalValue("language", body)?.ToObject<string>();

			Log.Info(Owner.Will, "New sticky message issued.", Token, data: message);
			Room stickies = _roomService.StickyRoom;
			
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

		[HttpPost, Route(template: "messages/unsticky")]
		public ActionResult Unsticky()
		{
			string messageId = Require<string>("messageId");//ExtractRequiredValue("messageId", body).ToObject<string>();

			Room room = _roomService.StickyRoom;
			room.Messages.Remove(room.Messages.First(m => m.Id == messageId));
			_roomService.Update(room);
			
			return Ok(room.ResponseObject);
		}
		#endregion MESSAGES
		
		#region REPORTS
		[HttpPost, Route("reports/delete")]
		public ActionResult DeleteReport()
		{
			string reportId = Require<string>("reportId");//ExtractRequiredValue("reportId", body).ToObject<string>();

			Report report = _reportService.Get(reportId);
			_reportService.Remove(report);
			return Ok(report.ResponseObject);
		}
		
		[HttpPost, Route("reports/ignore")]
		public ActionResult IgnoreReport()
		{
			string reportId = Require<string>("reportId");//ExtractRequiredValue("reportId", body).ToObject<string>();
			
			Report report = _reportService.Get(reportId);
			report.Status = Report.STATUS_BENIGN;
			_reportService.Update(report);
			return Ok(report.ResponseObject);
		}

		
		#endregion REPORTS
		
		#region LOAD BALANCER
		[HttpGet, Route(template: "health"), NoAuth]
		public override ActionResult HealthCheck()
		{
			return Ok(
				_banService.HealthCheckResponseObject,
				_roomService.HealthCheckResponseObject
			);
		}

		[HttpPost, Route("slackHandler"), NoAuth]
		public ActionResult SlackHandler()
		{
			// TODO: Send slack interactions here (e.g. button presses)
			// Can serialize necessary information (e.g. tokens) in the value field when creating buttons
			// And deserialize it to accomplish admin things in a secure way
			return Ok();
		}
		#endregion LOAD BALANCER
	}
}