namespace Rumble.Platform.ChatService.Settings
{
	public class BanDBSettings : IMongoDBSettings
	{
		public string CollectionName { get; set; }
		public string ConnectionString { get; set; }
		public string DatabaseName { get; set; }
	}
}