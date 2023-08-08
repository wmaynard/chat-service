using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Services;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Extensions;

namespace Rumble.Platform.ChatService.Controllers;

[ApiController, Route(template: "chat/v2/rooms"), Produces(contentType: "application/json"), RequireAuth]
public class V2RoomController : V2ChatControllerBase
{
#pragma warning disable CS0649
	private readonly V2InactiveUserService _inactiveUserService;
#pragma warning restore CS0649

	#region GLOBAL
	// Adds or assigns a user to a global room.  Also removes a user from any global rooms they were already in
	// if it's not the same room.
	[HttpPost, Route(template: "global/join")]
	public ActionResult JoinGlobal()
	{
		_inactiveUserService.Track(Token);
		
		// TODO check with player/token service to see if player is banned from chat

		string language = Require<string>(V2Room.FRIENDLY_KEY_LANGUAGE);
		string roomId = Optional<string>("roomId");
		string player = Token.AccountId;
		// PlayerInfo player = Require<PlayerInfo>(PlayerInfo.FRIENDLY_KEY_SELF);
		// PlayerInfo player = PlayerInfo.FromJsonElement(Require(PlayerInfo.FRIENDLY_KEY_SELF), Token);

		V2Room joined = _roomService.JoinGlobal(player, language, roomId);
		
		object updates = GetAllUpdates();
		return Ok(joined.ResponseObject, updates);
	}

	[HttpPost, Route("global/leave")]
	public ActionResult LeaveGlobal()
	{
		_inactiveUserService.Track(Token);
		
		object updates = GetAllUpdates(preUpdateAction: rooms =>
		{
			foreach (V2Room room in rooms.Where(r => r.Type == V2Room.V2RoomType.Global))
			{
				room.RemoveMember(Token.AccountId);
				_roomService.UpdateRoom(room);
			}
		});
		return Ok(updates);
	}
	#endregion GLOBAL
	
	#region GENERAL
	// Returns a list of available global rooms for the player, as dictated by their language setting.
	[HttpPost, Route(template: "available")]
	public ActionResult Available()
	{
		_inactiveUserService.Track(Token);

		string language = Require<string>(V2Room.FRIENDLY_KEY_LANGUAGE);
		
		object updates = GetAllUpdates();
		IEnumerable<V2Room> rooms = _roomService.GetGlobals(language);
		List<string> roomIds = new List<string>();
		foreach (V2Room room in rooms)
		{
			roomIds.Add(room.Id);
		}
		return Ok(updates, CollectionResponseObject(roomIds));
	}
	
	// Intended for use when a user is logging out and needs to exit a room, or leaves a guild chat.
	[HttpPost, Route(template: "leave")]
	public ActionResult Leave()
	{
		_inactiveUserService.Track(Token);
		
		string roomId = Require<string>("roomId");

		object updates = GetAllUpdates(preUpdateAction: rooms =>
		{
			V2Room ciao;
			try { ciao = rooms.First(r => r.Id == roomId); }
			catch (InvalidOperationException) { throw new V2RoomNotFoundException(roomId); }
			
			ciao.RemoveMember(Token.AccountId);
			_roomService.UpdateRoom(ciao);
		});
		return Ok(updates);
	}
	
	// Returns a user's rooms.
	[HttpPost, Route(template: "list")]
	public ActionResult List()
	{
		_inactiveUserService.Track(Token);
		
		object roomResponse = null;
		// Since the RoomUpdates needs to get all of the rooms anyway, grab the room response object from them.
		object updates = GetAllUpdates(preUpdateAction: rooms =>
		{
			roomResponse = CollectionResponseObject(rooms);
		});
		
		return Ok(roomResponse, updates);
	}
	#endregion GENERAL

	[HttpGet, Route("guild")]
	public ActionResult GetGuildChat()
	{
		string guildId = Require<string>("guildId");

		if (!guildId.CanBeMongoId())
			throw new PlatformException("Invalid Guild ID.");
		
		return Ok(_roomService.GetGuildChat(guildId));
	}
}