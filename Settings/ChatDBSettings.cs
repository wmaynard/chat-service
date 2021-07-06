namespace chat_service.Settings
{
	public class ChatDBSettings : IChatDBSettings
	{
		public string CollectionName { get; set; }
		public string ConnectionString { get; set; }
		public string DatabaseName { get; set; }
	}
}