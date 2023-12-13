using Rumble.Platform.Common.Minq;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class Preferences : PlatformCollectionDocument
{
    public string AccountId { get; set; }
    public RumbleJson Settings { get; set; }
    public long UpdatedOn { get; set; }
}

public class PreferencesService : MinqService<Preferences>
{
    public PreferencesService() : base("preferences") { }

    public Preferences Update(string accountId, RumbleJson settings) => mongo
        .Where(query => query.EqualTo(preferences => preferences.AccountId, accountId))
        .Upsert(update => update
            .Set(preferences => preferences.Settings, settings)
            .Set(preferences => preferences.UpdatedOn, Timestamp.Now)
        );

    public Preferences FromAccountId(string accountId) => mongo
        .Where(query => query.EqualTo(preferences => preferences.AccountId, accountId))
        .Upsert(update => update.Set(preferences => preferences.UpdatedOn, Timestamp.Now));
}