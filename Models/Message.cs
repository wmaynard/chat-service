using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace chat_service.Models
{
	public class Message
	{
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
	}
}