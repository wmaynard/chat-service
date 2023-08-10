using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RCL.Logging;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Enums;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;
using Ban = Rumble.Platform.Common.Models.Ban;

namespace Rumble.Platform.ChatService.Controllers;

[EnableCors(PlatformStartup.CORS_SETTINGS_NAME)]
[ApiController, Route(template: "chat/v2/admin"), Produces(contentType: "application/json"), RequireAuth(AuthType.ADMIN_TOKEN)]
public class V2AdminController : V2ChatControllerBase
{
#pragma warning disable CS0649
	private readonly V2ReportService _reportService;
	private readonly V2RoomService   _roomService;
	private readonly ApiService      _apiService;
	private readonly DynamicConfig   _config;
#pragma warning restore CS0649

	[HttpGet, Route("playerDetails")]
	public ActionResult PlayerDetails()
	{
		string aid = Require<string>("aid");
		
		string adminToken = _config.AdminToken;

		_apiService
			.Request(url: $"/token/admin/status?accountId={aid}")
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
				                                message: null,
				                                ban: ban);
			}
		}
		
		V2Report[] reports = _reportService.GetReportsForPlayer(aid);

		return Ok(new
		{
			Reports = reports
		});
	}

	[HttpPost, Route("challenge")]
	public ActionResult IssueChallenge()
	{
		string id = Require<string>("aid");
		string password = Require<string>("password");

		_roomService.DeletePvpChallenge(null, id);
		V2Room global = _roomService.GetRoomsForUser(id).FirstOrDefault(room => room.Type == V2Room.V2RoomType.Global);

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

		V2Message message = new V2Message
		                    {
			                    AccountId = id,
			                    Text = $"I'm looking for a PvP match!",
			                    Type = V2Message.V2MessageType.PvpChallenge,
			                    Data = new RumbleJson
			                           {
				                           { "password", password }
			                           }
		                    };
		global.AddMessage(message);
		_roomService.UpdateRoom(global);
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

#region ROOMS
	[HttpGet, Route(template: "rooms/list")]
	public ActionResult ListAllRooms() => Ok(CollectionResponseObject(_roomService.List()));

	[HttpPatch, Route(template: "rooms/removePlayers")]
	public ActionResult RemovePlayers()
	{
		string[] aids = Require<string[]>("aids");

		V2Room[] rooms = _roomService.GetGlobals().Where(room => room.HasMember(aids)).ToArray();
		foreach (V2Room room in rooms)
		{
			room.RemoveMembers(aids);
			_roomService.UpdateRoom(room);
		}
		
		return Ok(new { Message = $"Removed {aids.Length} players from {rooms.Length} unique rooms."});
	}
	#endregion ROOMS
	
	#region MESSAGES
	[HttpDelete, Route(template: "messages/delete")]
	public ActionResult DeleteMessage()
	{
		string[] messageIds = Require<string[]>("messageIds");
		string roomId = Require<string>("roomId");

		V2Room room = _roomService.Get(roomId);
		room.Messages = room.Messages.Where(m => !messageIds.Contains(m.Id)).ToList();
		_roomService.UpdateRoom(room);

		return Ok(room.ResponseObject);
	}

	[HttpGet, Route(template: "messages/sticky")]
	public ActionResult StickyList() => Ok(new { Stickies = _roomService.GetStickyMessages(all: true) });

	[HttpPost, Route(template: "messages/sticky")]
	public ActionResult Sticky()
	{
		V2Message message = V2Message.FromGeneric(Require<RumbleJson>("message"), Token.AccountId);
		// Message message = Message.FromJsonElement(Require("message"), Token.AccountId);
		message.Type = V2Message.V2MessageType.Sticky;
		string language = Optional<string>("language");

		Log.Info(Owner.Will, "New sticky message issued.", data: message);
		V2Room stickies = _roomService.StickyRoom;
		
		if (string.IsNullOrEmpty(message.Text))
		{
			_roomService.DeleteStickies();
			return Ok();
		}
		stickies.AddMessage(message);
		foreach (V2Room r in _roomService.GetGlobals())
		{
			r.AddMessage(message);
			_roomService.UpdateRoom(r);
		}
		_roomService.UpdateRoom(stickies);
		return Ok(stickies.ResponseObject);
	}

	[HttpPatch, Route(template: "messages/unsticky")]
	public ActionResult Unsticky()
	{
		string messageId = Require<string>("messageId");

		V2Room room = _roomService.StickyRoom;
		room.Messages.Remove(room.Messages.First(m => m.Id == messageId));
		_roomService.UpdateRoom(room);

		foreach (V2Room r in _roomService.GetGlobals())
		{
			if (!r.Messages.Any(message => message.Id == messageId))
				continue;
			r.Messages = r.Messages.Where(message => message.Id != messageId).ToList();
			_roomService.UpdateRoom(r);
		}
		
		return Ok(room.ResponseObject);
	}
	#endregion MESSAGES
	
	#region REPORTS
	[HttpDelete, Route("reports/delete")]
	public ActionResult DeleteReport()
	{
		string reportId = Require<string>("reportId");

		V2Report report = _reportService.Get(reportId);
		_reportService.Delete(report);
		return Ok(report.ResponseObject);
	}
	
	[HttpPatch, Route("reports/ignore")]
	public ActionResult IgnoreReport()
	{
		string reportId = Require<string>("reportId");
		
		V2Report report = _reportService.Get(reportId);
		report.Status = V2Report.V2ReportStatus.Ignored;
		_reportService.UpdateReport(report);
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