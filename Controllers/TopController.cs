using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers;

[ApiController, Route("chat"), RequireAuth]
public class TopController : PlatformController
{
#pragma warning disable
	private readonly BanService _banService;
	private readonly ReportService _reportService;
	private readonly RoomService _roomService;
	private readonly SettingsService _settingsService;
	private readonly InactiveUserService _inactiveUserService;
	private readonly RoomDespawnService _despawnService;
#pragma warning restore

	#region CLIENT
	// Called when an account is logging in to chat.  Returns sticky messages, bans applied, and user settings.
	[HttpPost, Route(template: "launch"), HealthMonitor(weight: 3)]
	public ActionResult Launch()
	{
		long lastRead = Require<long>("lastRead");
		string language = Require<string>(Room.FRIENDLY_KEY_LANGUAGE);
		
		PlayerInfo player = PlayerInfo.FromRequest(Body, Token);
		// PlayerInfo player = PlayerInfo.FromJsonElement(Require(PlayerInfo.FRIENDLY_KEY_SELF), Token);

		IEnumerable<Message> stickies = _roomService.GetStickyMessages();
		Ban[] bans = _banService.GetBansForUser(Token.AccountId).ToArray();

		foreach (Ban b in bans)
			b.PurgeSnapshot();
		
		ChatSettings settings = _settingsService.Get(Token.AccountId);

		Room global = _roomService.JoinGlobal(player, language);
		IEnumerable<Room> rooms = _roomService.GetRoomsForUser(Token.AccountId);
		object updates = RoomUpdate.GenerateResponseFrom(rooms, lastRead);
		if (_inactiveUserService == null)
			Log.Error(Owner.Will, "Inactive user service is null; this should be impossible.");
		_inactiveUserService.Track(player);

		return Ok(
			new { Bans = bans },
			settings.ResponseObject,
			Message.GenerateStickyResponseFrom(stickies),
			global.ResponseObject,
			updates
		);
	}
	#endregion CLIENT
}