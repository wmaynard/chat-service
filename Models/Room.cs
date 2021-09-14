using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService.Models
{
	[BsonIgnoreExtraElements]
	public class Room : RumbleModel
	{
		internal const string DB_KEY_CAPACITY = "cap";
		internal const string DB_KEY_CREATED_TIMESTAMP = "ts";
		internal const string DB_KEY_GUILD_ID = "gid";
		internal const string DB_KEY_LANGUAGE = "lang";
		internal const string DB_KEY_MESSAGES = "msg";
		internal const string DB_KEY_MEMBERS = "who";
		internal const string DB_KEY_PREVIOUS_MEMBERS = "pwho";
		internal const string DB_KEY_TYPE = "t";

		public const string FRIENDLY_KEY_ID = "id";
		public const string FRIENDLY_KEY_CAPACITY = "capacity";
		public const string FRIENDLY_KEY_CREATED_TIMESTAMP = "created";
		public const string FRIENDLY_KEY_GUILD_ID = "guildId";
		public const string FRIENDLY_KEY_HAS_STICKY = "hasSticky";
		public const string FRIENDLY_KEY_LANGUAGE = "language";
		public const string FRIENDLY_KEY_MESSAGES = "messages";
		public const string FRIENDLY_KEY_MEMBERS = "members";
		public const string FRIENDLY_KEY_PREVIOUS_MEMBERS = "previousMembers";
		public const string FRIENDLY_KEY_TYPE = "type";
	
		public const string TYPE_GLOBAL = "global";
		public const string TYPE_DIRECT_MESSAGE = "dm";
		public const string TYPE_GUILD = "guild";
		public const string TYPE_UNKNOWN = "unknown";
		public const string TYPE_STICKY = "sticky";

		public const int MESSAGE_CAPACITY = 200;
		// public const int GLOBAL_PLAYER_CAPACITY = 1000;
		public static readonly int GLOBAL_PLAYER_CAPACITY = int.Parse(RumbleEnvironment.Variable("GLOBAL_PLAYER_CAPACITY"));

		public static event EventHandler<RoomEventArgs> OnMessageAdded;
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }

		private int _memberCapacity;
		[BsonElement(DB_KEY_CAPACITY)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_CAPACITY)]
		public int MemberCapacity
		{
			get => Type == TYPE_GLOBAL 
				? GLOBAL_PLAYER_CAPACITY 
				: _memberCapacity;
			set => _memberCapacity = value;
		}
		[BsonElement(DB_KEY_CREATED_TIMESTAMP)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_CREATED_TIMESTAMP)]
		public long CreatedTimestamp { get; set; }
		[BsonElement(DB_KEY_GUILD_ID), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_GUILD_ID, NullValueHandling = NullValueHandling.Ignore)]
		public string GuildId { get; set; }
		[BsonIgnore]
		[JsonIgnore]
		public bool IsFull => Members.Count >= MemberCapacity;
		[BsonElement(DB_KEY_LANGUAGE), BsonIgnoreIfNull]
		[JsonProperty(PropertyName = FRIENDLY_KEY_LANGUAGE, NullValueHandling = NullValueHandling.Ignore)]
		public string Language { get; set; }
		[BsonElement(DB_KEY_MESSAGES)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_MESSAGES)]
		public List<Message> Messages { get; set; }
		[BsonElement(DB_KEY_MEMBERS)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_MEMBERS)]
		public HashSet<PlayerInfo> Members { get; set; }
		[BsonElement(DB_KEY_PREVIOUS_MEMBERS)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_PREVIOUS_MEMBERS, DefaultValueHandling = DefaultValueHandling.Ignore)]
		public HashSet<PlayerInfo> PreviousMembers { get; set; }
		[BsonElement(DB_KEY_TYPE)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_TYPE)]
		public string Type { get; set; }
		[BsonIgnore]
		[JsonIgnore]
		public HashSet<PlayerInfo> AllMembers => Members.Union(PreviousMembers).ToHashSet();
		[BsonIgnore]
		[JsonProperty(PropertyName = FRIENDLY_KEY_HAS_STICKY, DefaultValueHandling = DefaultValueHandling.Ignore)]
		public bool HasSticky => Messages.Any(message => message.IsSticky);
		[BsonIgnore]
		[JsonIgnore]
		public string SlackColor => Id[^6..];
		[BsonIgnore]
		[JsonIgnore]
		public bool IsStickyRoom => Type == TYPE_STICKY;
		public Room ()
		{
			CreatedTimestamp = UnixTime;
			Messages = new List<Message>();
			Members = new HashSet<PlayerInfo>();
			PreviousMembers = new HashSet<PlayerInfo>();
		}

		/// <summary>
		/// Adds an account ID to the Room.  If the player already exists in the room, an AlreadyInRoomException is
		/// thrown.  If the Room is at capacity, a RoomFullException is thrown.
		/// </summary>
		/// <param name="playerInfo">A PlayerInfo object, parsed from a JToken.</param>
		/// <exception cref="AlreadyInRoomException">Indicates the account ID is already in the Room.</exception>
		/// <exception cref="RoomFullException">Indicates the Room is already full, or became full as the account was joining.</exception>
		public void AddMember(PlayerInfo playerInfo)
		{
			playerInfo.Validate();
			if (playerInfo.ScreenName == null)
				throw new InvalidPlayerInfoException("Screenname cannot be null.");
			if (HasMember(playerInfo.AccountId))
				throw new AlreadyInRoomException();
			if (Members.Count >= MemberCapacity)
				throw new RoomFullException();
			PreviousMembers.RemoveWhere(m => m.AccountId == playerInfo.AccountId);
			Members.Add(playerInfo);
		}

		/// <summary>
		/// Adds a Message to the room.  If the author of the message is not a Member of the Room, a NotInRoomException
		/// is thrown.
		/// </summary>
		/// <param name="msg">The Message to add.</param>
		public void AddMessage(Message msg)
		{
			if (!msg.IsSticky)
				RequireMember(msg.AccountId);
			Messages.Add(msg);
			OnMessageAdded?.Invoke(this, new RoomEventArgs(msg));
			Messages = Messages.OrderBy(m => m.Timestamp).ToList();
			if (Messages.Count(m => !m.IsSticky) <= MESSAGE_CAPACITY)
				return;

			Message[] stickies = Messages.Where(m => m.IsSticky).ToArray();
			Messages.RemoveRange(0, Messages.Count - MESSAGE_CAPACITY);
			if (stickies.Any())	// Add the stickies back in
				Messages = Messages.Union(stickies).OrderBy(m => m.Timestamp).ToList();
			PreviousMembers.RemoveWhere(p => !Messages.Any(m => m.AccountId == p.AccountId));
		}

		public int RemoveStickies(bool expiredOnly = false)
		{
			int before = Messages.Count;
			Messages = expiredOnly
				? Messages = Messages.Where(m => !m.IsSticky || !m.IsExpired).ToList()
				: Messages = Messages.Where(m => !m.IsSticky).ToList();
			int after = Messages.Count;
			if (before > after)
				Log.Local(Owner.Will, $"{before - after} stickies deleted from {Language ?? (Type == Room.TYPE_STICKY ? "Stickies" : "")} | {Id}");
			return before - after;
		}
		/// <summary>
		/// Adds a Message to the room.  If the author of the message is not a Member of the Room, a NotInRoomException
		/// is thrown.
		/// </summary>
		/// <param name="msg">The Message to add.</param>
		/// <param name="lastReadTimestamp">The timestamp from the last read message of the client.</param>
		/// <returns>An IEnumerable of Messages.  Deprecated with the use of RoomUpdate.</returns>
		public IEnumerable<Message> AddMessage(Message msg, long lastReadTimestamp)
		{
			AddMessage(msg);
			return MessagesSince(lastReadTimestamp);
		}

		/// <summary>
		/// Removes an account ID from the Room.  If the account is not a Member, a NotInRoomException is thrown.
		/// </summary>
		/// <param name="accountId">The account ID to remove.</param>
		public void RemoveMember(string accountId)
		{
			RequireMember(accountId);
			PlayerInfo ciao = Members.First(m => m.AccountId == accountId);
			Members.Remove(ciao);
			if (Messages.Any(m => m.AccountId == ciao.AccountId))
				PreviousMembers.Add(ciao);
			// Members = Members.Where(m => m.AccountId != accountId).ToHashSet();
		}

		public void RemoveMembers(params string[] accountIds)
		{
			foreach (string aid in accountIds)
				RemoveMember(aid);
		}

		/// <summary>
		/// Wrapper for a LINQ query to look for an accountID in the Room.
		/// </summary>
		/// <param name="accountId">The account ID to look for.</param>
		/// <returns>True if the account is found, otherwise false.</returns>
		public bool HasMember(string accountId)
		{
			return Members.Any(m => m.AccountId == accountId);
		}

		public bool HasMember(IEnumerable<string> accountIds)
		{
			return Members.Any(m => accountIds.Contains(m.AccountId));
		}

		/// <summary>
		/// Checks to make sure an account exists as a Member.  If the account ID isn't found, a NotInRoomException
		/// is thrown.
		/// </summary>
		/// <param name="accountId">The account ID to look for.</param>
		/// <exception cref="NotInRoomException">Indicates that the account does not exist in the given room.</exception>
		private void RequireMember(string accountId)
		{
			if (!HasMember(accountId))
				throw new NotInRoomException();
		}

		/// <summary>
		/// Retrieve all Messages that occur after a provided timestamp.  Used in generating RoomUpdates.
		/// </summary>
		/// <param name="timestamp">A Unix timestamp.</param>
		/// <returns>An IEnumerable of all Messages after the timestamp.</returns>
		public IEnumerable<Message> MessagesSince(long timestamp)
		{
			return Messages.Where(m => m.Timestamp > timestamp);
		}

		public IEnumerable<Message> Snapshot(string messageId, int before, int after)
		{
			List<Message> ordered = Messages.OrderBy(m => m.Timestamp).ToList();
			int position = ordered.IndexOf(ordered.First(m => m.Id == messageId));
			int start = position > before ? position - before : 0;

			return ordered.Skip(start).Take(after + before);
		}

		public bool UpdateMember(PlayerInfo info)
		{
			int removed = Members.RemoveWhere(m => m.AccountId == info.AccountId);
			if (removed > 0)
				return Members.Add(info);
			PreviousMembers.RemoveWhere(m => m.AccountId == info.AccountId);
			return PreviousMembers.Add(info);
		}

		public SlackAttachment ToSlackAttachment(long timestamp)
		{
			List<Message> news = Messages.Where(message => message.Timestamp >= timestamp).ToList();
			if (!news.Any())
				return null;
			string title = Type == Room.TYPE_STICKY ? "Stickies" : Language;
			List<SlackBlock> blocks = new List<SlackBlock>()
			{
				new(SlackBlock.BlockType.HEADER, $"{title} | {Id}")
			};
			
			// Process normal (non-sticky) messages.
			Message[] nonStickies = news.Where(message => !message.IsSticky).ToArray();
			if (nonStickies.Any())
			{
				string aid = nonStickies.First().AccountId;
				DateTime date = nonStickies.First().Date;
				string entries = "";

				void CreateBlock(Message msg)
				{
					blocks.Add(new SlackBlock(text: $"`{date:HH:mm}` *{AllMembers.First(p => p.AccountId == aid).SlackLink}*\n{entries}"));
					aid = msg.AccountId;
					date = msg.Date;
					entries = "";
				}

				foreach (Message msg in nonStickies)
				{
					if (msg.AccountId != aid)
					{
						// Add the built block, then reset the values to the next message author / date.
						// blocks.Add(new (
						// 	$"`{date:HH:mm}` *{AllMembers.First(p => p.AccountId == aid).SlackLink}*\n{entries}"
						// ));
						// aid = msg.AccountId;
						// date = msg.Date;
						// entries = "";
						CreateBlock(msg);
					}
					// Clean the text for Slack and add it to the entries.
					string clean = msg.Text.Replace('\n', ' ').Replace('`', '\'');
					string toAdd = msg.Type switch
					{
						Message.TYPE_CHAT => clean,
						Message.TYPE_BAN_ANNOUNCEMENT => $"> :anger: `{clean}`",
						Message.TYPE_BROADCAST => $"```Broadcast: {clean}```",
						Message.TYPE_UNBAN_ANNOUNCEMENT => $"> :tada: `{clean}`",
						_ => $":question: {clean}"
					} + '\n';
					if (SlackBlock.WouldOverflow(toAdd))
						Log.Error(Owner.Will, "Impossible to create a SlackBlock.  Text is too long.", data: new { ToAdd = toAdd });
					else if (SlackBlock.WouldOverflow(entries + toAdd))
						CreateBlock(msg);
					entries += toAdd;
				}
				blocks.Add(new (
					text: $"`{date:HH:mm}` *{AllMembers.First(p => p.AccountId == aid).SlackLink}*\n{entries}"
				));
			}
			
			// Process the sticky messages
			blocks.AddRange((IsStickyRoom ? Messages : news)
				.Where(m => m.IsSticky)
				.Select(m => new SlackBlock(
					text: $"`Sticky Message on {m.Date:HH:mm}` > {m.Text}"
			)));

			return new SlackAttachment(SlackColor, blocks);
		}
		
		public class RoomEventArgs : EventArgs
		{
			public Message Message { get; private set; }

			public RoomEventArgs(Message msg)
			{
				Message = msg;
			}
		}
	}
}