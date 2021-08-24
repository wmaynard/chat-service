using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "chat/messages"), Produces(contentType: "application/json")]
	public class MessageController : ChatControllerBase
	{
		// TODO: insert into mongo doc (as opposed to update, which could overwrite other messages)
		private readonly ReportService _reportService;
		private readonly BanService _banService;
		
		public MessageController(ReportService reports, RoomService rooms, BanService bans, IConfiguration config) : base(rooms, config)
		{
			_reportService = reports;
			_banService = bans;
		}
		
		/// <summary>
		/// Attempts to send a message to the user's global chat room.  All submitted information must be sent as JSON in a request body.
		/// </summary>
		[HttpPost, Route(template: "broadcast")]
		public ActionResult Broadcast([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			// Unlike other endpoints, broadcast is called from the game server.
			// As such, this is the only endpoint that needs to grab "aid", as it does not have access
			// to the player's token.  This should be alright since the rooms already have the player info
			// (e.g. screenname / avatar / discriminator).
			// If there are other endpoints that get modified, however, this and others should be split off
			// into their own controllers to keep client-facing endpoints consistent in their behavior.
			TokenInfo token = ValidateToken(auth);
			string aid = ExtractRequiredValue("aid", body).ToObject<string>();
			long lastRead = ExtractRequiredValue("lastRead", body).ToObject<long>();
			Message msg = Message.FromJToken(ExtractRequiredValue("message", body), aid).Validate();
			msg.Type = Message.TYPE_BROADCAST;

			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(aid);
			foreach (Room r in rooms.Where(r => r.Type == Room.TYPE_GLOBAL))
			{
				r.AddMessage(msg);
				_roomService.Update(r); // TODO: Push the message rather than update the room.
			}

			object updates = RoomUpdate.GenerateResponseFrom(rooms, lastRead);

			return Ok(updates);
		}

		[HttpPost, Route(template: "report")]
		public ActionResult Report([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string messageId = ExtractRequiredValue("messageId", body).ToObject<string>();
			string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();

			Room room = _roomService.Get(roomId);
			
			IEnumerable<Message> logs = room.Snapshot(messageId, Models.Report.COUNT_MESSAGES_BEFORE_REPORTED, Models.Report.COUNT_MESSAGES_AFTER_REPORTED);
			IEnumerable<PlayerInfo> players = room.AllMembers
				.Where(p => logs.Select(m => m.AccountId)
					.Contains(p.AccountId));
			PlayerInfo reporter = room.Members.First(p => p.AccountId == token.AccountId);
			
			Report report = new Report()
			{
				Reporter = reporter,
				Log = logs,
				Players = players
			};
			report.Log.First(m => m.Id == messageId).Reported = true;
			
			_reportService.Create(report);

			object slack = null;
			try
			{
				SlackHelper.SendReport(reporter, logs, players);
				slack = new {SlackSuccess = true};
			}
			catch (Exception e)
			{
				slack = new { ErrorMessage = e.Message, StackTrace = e.StackTrace };
			}
			
			return Ok(report.ResponseObject, GetAllUpdates(token, body), slack);
		}
		/// <summary>
		/// Attempts to send a message to a chat room.  All submitted information must be sent as JSON in a request body.
		/// </summary>
		[HttpPost, Route(template: "send")]
		public ActionResult Send([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			string roomId = ExtractRequiredValue("roomId", body).ToObject<string>();
			Message msg = Message.FromJToken(body["message"], token.AccountId).Validate();

			IEnumerable<Ban> bans = _banService.GetBansForUser(token.AccountId)
				.Where(b => !b.IsExpired)
				.OrderByDescending(b => b.ExpirationDate);
			if (bans.Any())
				throw new UserBannedException(bans.First().TimeRemaining);

			object updates = GetAllUpdates(token, body, delegate(IEnumerable<Room> rooms)
			{
				Room room = rooms.First(r => r.Id == roomId);
				room.AddMessage(msg);
				_roomService.Update(room);
			});

			return Ok(updates);
		}
		/// <summary>
		/// Retrieves all unread messages for a user based on a timestamp.  This timestamp should be the most recent
		/// timestamp from any message in the user's chat rooms.
		/// </summary>
		/// <param name="auth">The Authorization header from a request.  Tokens are provided by player-service.</param>
		/// <param name="body">The JSON body.  'lastRead' is a required field.
		/// Expected body example:
		///	{
		///		"lastRead": 1625704809
		///	}
		/// </param>
		/// <returns></returns>
		[HttpPost, Route(template: "unread")]
		public ActionResult Unread([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);

			return Ok(GetAllUpdates(token, body));
		}

		[HttpPost, Route(template: "sticky")]
		public ActionResult StickyList([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			bool all = ExtractOptionalValue("all", body)?.ToObject<bool>() ?? false;

			return Ok(new { Stickies = _roomService.GetStickyMessages(all) }, GetAllUpdates(token, body));
		}
		[HttpGet, Route("health")]
		public override ActionResult HealthCheck()
		{
			return Ok(
				_reportService.HealthCheckResponseObject,
				_roomService.HealthCheckResponseObject
			);
		}
	}
}