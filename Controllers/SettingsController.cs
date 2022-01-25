using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route("chat/settings"), RequireAuth]
	public class SettingsController : PlatformController
	{
		private readonly InactiveUserService _inactiveUserService;
		private readonly SettingsService _settingsService;

		public SettingsController(InactiveUserService inactive, SettingsService preferences, IConfiguration config) : base(config)
		{
			_inactiveUserService = inactive;
			_settingsService = preferences;
		}
		
		#region CLIENT
		[HttpGet]
		public ActionResult Get()
		{
			_inactiveUserService.Track(Token);

			ChatSettings prefs = _settingsService.Get(Token.AccountId);

			return Ok(prefs.ResponseObject);
		}

		[HttpPost, Route(template: "mute")]
		public ActionResult Mute()
		{
			_inactiveUserService.Track(Token);

			PlayerInfo info = PlayerInfo.FromRequest(Body, Token);
			// PlayerInfo info = PlayerInfo.FromJsonElement(Require("playerInfo"));
			if (info.AccountId == Token.AccountId)
				throw new InvalidPlayerInfoException(info, "AccountId", "You can't mute yourself!");

			ChatSettings prefs = _settingsService.Get(Token.AccountId);
			prefs.AddMutedPlayer(info);
			_settingsService.Update(prefs);

			return Ok(prefs.ResponseObject);
		}

		[HttpPost, Route(template: "unmute")]
		public ActionResult Unmute()
		{
			_inactiveUserService.Track(Token);

			// TODO: Switch to aid to unmute
			PlayerInfo info = PlayerInfo.FromRequest(Body, Token);
			// PlayerInfo info = PlayerInfo.FromJsonElement(Require("playerInfo"));

			ChatSettings prefs = _settingsService.Get(Token.AccountId);
			prefs.RemoveMutedPlayer(info);
			_settingsService.Update(prefs);

			return Ok(prefs.ResponseObject);
		}
		#endregion CLIENT

		#region LOAD BALANCER
		[HttpGet, Route("health"), NoAuth]
		public override ActionResult HealthCheck()
		{
			return Ok(_settingsService.HealthCheckResponseObject);
		}
		#endregion LOAD BALANCER
	}
}