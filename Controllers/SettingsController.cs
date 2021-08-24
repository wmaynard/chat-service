using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	// TODO: Documentation
	[ApiController, Route("chat/settings")]
	public class SettingsController : RumbleController
	{
		private readonly SettingsService _settingsService;
		
		protected override string TokenAuthEndpoint => _config["player-service-verify"];

		public SettingsController(SettingsService preferences, IConfiguration config) : base(config)
		{
			_settingsService = preferences;
		}

		[HttpPost, Route(template: "mute")]
		public ActionResult Mute([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			PlayerInfo info = PlayerInfo.FromJToken(ExtractRequiredValue("playerInfo", body));
			if (info.AccountId == token.AccountId)
				throw new InvalidPlayerInfoException("You can't mute yourself!");

			ChatSettings prefs = _settingsService.Get(token.AccountId);
			prefs.AddMutedPlayer(info);
			_settingsService.Update(prefs);

			return Ok(prefs.ResponseObject);
		}

		[HttpPost, Route(template: "unmute")]
		public ActionResult Unmute([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth); // TODO: Switch to aid to unmute
			PlayerInfo info = PlayerInfo.FromJToken(ExtractRequiredValue("playerInfo", body));

			ChatSettings prefs = _settingsService.Get(token.AccountId);
			prefs.RemoveMutedPlayer(info);
			_settingsService.Update(prefs);

			return Ok(prefs.ResponseObject);
		}

		[HttpGet]
		public ActionResult Get([FromHeader(Name = AUTH)] string auth)
		{
			TokenInfo token = ValidateToken(auth);

			ChatSettings prefs = _settingsService.Get(token.AccountId);

			return Ok(prefs.ResponseObject);
		}
		[HttpGet, Route("health")]
		public override ActionResult HealthCheck()
		{
			return Ok(_settingsService.HealthCheckResponseObject);
		}
	}
}