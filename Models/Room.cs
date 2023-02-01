using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using RCL.Logging;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

[BsonIgnoreExtraElements]
public class Room : PlatformCollectionDocument
{
	internal const string DB_KEY_CAPACITY = "cap";
	internal const string DB_KEY_CREATED_TIMESTAMP = "ts";
	internal const string DB_KEY_GUILD_ID = "gid";
	internal const string DB_KEY_LANGUAGE = "lang";
	internal const string DB_KEY_MEMBERS = "who";
	internal const string DB_KEY_MESSAGES = "msg";
	internal const string DB_KEY_PREVIOUS_MEMBERS = "pwho";
	internal const string DB_KEY_TYPE = "t";

	public const string FRIENDLY_KEY_ID = "id";
	public const string FRIENDLY_KEY_CAPACITY = "capacity";
	public const string FRIENDLY_KEY_CREATED_TIMESTAMP = "created";
	public const string FRIENDLY_KEY_GUILD_ID = "guildId";
	public const string FRIENDLY_KEY_HAS_STICKY = "hasSticky";
	public const string FRIENDLY_KEY_LANGUAGE = "language";
	public const string FRIENDLY_KEY_LANGUAGE_DISCRIMINATOR = "listingId";
	public const string FRIENDLY_KEY_ALL_MEMBERS = "participants";
	public const string FRIENDLY_KEY_MEMBERS = "members";
	public const string FRIENDLY_KEY_MESSAGES = "messages";
	public const string FRIENDLY_KEY_PREVIOUS_MEMBERS = "previousMembers";
	public const string FRIENDLY_KEY_TYPE = "type";
	public const string FRIENDLY_KEY_VACANCIES = "vacancies";

	public const string TYPE_DIRECT_MESSAGE = "dm";
	public const string TYPE_GLOBAL = "global";
	public const string TYPE_GUILD = "guild";
	public const string TYPE_STICKY = "sticky";
	public const string TYPE_UNKNOWN = "unknown";

	public const int MESSAGE_CAPACITY = 200;

	public static readonly string ENVIRONMENT = PlatformEnvironment.Deployment;
	public static int GlobalPlayerCapacity => DynamicConfig.Instance?.Optional<int?>("roomCapacity") ?? 200;

	public static event EventHandler<RoomEventArgs> OnMessageAdded;
	private static Dictionary<string, List<string>> IDMap; // TODO: This could be a separate service if there's a good way to bring a singleton into models
	
	private int _memberCapacity;
	
	#region MONGO
	[BsonElement(DB_KEY_CREATED_TIMESTAMP)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_CREATED_TIMESTAMP)]
	public long CreatedTimestamp { get; set; }
	
	[BsonElement(DB_KEY_GUILD_ID), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_GUILD_ID)]
	public string GuildId { get; set; }
	
	[SimpleIndex]
	[BsonElement(DB_KEY_LANGUAGE), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_LANGUAGE), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string Language { get; set; }
	
	[BsonElement(DB_KEY_CAPACITY)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_CAPACITY)]
	public int MemberCapacity
	{
		get => Type == TYPE_GLOBAL 
			? GlobalPlayerCapacity 
			: _memberCapacity;
		set => _memberCapacity = value;
	}
	
	[BsonElement(DB_KEY_MEMBERS)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_MEMBERS)]
	public HashSet<PlayerInfo> Members { get; set; }
	
	[BsonElement(DB_KEY_MESSAGES)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_MESSAGES)]
	public List<Message> Messages { get; set; }
	
	[BsonElement(DB_KEY_PREVIOUS_MEMBERS)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_PREVIOUS_MEMBERS), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public HashSet<PlayerInfo> PreviousMembers { get; set; }
	
	[BsonElement(DB_KEY_TYPE)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_TYPE)]
	public string Type { get; set; }
	#endregion MONGO
	
	#region CLIENT
	[BsonIgnore]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_LANGUAGE_DISCRIMINATOR), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Discriminator
	{
		get
		{
			if (Type != TYPE_GLOBAL)
				return null;
			IDMap ??= new Dictionary<string, List<string>>();
			if (!IDMap.ContainsKey(Language))
				IDMap.Add(Language, new List<string>());
			int output = IDMap[Language].IndexOf(Id);
			if (output >= 0)
				return output + 1;
			IDMap[Language].Add(Id);
			return IDMap[Language].IndexOf(Id) + 1;
		}
	}

	// TODO: Refactor out Members, AllMembers, and PreviousMembers.  The client should use player-service's lookup to find and cache account information.
	[BsonIgnore]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_ALL_MEMBERS)]
	public string[] Participants => Members.Union(PreviousMembers).Append(PlayerInfo.Admin).Select(info => info.AccountId).Distinct().ToArray();

	[BsonIgnore]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_HAS_STICKY), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool HasSticky => Messages.Any(message => message.IsSticky);
	
	[BsonIgnore]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_VACANCIES)]
	public int Vacancies => MemberCapacity - Members.Count;
	#endregion CLIENT
	
	#region INTERNAL
	[BsonIgnore]
	[JsonIgnore]
	public HashSet<PlayerInfo> AllMembers => Members.Union(PreviousMembers).Append(PlayerInfo.Admin).ToHashSet();
	
	[BsonIgnore]
	[JsonIgnore]
	public bool IsFull => Members.Count >= MemberCapacity;
	
	[BsonIgnore]
	[JsonIgnore]
	public bool IsStickyRoom => Type == TYPE_STICKY;
	
	[BsonIgnore]
	[JsonIgnore]
	public string SlackColor => Id[^6..];
	#endregion INTERNAL
	
	public Room ()
	{
		CreatedTimestamp = Timestamp.UnixTime;
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
		playerInfo.CustomValidate();
		if (playerInfo.ScreenName == null)
			throw new InvalidPlayerInfoException(playerInfo, "ScreenName");
		if (HasMember(playerInfo.AccountId))
			throw new AlreadyInRoomException(this, playerInfo);
		if (Members.Count >= MemberCapacity)
			throw new RoomFullException(this, playerInfo);
		PreviousMembers.RemoveWhere(m => m.AccountId == playerInfo.AccountId);
		Members.Add(playerInfo);
	}

	/// <summary>
	/// Adds a Message to the room.  If the author of the message is not a Member of the Room, a NotInRoomException
	/// is thrown.
	/// </summary>
	/// <param name="msg">The Message to add.</param>
	public void AddMessage(Message msg, bool allowPreviousMemberPost = false)
	{
		if (!msg.IsSticky)
			RequireMember(msg.AccountId, allowPreviousMemberPost);
		Messages.Add(msg);
		OnMessageAdded?.Invoke(this, new RoomEventArgs(msg));
		Messages = Messages.OrderBy(m => m.Timestamp).ToList();
		if (Messages.Count(m => !m.IsSticky) <= MESSAGE_CAPACITY)
			return;

		Message[] stickies = Messages.Where(m => m.IsSticky).ToArray();
		Messages.RemoveRange(0, Messages.Count - MESSAGE_CAPACITY);
		if (stickies.Any())	// Track the stickies back in
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
	public bool HasMember(string accountId) => Members.Any(m => m.AccountId == accountId);
	public bool HasMember(IEnumerable<string> accountIds) => Members.Any(m => accountIds.Contains(m.AccountId));
	public bool HasPreviousMember(string accountId) => PreviousMembers.Any(m => m.AccountId == accountId);

	/// <summary>
	/// Checks to make sure an account exists as a Member.  If the account ID isn't found, a NotInRoomException
	/// is thrown.
	/// </summary>
	/// <param name="accountId">The account ID to look for.</param>
	/// <exception cref="NotInRoomException">Indicates that the account does not exist in the given room.</exception>
	private void RequireMember(string accountId, bool orPrevious = false)
	{
		if (!(HasMember(accountId) || (orPrevious && HasPreviousMember(accountId))))
			throw new NotInRoomException(this, accountId);
	}

	/// <summary>
	/// Retrieve all Messages that occur after a provided timestamp.  Used in generating RoomUpdates.
	/// </summary>
	/// <param name="timestamp">A Unix timestamp.</param>
	/// <returns>An IEnumerable of all Messages after the timestamp.</returns>
	public IEnumerable<Message> MessagesSince(long timestamp) => Messages.Where(m => m.Timestamp > timestamp);

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

	/// <summary>
	/// Converts a Room to a SlackAttachment for the Room Monitor.
	/// </summary>
	/// <param name="timestamp"></param>
	/// <returns>A SlackAttachment, or null if there are no new messages.  Null attachments should be removed during the Compress stage.</returns>
	public SlackAttachment ToSlackAttachment(long timestamp)
	{
		// 2021.09.30: Omit Broadcasts from the chat monitor
		List<Message> news = Messages.Where(message => message.Timestamp >= timestamp && message.Type != Message.TYPE_BROADCAST).ToList();
		if (!news.Any())
			return null;
		string title = Type == Room.TYPE_STICKY ? "Stickies" : $"{Language}-{Discriminator}";
		List<SlackBlock> blocks = new List<SlackBlock>()
		{
			new(SlackBlock.BlockType.HEADER, $"{title} | {ENVIRONMENT}.{Id}")
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
					// Track the built block, then reset the values to the next message author / date.
					// blocks.Track(new (
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

		public RoomEventArgs(Message msg) => Message = msg;
	}
}