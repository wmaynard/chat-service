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

[ApiController, Route(template: "chat/rooms"), Produces(contentType: "application/json"), RequireAuth]
public class RoomController : ChatControllerBase
{
	// TODO: Inconsistency: Either clean these out or convert other controllers to use constants as well.
	public const string KEY_ROOM_ID = "roomId";
#pragma warning disable CS0649
	private readonly InactiveUserService _inactiveUserService;
#pragma warning restore CS0649

	#region GLOBAL
	// Adds or assigns a user to a global room.  Also removes a user from any global rooms they were already in
	// if it's not the same room.
	[HttpPost, Route(template: "global/join")]
	public ActionResult JoinGlobal()
	{
		_inactiveUserService.Track(Token);

		string language = Require<string>(Room.FRIENDLY_KEY_LANGUAGE);
		string roomId = Optional<string>(KEY_ROOM_ID);
		PlayerInfo player = PlayerInfo.FromRequest(Body, Token);
		// PlayerInfo player = Require<PlayerInfo>(PlayerInfo.FRIENDLY_KEY_SELF);
		// PlayerInfo player = PlayerInfo.FromJsonElement(Require(PlayerInfo.FRIENDLY_KEY_SELF), Token);

		Room joined = _roomService.JoinGlobal(player, language, roomId);
		
		object updates = GetAllUpdates();
		return Ok(joined.ResponseObject, updates);
	}

	[HttpPost, Route("global/leave")]
	public ActionResult LeaveGlobal()
	{
		_inactiveUserService.Track(Token);
		
		object updates = GetAllUpdates(preUpdateAction: rooms =>
		{
			foreach (Room room in rooms.Where(r => r.Type == Room.TYPE_GLOBAL))
			{
				room.RemoveMember(Token.AccountId);
				_roomService.Update(room);
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

		string language = Require<string>(Room.FRIENDLY_KEY_LANGUAGE);
		
		object updates = GetAllUpdates();
		IEnumerable<Room> rooms = _roomService.GetGlobals(language); // TODO: Only return IDs with this instead of the entire room objects.
		return Ok(updates, CollectionResponseObject(rooms));
	}
	
	// Intended for use when a user is logging out and needs to exit a room, or leaves a guild chat.
	[HttpPost, Route(template: "leave")]
	public ActionResult Leave()
	{
		_inactiveUserService.Track(Token);
		
		string roomId = Require<string>(KEY_ROOM_ID);

		object updates = GetAllUpdates(preUpdateAction: rooms =>
		{
			Room ciao;
			try { ciao = rooms.First(r => r.Id == roomId); }
			catch (InvalidOperationException) { throw new RoomNotFoundException(roomId); }
			
			ciao.RemoveMember(Token.AccountId);
			_roomService.Update(ciao);
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

	[HttpPost, Route("update")]
	public ActionResult UpdatePlayerInfo()
	{
		_inactiveUserService.Track(Token);

		PlayerInfo info = PlayerInfo.FromRequest(Body, Token);
		// PlayerInfo info = PlayerInfo.FromJsonElement(Require("playerInfo"), Token);

		IEnumerable<Room> rooms = _roomService.GetPastAndPresentRoomsForUser(Token.AccountId);
		foreach (Room room in rooms)
		{
			room.UpdateMember(info);
			_roomService.Update(room); // TODO: Only update user details, not whole room
		}

		return Ok(GetAllUpdates());
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