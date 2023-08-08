using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Services;

public class V2SettingsService : PlatformMongoService<V2ChatSettings>
{
	public V2SettingsService() : base("settings") { }

	public override V2ChatSettings Get(string accountId)
	{
		V2ChatSettings output = _collection.Find(s => s.AccountId == accountId).FirstOrDefault();
		if (output != null)
			return output;
		output = new V2ChatSettings(accountId);
		Create(output);
		return output;
	}

	public void UpdateSettings(V2ChatSettings settings)
	{
		FilterDefinition<V2ChatSettings> filter = Builders<V2ChatSettings>.Filter.Eq(set => set.Id, settings.Id);
		UpdateDefinition<V2ChatSettings> update = Builders<V2ChatSettings>.Update.Set(set => set, settings);

		_collection.FindOneAndUpdate(
		                             filter: filter, 
		                             update: update
		                            );
	}
}