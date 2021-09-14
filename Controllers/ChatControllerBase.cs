using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers
{
	/// <summary>
	/// All client-facing chat endpoints (not Admin) should return all RoomUpdates in their responses.
	/// This base class should make it easier for them to do that.
	/// </summary>
	public abstract class ChatControllerBase : RumbleController
	{
		protected readonly RoomService _roomService;
		
		protected override string TokenAuthEndpoint => RumbleEnvironment.Variable("RUMBLE_TOKEN_VERIFICATION");

		// public ChatControllerBase(IConfiguration config) : base(config){}
		protected ChatControllerBase(RoomService rooms, IConfiguration config) : base(config)
		{
			_roomService = rooms;
		}

		/// <summary>
		/// Gets all RoomUpdates for a user.
		/// </summary>
		/// <param name="token">The token for the user.</param>
		/// <param name="body">The JSON body of the request.  Must have the field "lastRead".</param>
		/// <param name="preUpdateAction">A function used to modify one or more of the user's Rooms before the updates are generated.</param>
		/// <returns>A ResponseObject of rooms.</returns>
		protected object GetAllUpdates(TokenInfo token, JObject body, Action<IEnumerable<Room>> preUpdateAction = null)
		{
			IEnumerable<Room> rooms = _roomService.GetRoomsForUser(token.AccountId);
			long timestamp = ExtractRequiredValue(name: "lastRead", body).ToObject<long>();
			preUpdateAction?.Invoke(rooms);
			return RoomUpdate.GenerateResponseFrom(rooms, timestamp);
		}
	}
}