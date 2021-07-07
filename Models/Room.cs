using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace chat_service.Models
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
		public HashSet<string> MemberIds { get; set; }

		public Room ()
		{
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
