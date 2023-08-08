using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers;

[ApiController, Route("chat/v2/settings"), RequireAuth]
public class V2SettingsController : PlatformController
{
#pragma warning disable CS0649
	private readonly V2InactiveUserService _inactiveUserService;
	private readonly V2SettingsService     _settingsService;
#pragma warning restore CS0649
	
	#region CLIENT
	[HttpGet]
	public ActionResult Get()
	{
		_inactiveUserService.Track(Token);

		V2ChatSettings prefs = _settingsService.Get(Token.AccountId);

		return Ok(prefs.ResponseObject);
	}

	[HttpPost, Route(template: "mute")]
	public ActionResult Mute()
	{
		_inactiveUserService.Track(Token);

		// TODO changed required body key
		string player = Require<string>(key: "accountId");
		if (player == Token.AccountId)
			throw new V2InvalidPlayerInfoException(player, "AccountId", "You can't mute yourself!");

		V2ChatSettings prefs = _settingsService.Get(Token.AccountId);
		prefs.AddMutedPlayer(player);
		_settingsService.UpdateSettings(prefs);

		return Ok(prefs.ResponseObject);
	}

	[HttpPost, Route(template: "unmute")]
	public ActionResult Unmute()
	{
		_inactiveUserService.Track(Token);

		// TODO changed required body key
		string player = Require<string>("accountId");

		V2ChatSettings prefs = _settingsService.Get(Token.AccountId);
		prefs.RemoveMutedPlayer(player);
		_settingsService.UpdateSettings(prefs);

		return Ok(prefs.ResponseObject);
	}
	#endregion CLIENT
}