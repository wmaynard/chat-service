using System;
using System.Collections.Generic;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Controllers;

// TODO: Either polish this up or get rid of it; it doesn't do a lot.
/// <summary>
/// All client-facing chat endpoints (not Admin) should return all RoomUpdates in their responses.
/// This base class should make it easier for them to do that.
/// </summary>
public abstract class V2ChatControllerBase : PlatformController
{
#pragma warning disable CS0649
	protected readonly V2RoomService _roomService;
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
		IEnumerable<V2Room> rooms = _roomService.GetRoomsForUser(Token.AccountId);
		long timestamp = Require<long>("lastRead");
		preUpdateAction?.Invoke(rooms);
		return V2RoomUpdate.GenerateResponseFrom(rooms, timestamp);
	}
}