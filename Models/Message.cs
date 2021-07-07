using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Rumble.Platform.ChatService.Models
{
	public class Message
	{
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
	}
}