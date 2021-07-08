using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	[BsonIgnoreExtraElements]
	public class Room
	{
		public const string TYPE_GLOBAL = "global";
		public const string TYPE_DIRECT_MESSAGE = "dm";
		public const string TYPE_GUILD = "guild";
		public const string TYPE_UNKNOWN = "unknown";

		public const int MESSAGE_CAPACITY = 200;
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		[BsonElement("capacity")]
		public int Capacity { get; set; }
		[BsonElement("created")]
		public long CreatedTimestamp { get; set; }
		[BsonElement("guildId")]
		public string GuildId { get; set; }
		[BsonElement("language")]
		public string Language { get; set; }
		[BsonElement("messages")]
		public List<Message> Messages { get; set; }
		[BsonElement("members")]
		public HashSet<PlayerInfo> Members { get; set; }

		public Room ()
		{
			CreatedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
			Messages = new List<Message>();
			Members = new HashSet<PlayerInfo>();
		}

		[BsonElement("type")]
		public string Type { get; set; }

		public bool AddMember(PlayerInfo playerInfo)
		{
			if (Members.Any(m => m.AccountId == playerInfo.AccountId))
				throw new AlreadyInRoomException();
			if (Members.Count >= Capacity)
				throw new RoomFullException();
			return Members.Add(playerInfo);
		}

		public IEnumerable<Message> AddMessage(Message msg, long lastReadTimestamp)
		{
			if (!Members.Any(m => m.AccountId == msg.AccountId))
				throw new NotInRoomException();
			Messages.Add(msg);
			Messages = Messages.OrderBy(m => m.Timestamp).ToList();
			if (Messages.Count > MESSAGE_CAPACITY)
				Messages.RemoveRange(0, MESSAGE_CAPACITY - Messages.Count);
			return Messages.Where(m => m.Timestamp > lastReadTimestamp);
		}

		public void RemoveMember(string accountId)
		{
			if (!Members.Any(m => m.AccountId == accountId))
				throw new NotInRoomException();
			Members = Members.Where(m => m.AccountId != accountId).ToHashSet();
		}

		[BsonIgnore]
		public bool IsFull => Members.Count >= Capacity;
	}
}