using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RCL.Logging;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Enums;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Controllers;

[EnableCors(PlatformStartup.CORS_SETTINGS_NAME)]
[ApiController, Route(template: "chat/admin"), Produces(contentType: "application/json"), RequireAuth(AuthType.ADMIN_TOKEN)]
public class AdminController : ChatControllerBase
{
#pragma warning disable CS0649
	private readonly BanService _banService;
	private readonly ReportService _reportService;
#pragma warning restore CS0649

	[HttpGet, Route("playerDetails")]
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

	[HttpPost, Route("challenge")]
	public ActionResult IssueChallenge()
	{
		string id = Require<string>("aid");
		string password = Require<string>("password");

		_roomService.DeletePvpChallenge(null, id);
		Room global = _roomService.GetRoomsForUser(id).FirstOrDefault(room => room.Type == Room.TYPE_GLOBAL);

		if (global == null)
			throw new PlatformException("Unable to find global room for user.", code: ErrorCode.MongoRecordNotFound);

		// global.Members.RemoveWhere(info => info.AccountId != id);
		// global.Members.Add(new PlayerInfo
		// {
		// 	Avatar = "human_infantryman",
		// 	Level = 99,
		// 	Power = 5000,
		// 	ScreenName = "foobar",
		// 	InRoomSince = Timestamp.UnixTime,
		// 	Discriminator = 1234,
		// 	AccountId = "deadbeefdeadbeefdeadbeef"
		// });
		// for (int i = 0; i < 5; i++)
		// 	global.AddMessage(new Message
		// 	{
		// 		AccountId = "deadbeefdeadbeefdeadbeef",
		// 		Text = $"Test message {i}",
		// 		Type = Message.TYPE_CHAT
		// 	});

		Message message = new Message
		{
			AccountId = id,
			Text = $"I'm looking for a PvP match!",
			Type = Message.TYPE_CHALLENGE,
			Data = new RumbleJson
			{
				{ "password", password }
			}
		};
		global.AddMessage(message);
		_roomService.Update(global);
		return Ok(new RumbleJson
		{
			{ "message", message }
		});
	}

	[HttpDelete, Route("claimChallenge")]
	public ActionResult DeleteChallenge()
	{
		string password = Optional<string>("password");
		string issuer = Optional<string>("issuer");

		if (password == null && issuer == null)
			throw new PlatformException("Either password or issuer must be supplied.", code: ErrorCode.RequiredFieldMissing);
		
		return Ok(new RumbleJson
		{
			{ "messagesDeleted", _roomService.DeletePvpChallenge(password, issuer) }
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

		return Ok(ban);
	}

	[HttpPost, Route(template: "ban/lift")]
	public ActionResult Unban()
	{
		string accountId = Optional<string>("accountId");
		string banId = Optional<string>("banId");
		// string banId = Require<string>("banId");

		Ban[] bans = banId != null
			? _banService.Find(filter: ban => ban.Id == banId)
			: _banService.Find(filter: ban => ban.AccountId == accountId);

		if (!bans.Any())
			return Ok();

		foreach (Ban ban in bans.Where(b => b != null && !b.IsExpired))
		{
			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(ban.AccountId);
			foreach (Room r in rooms)
			{
				try
				{
					r.AddMessage(new Message()
					{
						AccountId = ban.AccountId,
						Text = $"Ban {ban.Id} was lifted by an administrator.",
						Type = Message.TYPE_UNBAN_ANNOUNCEMENT
					}, allowPreviousMemberPost: true);
					_roomService.Update(r);
				}
				catch (NotInRoomException) { } // Not actually an error; just banned for long enough that user fell out of the room.
			}
		
			_banService.Delete(ban.Id);
		}

		return Ok();
	}

	[HttpGet, Route(template: "ban/list")]
	public ActionResult ListBans() => Ok(CollectionResponseObject(_banService.List()));
	#endregion BANS
	
	#region ROOMS
	[HttpGet, Route(template: "rooms/list")]
	public ActionResult ListAllRooms() => Ok(CollectionResponseObject(_roomService.List()));

	[HttpPost, Route(template: "rooms/removePlayers")]
	public ActionResult RemovePlayers()
	{
		string[] aids = Require<string[]>("aids");

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
		string[] messageIds = Require<string[]>("messageIds");
		string roomId = Require<string>("roomId");

		Room room = _roomService.Get(roomId);
		room.Messages = room.Messages.Where(m => !messageIds.Contains(m.Id)).ToList();
		_roomService.Update(room);

		return Ok(room.ResponseObject);
	}

	[HttpGet, Route(template: "messages/sticky")]
	public ActionResult StickyList() => Ok(new { Stickies = _roomService.GetStickyMessages(all: true) });

	[HttpPost, Route(template: "messages/sticky")]
	public ActionResult Sticky()
	{
		Message message = Message.FromGeneric(Require<RumbleJson>("message"), Token.AccountId);
		// Message message = Message.FromJsonElement(Require("message"), Token.AccountId);
		message.Type = Message.TYPE_STICKY;
		string language = Optional<string>("language");

		Log.Info(Owner.Will, "New sticky message issued.", data: message);
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
		string messageId = Require<string>("messageId");

		Room room = _roomService.StickyRoom;
		room.Messages.Remove(room.Messages.First(m => m.Id == messageId));
		_roomService.Update(room);

		foreach (Room r in _roomService.GetGlobals())
		{
			if (!r.Messages.Any(message => message.Id == messageId))
				continue;
			r.Messages = r.Messages.Where(message => message.Id != messageId).ToList();
			_roomService.Update(r);
		}
		
		return Ok(room.ResponseObject);
	}
	#endregion MESSAGES
	
	#region REPORTS
	[HttpPost, Route("reports/delete")]
	public ActionResult DeleteReport()
	{
		string reportId = Require<string>("reportId");

		Report report = _reportService.Get(reportId);
		_reportService.Delete(report);
		return Ok(report.ResponseObject);
	}
	
	[HttpPost, Route("reports/ignore")]
	public ActionResult IgnoreReport()
	{
		string reportId = Require<string>("reportId");
		
		Report report = _reportService.Get(reportId);
		report.Status = Report.STATUS_BENIGN;
		_reportService.Update(report);
		return Ok(report.ResponseObject);
	}

	[HttpGet, Route("reports/list")]
	public ActionResult ListReports() => Ok(CollectionResponseObject(_reportService.List()));

	
	#endregion REPORTS
	
	// TODO: Send slack interactions here (e.g. button presses)
	// Can serialize necessary information (e.g. tokens) in the value field when creating buttons
	// And deserialize it to accomplish admin things in a secure way
	[HttpPost, Route("slackHandler"), NoAuth]
	public ActionResult SlackHandler() => Ok();
}