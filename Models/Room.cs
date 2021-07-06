using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace chat_service.Models
{
	public class Room
	{
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }

		[BsonElement("someProperty")]
		public string SomeProperty { get; set; }
	}
}

/*

ROOM:
{
	_id: deadbeefdeadbeefdeadbeef
	language: en-US
	type: global | dm | guild
	guildId: deadbeefdeadbeefdeadbeef
	capacity: 50
	messages: []
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
