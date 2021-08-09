using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	public class PlayerInfo
	{
		public const string KEY_ACCOUNT_ID = "aid";
		public const string KEY_AVATAR = "avatar";
		public const string KEY_SCREENNAME = "sn";
		public const string KEY_MEMBER_SINCE = "memberSince";
		public const string KEY_LEVEL = "level";
		public const string KEY_POWER = "power";

		[BsonElement(KEY_ACCOUNT_ID)]
		public string AccountId { get; set; }
		
		[BsonElement(KEY_AVATAR)]
		public string Avatar { get; set; }
		[BsonElement(KEY_MEMBER_SINCE)]
		public long InRoomSince { get; set; }
		
		[BsonElement(KEY_SCREENNAME)]
		public string ScreenName { get; set; }
		[BsonElement(KEY_LEVEL)]
		public int Level { get; set; }
		[BsonElement(KEY_POWER)]
		public int Power { get; set; }

		public static PlayerInfo FromJToken(JToken input, string accountId = null)
		{
			return new PlayerInfo()
			{
				AccountId = accountId ?? input[KEY_ACCOUNT_ID].ToObject<string>(),
				Avatar = input[KEY_AVATAR]?.ToObject<string>(),
				ScreenName = input[KEY_SCREENNAME]?.ToObject<string>(),
				InRoomSince = DateTimeOffset.Now.ToUnixTimeSeconds(),	// TODO: We're using this in places other than Rooms now; only assign when joining a room
				Level = input[KEY_LEVEL]?.ToObject<int>() ?? 0,
				Power = input[KEY_POWER]?.ToObject<int>() ?? 0
			};
		}

		public void Validate()
		{
			if (AccountId == null)
				throw new InvalidPlayerInfoException("AccountId cannot be null.");
			if (ScreenName == null)
				throw new InvalidPlayerInfoException("ScreenName cannot be null.");
		}
	}
}