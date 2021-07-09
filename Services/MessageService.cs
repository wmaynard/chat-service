using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;

namespace Rumble.Platform.ChatService.Services
{
	public class MessageService
	{
		private readonly IMongoCollection<Message> _collection;

		public MessageService(IChatDBSettings settings)
		{
			MongoClient client = new MongoClient(settings.ConnectionString);
			IMongoDatabase db = client.GetDatabase(settings.DatabaseName);
		}
		
		
	}
}