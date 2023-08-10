using System;
using System.Collections;
using System.Collections.Generic;
using RCL.Logging;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;
using Ban = Rumble.Platform.Common.Models.Ban;

namespace Rumble.Platform.ChatService.Controllers;

/// <summary>
/// All client-facing chat endpoints (not Admin) should return all RoomUpdates in their responses.
/// This base class should make it easier for them to do that.
/// </summary>
public abstract class V2ChatControllerBase : PlatformController
{
#pragma warning disable CS0649
	private readonly V2ReportService       _reportService;
	private readonly V2RoomService         _roomService;
	private readonly V2SettingsService     _settingsService;
	private readonly V2InactiveUserService _inactiveUserService;
	private readonly V2RoomDespawnService  _despawnService;
	private readonly DynamicConfig         _config;
#pragma warning restore CS0649

	/// <summary>
	/// Gets all RoomUpdates for a user.
	/// </summary>
	/// <param name="token">The token for the user.</param>
	/// <param name="body">The JSON body of the request.  Must have the field "lastRead".</param>
	/// <param name="preUpdateAction">A function used to modify one or more of the user's Rooms before the updates are generated.</param>
	/// <returns>A ResponseObject of rooms.</returns>
	protected object GetAllUpdates(Action<IEnumerable<V2Room>> preUpdateAction = null)
	{
		long lastRead = Require<long>("lastRead");
		
		string language = Require<string>(V2Room.FRIENDLY_KEY_LANGUAGE);

		string player = Token.AccountId;

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
			if (((IList) ban.Audience).Contains("chat_service"))
			{
				throw new V2UserBannedException(tokenInfo: Token, 
				                                message: null,
				                                ban: ban);
			}
		}

		_roomService.JoinGlobal(player, language);
		IEnumerable<V2Room> rooms = _roomService.GetRoomsForUser(Token.AccountId);
		if (_inactiveUserService == null)
		{
			Log.Error(Owner.Will, "Inactive user service is null; this should be impossible.");
		}
		else
		{
			_inactiveUserService.Track(player);
		}
		
		preUpdateAction?.Invoke(rooms);
		return V2RoomUpdate.GenerateResponseFrom(rooms, lastRead);
	}
}