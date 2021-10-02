using Newtonsoft.Json;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Exceptions
{
	public class NotInRoomException : RoomException
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public PlayerInfo Player { get; private set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string AccountId { get; private set; }

		public NotInRoomException(Room room, string accountId) : base(room, $"Account {accountId} is not a member of room {room.Id}")
		{
			AccountId = accountId;
		}

		public NotInRoomException(Room room, PlayerInfo player) : this(room, player.AccountId)
		{
			Player = player;
		}
	}
}