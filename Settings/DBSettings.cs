using Rumble.Platform.Common.Web;
// ReSharper disable ClassNeverInstantiated.Global

namespace Rumble.Platform.ChatService.Settings
{
	// An unfortunate side effect of the abstraction in Startup is that we have seemingly empty settings classes for database settings.
	public class BanDBSettings : MongoDBSettings { }
	public class ReportDBSettings : MongoDBSettings { }
	public class RoomDBSettings : MongoDBSettings { }
	public class SettingsDBSettings : MongoDBSettings { }
}