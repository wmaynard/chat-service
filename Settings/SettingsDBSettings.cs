using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Settings
{
	public class SettingsDBSettings : IMongoDBSettings
	{
		public string CollectionName { get; set; }
		public string ConnectionString { get; set; }
		public string DatabaseName { get; set; }
	}
}