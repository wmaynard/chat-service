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
		public const string KEY_ID = "id";
		public const string KEY_CAPACITY = "capacity";
		public const string KEY_CREATED_TIMESTAMP = "created";
		public const string KEY_GUILD_ID = "guildId";
		public const string KEY_LANGUAGE = "language";
		public const string KEY_MESSAGES = "messages";
		public const string KEY_MEMBERS = "members";
		public const string KEY_TYPE = "type";
	
		public const string TYPE_GLOBAL = "global";
		public const string TYPE_DIRECT_MESSAGE = "dm";
		public const string TYPE_GUILD = "guild";
		public const string TYPE_UNKNOWN = "unknown";

		public const int MESSAGE_CAPACITY = 200;
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		[BsonElement(KEY_CAPACITY)]
		public int Capacity { get; set; }
		[BsonElement(KEY_CREATED_TIMESTAMP)]
		public long CreatedTimestamp { get; set; }
		[BsonElement(KEY_GUILD_ID)]
		public string GuildId { get; set; }
		[BsonElement(KEY_LANGUAGE)]
		public string Language { get; set; }
		[BsonElement(KEY_MESSAGES)]
		public List<Message> Messages { get; set; }
		[BsonElement(KEY_MEMBERS)]
		public HashSet<PlayerInfo> Members { get; set; }

		public Room ()
		{
			CreatedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
			Messages = new List<Message>();
			Members = new HashSet<PlayerInfo>();
		}

		[BsonElement(KEY_TYPE)]
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