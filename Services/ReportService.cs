using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;

namespace Rumble.Platform.ChatService.Services
{
	public class ReportService
	{
		private readonly IMongoCollection<Report> _collection;

		public ReportService(ChatDBSettings settings)
		{
			MongoClient client = new MongoClient(settings.ConnectionString);
			IMongoDatabase db = client.GetDatabase(settings.DatabaseName);
			_collection = db.GetCollection<Report>(settings.CollectionName);
		}

		public void Create(Report report) => _collection.InsertOne(document: report);

		public void Update(Report report) =>
			_collection.ReplaceOne(filter: r => r.Id == report.Id, replacement: report);

		public void Remove(Report report) => _collection.DeleteOne(filter: r => r.Id == report.Id);
	}
}