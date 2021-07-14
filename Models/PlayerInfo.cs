using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json.Linq;

namespace Rumble.Platform.ChatService.Models
{
	public class PlayerInfo
	{
		public const string KEY_ACCOUNT_ID = "aid";
		public const string KEY_AVATAR = "avatar";
		public const string KEY_SCREENNAME = "sn";
		public const string KEY_MEMBER_SINCE = "memberSince";

		[BsonElement(KEY_ACCOUNT_ID)]
		public string AccountId { get; set; }
		
		[BsonElement(KEY_AVATAR)]
		public string Avatar { get; set; }
		[BsonElement(KEY_MEMBER_SINCE)]
		public long InRoomSince { get; set; }
		
		[BsonElement(KEY_SCREENNAME)]
		public string ScreenName { get; set; }

		public static PlayerInfo FromJToken(JToken input, string accountId)
		{
			return new PlayerInfo()
			{
				AccountId = accountId,
				Avatar = input[KEY_AVATAR]?.ToObject<string>(),
				ScreenName = input[KEY_SCREENNAME]?.ToObject<string>(),
				InRoomSince = DateTimeOffset.Now.ToUnixTimeSeconds()
			};
		}
	}
}