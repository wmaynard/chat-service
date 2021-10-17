using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route(template: "chat/messages"), Produces(contentType: "application/json"), RequireAuth]
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
		#region SERVER
		/// <summary>
		/// Attempts to send a message to the user's global chat room.  All submitted information must be sent as JSON in a request body.
		/// </summary>
		[HttpPost, Route(template: "broadcast")]
		public ActionResult Broadcast()
		{
			// Unlike other endpoints, broadcast is called from the game server.
			// As such, this is the only endpoint that needs to grab "aid", as it does not have access
			// to the player's token.  This should be alright since the rooms already have the player info
			// (e.g. screenname / avatar / discriminator).
			// If there are other endpoints that get modified, however, this and others should be split off
			// into their own controllers to keep client-facing endpoints consistent in their behavior.
			string aid = Require<string>("aid");//ExtractRequiredValue("aid", body).ToObject<string>();
			long lastRead = Require<long>("lastRead");//ExtractRequiredValue("lastRead", body).ToObject<long>();
			Message msg = Message.FromJToken(Require<JToken>("message"), aid).Validate();//(ExtractRequiredValue("message", body), aid).Validate();
			msg.Type = Message.TYPE_BROADCAST;
			Log.Info(Owner.Will, "New broadcast message", Token, data : new
			{
				AccountId = aid,
				Broadcast = msg
			});

			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(aid);
			foreach (Room r in rooms.Where(r => r.Type == Room.TYPE_GLOBAL))
			{
				r.AddMessage(msg);
				_roomService.Update(r); // TODO: Push the message rather than update the room.
			}
			
			Graphite.Track("broadcasts", 1, type: Graphite.Metrics.Type.FLAT);

			object updates = RoomUpdate.GenerateResponseFrom(rooms, lastRead);
			return Ok(updates);
		}
		#endregion SERVER
		
		#region CLIENT
		[HttpPost, Route(template: "report")]
		public ActionResult Report()
		{
			string messageId = Require<string>("messageId");//ExtractRequiredValue("messageId", body).ToObject<string>();
			string roomId = Require<string>("roomId");//ExtractRequiredValue("roomId", body).ToObject<string>();

			Room room = _roomService.Get(roomId);
			
			IEnumerable<Message> logs = room.Snapshot(messageId, Models.Report.COUNT_MESSAGES_BEFORE_REPORTED, Models.Report.COUNT_MESSAGES_AFTER_REPORTED);
			IEnumerable<PlayerInfo> players = room.AllMembers
				.Where(p => logs.Select(m => m.AccountId)
					.Contains(p.AccountId));
			Message reportedMessage = room.Messages.First(m => m.Id == messageId);
			PlayerInfo reported = room.AllMembers.First(p => p.AccountId == reportedMessage.AccountId);
			PlayerInfo reporter = room.Members.First(p => p.AccountId == Token.AccountId);
			
			Report report = _reportService.FindByPlayerAndMessage(reported.AccountId, reportedMessage.Id)
				?? new Report()
				{
					ReportedPlayer = reported,
					Log = logs,
					MessageId = reportedMessage.Id,
					Players = players
				};
			
			bool added = report.AddReporter(reporter);
			report.Log.First(m => m.Id == messageId).Reported = true;

			if (!added) 
				return Ok(report.ResponseObject, GetAllUpdates(Token, Body));
			
			_reportService.UpdateOrCreate(report);
			Graphite.Track("reports", 1, type: Graphite.Metrics.Type.FLAT);
			
			object slack = _reportService.SendToSlack(report);
			return Ok(report.ResponseObject, GetAllUpdates(Token, Body), slack);
		}
		/// <summary>
		/// Attempts to send a message to a chat room.  All submitted information must be sent as JSON in a request body.
		/// </summary>
		[HttpPost, Route(template: "send")]
		public ActionResult Send()
		{
			string roomId = Require<string>("roomId");//ExtractRequiredValue("roomId", body).ToObject<string>();
			Message msg = Message.FromJToken(Body["message"], Token.AccountId).Validate();

			IEnumerable<Ban> bans = _banService.GetBansForUser(Token.AccountId)
				.Where(b => !b.IsExpired)
				.OrderByDescending(b => b.ExpirationDate);
			if (bans.Any())
				throw new UserBannedException(Token, msg, bans.FirstOrDefault());

			Graphite.Track("messages", 1, type: Graphite.Metrics.Type.FLAT);
			object updates = GetAllUpdates(Token, Body, delegate(IEnumerable<Room> rooms)
			{
				Room room = rooms.FirstOrDefault(r => r.Id == roomId);
				if (room == null)
					throw new RoomNotFoundException(roomId);
				room.AddMessage(msg);
				_roomService.Update(room);
			});

			return Ok(updates);
		}
		
		[HttpPost, Route(template: "unread")]
		public ActionResult Unread()
		{
			return Ok(GetAllUpdates(Token, Body));
		}
		
		[HttpPost, Route(template: "sticky")]
		public ActionResult StickyList()
		{
			// TODO: Is this ever used?  Or is it time to retire it now that we insert stickies into every room?
			bool all = Optional<bool>("all");//ExtractOptionalValue("all", body)?.ToObject<bool>() ?? false;

			return Ok(new { Stickies = _roomService.GetStickyMessages(all) }, GetAllUpdates(Token, Body));
		}
		#endregion CLIENT
		
		#region LOAD BALANCER
		[HttpGet, Route("health"), NoAuth]
		public override ActionResult HealthCheck()
		{
			return Ok(
				_reportService.HealthCheckResponseObject,
				_roomService.HealthCheckResponseObject
			);
		}
		#endregion LOAD BALANCER
	}
}