using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using Rumble.Platform.ChatService.Settings;

namespace Rumble.Platform.ChatService.Utilities
{
	public abstract class RumbleMongoService
	{
		protected readonly MongoClient _client;
		protected readonly IMongoDatabase _database;
		protected readonly IMongoCollection<dynamic> _collection;
		
		protected bool IsConnected => _client.Cluster.Description.State == ClusterState.Connected;
		public abstract bool IsHealthy { get; }

		protected RumbleMongoService(IMongoDBSettings settings)
		{
			_client = new MongoClient(settings.ConnectionString);
			_database = _client.GetDatabase(settings.DatabaseName);
		}
	}
}