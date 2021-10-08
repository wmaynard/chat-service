using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route("chat/settings"), RequireAuth]
	public class SettingsController : PlatformController
	{
		private readonly SettingsService _settingsService;
		
		// protected override string TokenAuthEndpoint => RumbleEnvironment.Variable("RUMBLE_TOKEN_VERIFICATION");

		public SettingsController(SettingsService preferences, IConfiguration config) : base(config)
		{
			_settingsService = preferences;
		}
		
		#region CLIENT
		[HttpGet]
		public ActionResult Get()
		{
			ChatSettings prefs = _settingsService.Get(Token.AccountId);

			return Ok(prefs.ResponseObject);
		}

		[HttpPost, Route(template: "mute")]
		public ActionResult Mute()
		{
			PlayerInfo info = PlayerInfo.FromJToken(Require<JToken>("playerInfo"));//ExtractRequiredValue("playerInfo", body));
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
			// TODO: Switch to aid to unmute
			PlayerInfo info = PlayerInfo.FromJToken(Require<JToken>("playerInfo"));//ExtractRequiredValue("playerInfo", body));

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