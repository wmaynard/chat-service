using System;
using System.Collections.Generic;
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
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		[BsonElement("capacity")]
		public int Capacity { get; set; }
		[BsonElement("guildId")]
		public string GuildId { get; set; }
		[BsonElement("language")]
		public string Language { get; set; }
		[BsonElement("messages")]
		public Message[] Messages { get; set; }
		[BsonElement("memberIds")]
		public HashSet<string> MemberIds { get; private set; }

		public Room ()
		{
			Messages = Array.Empty<Message>();
			MemberIds = new HashSet<string>();
		}

		[BsonElement("type")]
		public string Type { get; set; }

		private static string ValidateType(string s)
		{
			switch (s)
			{
				case TYPE_GLOBAL:
				case TYPE_DIRECT_MESSAGE:
				case TYPE_GUILD:
					return s;
				default:
					return TYPE_UNKNOWN;
			}
		}

		public bool AddMember(string accountId)
		{
			if (MemberIds.Contains(accountId))
				throw new AlreadyInRoomException();
			if (MemberIds.Count >= Capacity)
				throw new RoomFullException();
			return MemberIds.Add(accountId);
		}

		public bool RemoveMember(string accountId)
		{
			if (!MemberIds.Remove(accountId))
				throw new NotInRoomException();
			return true;
		}

		[BsonIgnore]
		public bool IsFull => MemberIds.Count >= Capacity;
	}
}

/*

ROOM:
{
	_id: deadbeefdeadbeefdeadbeef
	capacity: 50
	guildId: deadbeefdeadbeefdeadbeef
	language: en-US
	messages: []
	memberIds: []
	type: global | dm | guild
}

MESSAGE:
{
	_id: deadbeefdeadbeefdeadbeef
	aid: deadbeefdeadbeefdeadbeef
	isSticky: true | false
	text: Hello, World!
	timestamp: 1622074219
	type: activity | chat | announcement
	formatting: none | urgent | server
*/
