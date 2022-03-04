using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Web;

namespace Rumble.Platform.ChatService.Services;

public class BanService : PlatformMongoService<Ban>
{
	public BanService() : base("bans") { }

	public IEnumerable<Ban> GetBansForUser(string accountId, bool includeExpired = false) => _collection
		.Find(b => b.AccountId == accountId)
		.ToList()
		.Where(b => includeExpired || !b.IsExpired);
}