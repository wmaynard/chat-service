using System;
using System.Collections.Generic;
using MongoDB.Driver;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService.Services
{
	public class SettingsService : PlatformMongoService<ChatSettings>
	{
		public SettingsService() : base("settings") { }

		public override ChatSettings Get(string accountId)
		{
			ChatSettings output = _collection.Find(s => s.AccountId == accountId).FirstOrDefault();
			if (output != null)
				return output;
			output = new ChatSettings(accountId);
			Create(output);
			return output;
		}
	}
}