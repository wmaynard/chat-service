using System.Collections.Generic;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Settings;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Services
{
	public class ReportService : RumbleMongoService
	{
		private new readonly IMongoCollection<Report> _collection;

		public ReportService(ReportDBSettings settings) : base(settings)
		{
			_collection = _database.GetCollection<Report>(settings.CollectionName);
		}

		public List<Report> List() => _collection.Find(filter: r => true).ToList();
		public void Create(Report report) => _collection.InsertOne(document: report);

		public void Update(Report report) =>
			_collection.ReplaceOne(filter: r => r.Id == report.Id, replacement: report);

		public void Remove(Report report) => _collection.DeleteOne(filter: r => r.Id == report.Id);
	}
}