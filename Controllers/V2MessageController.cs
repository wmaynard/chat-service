using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using RCL.Logging;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Data;
using Ban = Rumble.Platform.Common.Models.Ban;

namespace Rumble.Platform.ChatService.Controllers;

[ApiController, Route(template: "chat/v2/messages"), Produces(contentType: "application/json"), RequireAuth]
public class V2MessageController : V2ChatControllerBase
{
#pragma warning disable CS0649
	private readonly V2ReportService       _reportService;
	private readonly V2RoomService         _roomService;
	private readonly V2InactiveUserService _inactiveUserService;
	private readonly DynamicConfig         _config;
#pragma warning restore CS0649
	
	#region SERVER
	/// <summary>
	/// Attempts to send a message to the user's global chat room.  All submitted information must be sent as JSON in a request body.
	/// </summary>
	[HttpPost, Route(template: "broadcast"), RequireAuth(AuthType.ADMIN_TOKEN)]
	public ActionResult Broadcast()
	{
		// Unlike other endpoints, broadcast is called from the game server.
		// As such, this is the only endpoint that needs to grab "aid", as it does not have access
		// to the player's token.  This should be alright since the rooms already have the player info
		// (e.g. screenname / avatar / discriminator).
		// If there are other endpoints that get modified, however, this and others should be split off
		// into their own controllers to keep client-facing endpoints consistent in their behavior.
		string aid = Require<string>("aid");
		long lastRead = Require<long>("lastRead");
		V2Message msg = V2Message.FromGeneric(Require<RumbleJson>("message"), aid).Validate();
		
		msg.Type = V2Message.V2MessageType.Broadcast;
		Log.Verbose(Owner.Will, "New broadcast message", data : new
		{
			AccountId = aid,
			Broadcast = msg
		});

		IEnumerable<V2Room> rooms = _roomService.GetRoomsForUser(aid);
		foreach (V2Room r in rooms.Where(r => r.Type == V2Room.V2RoomType.Global))
		{
			r.AddMessage(msg);
			_roomService.UpdateRoom(r);
		}
		
		Graphite.Track("broadcasts", 1, type: Graphite.Metrics.Type.FLAT);

		object updates = V2RoomUpdate.GenerateResponseFrom(rooms, lastRead);
		_inactiveUserService.Track(aid);
		return Ok(updates);
	}
	#endregion SERVER
	
	#region CLIENT
	[HttpPost, Route(template: "report")]
	public ActionResult Report()
	{
		_inactiveUserService.Track(Token);

		string messageId = Require<string>("messageId");
		string roomId = Require<string>("roomId");

		V2Room room = _roomService.Get(roomId);
		
		IEnumerable<V2Message> logs = room.Snapshot(messageId, Models.V2Report.COUNT_MESSAGES_BEFORE_REPORTED, Models.V2Report.COUNT_MESSAGES_AFTER_REPORTED);
		IEnumerable<string> players = room.Members
		                                        .Where(p => logs.Select(m => m.AccountId)
		                                                        .Contains(p));
		V2Message reportedMessage = room.Messages.First(m => m.Id == messageId);
		string reported = room.Members.First(p => p == reportedMessage.AccountId);
		string reporter = room.Members.First(p => p == Token.AccountId);
		
		V2Report report = _reportService.FindByPlayerAndMessage(reported, reportedMessage.Id)
		                  ?? new V2Report()
		                     {
			                     ReportedPlayer = reported,
			                     Log = logs,
			                     MessageId = reportedMessage.Id,
			                     Players = players
		                     };
		
		bool added = report.AddReporter(reporter);
		V2ReportedMessage v2ReportedMessage = (V2ReportedMessage) report.Log.First(m => m.Id == messageId);

		if (!added) 
			return Ok(report.ResponseObject, GetAllUpdates());
		
		_reportService.UpdateOrCreate(report);
		Graphite.Track("reports", 1, type: Graphite.Metrics.Type.FLAT);
		
		object slack = _reportService.SendToSlack(report);
		return Ok(v2ReportedMessage.ResponseObject, GetAllUpdates(), slack);
	}
	/// <summary>
	/// Attempts to send a message to a chat room.  All submitted information must be sent as JSON in a request body.
	/// </summary>
	[HttpPost, Route(template: "send")]
	public ActionResult Send()
	{
		_inactiveUserService.Track(Token);

		string roomId = Require<string>("roomId");
		V2Message msg = V2Message.FromGeneric(Require<RumbleJson>("message"), Token.AccountId).Validate();

		string adminToken = _config.AdminToken;

		_apiService
			.Request(url: $"/token/admin/status?accountId={Token.AccountId}")
			.AddAuthorization(adminToken)
			.OnFailure(response =>
			           {
				           Log.Error(
				                     owner: Owner.Nathan,
				                     message: "Unable to fetch chat ban status for player.",
				                     data: new
				                           {
					                           Response = response.AsRumbleJson
				                           });
			           })
			.Get(out RumbleJson res, out int code);

		List<Ban> bans = (List<Ban>) res["bans"];

		foreach (Ban ban in bans)
		{
			if (ban.Audience.Contains("chat_service"))
			{
				throw new V2UserBannedException(tokenInfo: Token, 
				                                message: msg,
				                                ban: ban);
			}
		}
		
		Graphite.Track("messages", 1, type: Graphite.Metrics.Type.FLAT);
		object updates = GetAllUpdates(delegate(IEnumerable<V2Room> rooms)
		{
			V2Room room = rooms.FirstOrDefault(r => r.Id == roomId);
			if (room == null)
				throw new V2RoomNotFoundException(roomId);
			room.AddMessage(msg);
			_roomService.UpdateRoom(room);
		});

		return Ok(updates);
	}
	
	[HttpGet, Route(template: "unread")]
	public ActionResult Unread()
	{
		_inactiveUserService.Track(Token);

		return Ok(GetAllUpdates());
	}
	
	[HttpGet, Route(template: "sticky")]
	public ActionResult StickyList()
	{
		_inactiveUserService.Track(Token);

		// TODO: Is this ever used?  Or is it time to retire it now that we insert stickies into every room?
		bool all = Optional<bool>("all");

		return Ok(new { Stickies = _roomService.GetStickyMessages(all) }, GetAllUpdates());
	}
	#endregion CLIENT
}