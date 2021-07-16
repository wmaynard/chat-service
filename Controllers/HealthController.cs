using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	public class HealthController : RumbleController
	{
		private IConfiguration _config;
		protected override string TokenAuthEndpoint => _config["player-service-verify"];

		public HealthController(IConfiguration config) => _config = config;

		[HttpGet, Route(template: "health")]
		public ActionResult HealthCheck()
		{
			Log.Write("/health");
			string s = Environment.GetEnvironmentVariable("RUMBLE_GAME") ?? "$RUMBLE_GAME not found.";
			Log.Write("$RUMBLE_GAME: " + s);
			// TODO: Check status of services and controllers
			return Ok(new {Healthy = true});
		}
	}
}