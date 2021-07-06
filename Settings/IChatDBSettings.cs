namespace chat_service.Settings
{
	public interface IChatDBSettings
	{
		string CollectionName { get; set; }
		string ConnectionString { get; set; }
		string DatabaseName { get; set; }
	}
}