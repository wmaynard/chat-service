using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	[ApiController, Route("settings")]
	public class SettingsController : RumbleController
	{
		private IConfiguration _config;
		private SettingsService _settingsService;
		
		protected override string TokenAuthEndpoint => _config["player-service-verify"];

		public SettingsController(SettingsService preferences, IConfiguration config)
		{
			_config = config;
			_settingsService = preferences;
		}

		[HttpPost, Route(template: "mute")]
		public ActionResult Mute([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
			PlayerInfo info = PlayerInfo.FromJToken(ExtractRequiredValue("playerInfo", body));

			ChatSettings prefs = _settingsService.Get(token.AccountId);
			prefs.AddMutedPlayer(info);
			_settingsService.Update(prefs);

			return Ok(prefs.ResponseObject);
		}

		[HttpPost, Route(template: "unmute")]
		public ActionResult Unmute([FromHeader(Name = AUTH)] string auth, [FromBody] JObject body)
		{
			TokenInfo token = ValidateToken(auth);
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
		
		//TODO: Move to debug
		[HttpPost, Route(template: "nuke")]
		public ActionResult Nuke()
		{
			_settingsService.Nuke();
			return Ok();
		}
	}
}