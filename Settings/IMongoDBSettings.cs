namespace Rumble.Platform.ChatService.Settings
{
	public interface IMongoDBSettings
	{
		public string CollectionName { get; set; }
		public string ConnectionString { get; set; }
		public string DatabaseName { get; set;  }
	}
}