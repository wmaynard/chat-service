using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Timers;
using MongoDB.Bson.Serialization.Conventions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RestSharp;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Exceptions;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	// TODO: It's probably more efficient memory-wise to turn this into a service that checks the messages against the lastRead timestamp
	/// <summary>
	/// This class stores a buffer of Messages before dumping them to a Slack channel used by CS to monitor
	/// chat activity.  When either a room reaches a predetermined number of new messages or enough time has passed,
	/// a message is dumped to the monitor channel and the buffer is cleared.
	/// </summary>
	public class SlackLog
	{
		private const int FREQUENCY_IN_MS = 300_000; // 5 minutes
		private const int MAX_QUEUE_SIZE = 50;
		
		private static Dictionary<string, Queue<Message>> Buffer;
		private static Timer Timer;

		public List<SlackBlock> Content { get; private set; }
		public string Color { get; private set; }
		
		private SlackLog(string roomId, Message[] messages)
		{
			// Since Room IDs are Mongo Object IDs, they're already in hex.  Use the last six hex digits to make our
			// color; this will help each room be semi-unique.
			Color = "#" + roomId[^6..];
			Content = new List<SlackBlock>();

			// If there are no messages, return before any Content is created.  This will ensure the room doesn't
			// appear in the message dump.
			if (!messages.Any())
				return;
			
			Content.Add(new SlackBlock(SlackBlock.BlockType.HEADER, roomId));

			PlayerInfo author = messages.First().Author;
			DateTime lastDate = messages.First().Date;
			string entries = "";
			foreach (Message m in messages)
			{
				if (m.Author.AccountId != author.AccountId)
				{
					Content.Add(new (SlackBlock.BlockType.MARKDOWN, $"`{lastDate:HH:mm}` *{SlackHelper.User(author)}*\n{entries}"));
					author = m.Author;
					lastDate = m.Date;
					entries = "";
				}
				string txt = m.Text.Replace('\n', ' ').Replace('`', '\'');
				entries += txt + '\n';
			}
			Content.Add(new (SlackBlock.BlockType.MARKDOWN, $"`{lastDate:HH:mm}` *{SlackHelper.User(author)}*\n{entries}"));
		}

		public static void Initialize()
		{
			Buffer = new Dictionary<string, Queue<Message>>();
			Timer = new Timer(interval: FREQUENCY_IN_MS)
			{
				AutoReset = true
			};
			Timer.Elapsed += (_, _) => { Flush(); };
			Timer.Start();
		}

		/// <summary>
		/// Adds a message to the buffer.
		/// </summary>
		/// <param name="roomId">The language and MongoDB ID for a room.  (ex: "en-US | 605eb.....")</param>
		/// <param name="message">The sent message from a user.</param>
		public static void Add(string roomId, Message message)
		{
			try
			{
				Buffer[roomId].Enqueue(message);
			}
			catch (KeyNotFoundException)
			{
				Buffer.Add(roomId, new Queue<Message>());
				Buffer[roomId].Enqueue(message);
			}
			if (Buffer[roomId].Count > MAX_QUEUE_SIZE)
				Flush();
		}

		/// <summary>
		/// Creates and sends a message to Slack, then empties the Buffer.
		/// </summary>
		private static void Flush()
		{
			Timer.Stop();
			SlackLog[] logs = Buffer
				.OrderBy(kvp => kvp.Key)
				.Select(kvp => new SlackLog(kvp.Key, kvp.Value.OrderBy(m => m.Timestamp).ToArray()))
				.ToArray();
			if (logs.Any())
			{
				new SlackLogReport(logs).Send();
				Buffer = new Dictionary<string, Queue<Message>>();
			}
			Timer.Start();
		}
	}
}