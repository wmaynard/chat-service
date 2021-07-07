namespace Rumble.Platform.ChatService.Settings
{
	public class ChatDBSettings : IChatDBSettings
	{
		public string CollectionName { get; set; }
		public string ConnectionString { get; set; }
		public string DatabaseName { get; set; }
	}
}