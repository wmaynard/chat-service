using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	public abstract class ChatControllerBase : RumbleController
	{
		protected RoomService _roomService;
		protected IConfiguration _config;
		
		protected override string TokenAuthEndpoint => _config["player-service-verify"];

		public ChatControllerBase(IConfiguration config)
		{
			_config = config;
		}
		public ChatControllerBase(RoomService service, IConfiguration config)
		{
			_config = config;
			_roomService = service;
		}

		protected object GetAllUpdates(TokenInfo token, JObject body)
		{
			return GetAllUpdates(token, body, null);
		}

		protected object GetAllUpdates(TokenInfo token, JObject body, Action<IEnumerable<Room>> preUpdateAction)
		{
			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(token.AccountId);
			long timestamp = ExtractRequiredValue(name: "lastRead", body).ToObject<long>();
			preUpdateAction?.Invoke(rooms);
			return RoomUpdate.GenerateResponseFrom(rooms, timestamp);
		}

		protected string CreateExceptionMessage(Exception ex)
		{
			string output = "";
			do
			{
				output += ex.Message + "\n";
			} while ((ex = ex.InnerException) != null);

			return output[..^1]; // Equivalent to .Substring(0, output.Length - 1)?
		}
	}
}