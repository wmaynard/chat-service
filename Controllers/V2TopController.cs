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

[ApiController, Route("chat/v2"), RequireAuth]
public class V2TopController : PlatformController
{
#pragma warning disable
	private readonly V2ReportService       _reportService;
	private readonly V2RoomService         _roomService;
	private readonly V2SettingsService     _settingsService;
	private readonly V2InactiveUserService _inactiveUserService;
	private readonly V2RoomDespawnService  _despawnService;
#pragma warning restore

	#region CLIENT
	// Called when an account is logging in to chat.  Returns sticky messages, bans applied, and user settings.
	[HttpPost, Route(template: "launch"), HealthMonitor(weight: 3)]
	public ActionResult Launch()
	{
		long lastRead = Require<long>("lastRead");
		string language = Require<string>(V2Room.FRIENDLY_KEY_LANGUAGE);

		string player = Token.AccountId;
		// PlayerInfo player = PlayerInfo.FromJsonElement(Require(PlayerInfo.FRIENDLY_KEY_SELF), Token);

		IEnumerable<V2Message> stickies = _roomService.GetStickyMessages();

		V2ChatSettings settings = _settingsService.Get(Token.AccountId);
		
		// TODO check with player/token service to see if player is banned from chat

		V2Room global = _roomService.JoinGlobal(player, language);
		IEnumerable<V2Room> rooms = _roomService.GetRoomsForUser(Token.AccountId);
		object updates = V2RoomUpdate.GenerateResponseFrom(rooms, lastRead);
		if (_inactiveUserService == null)
		{
			Log.Error(Owner.Will, "Inactive user service is null; this should be impossible.");
		}
		else
		{
			_inactiveUserService.Track(player);
		}

		return Ok(
		          settings.ResponseObject,
		          V2Message.GenerateStickyResponseFrom(stickies),
		          global.ResponseObject,
		          updates
		         );
	}
	#endregion CLIENT
}