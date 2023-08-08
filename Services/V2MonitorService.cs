using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Extensions;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Services;

/// <summary>
/// The QueueService is necessary to avoid unnecessary chat duplication.  When deployed, services have multiple instances running.
/// QueueService guarantees only one of the nodes can process PrimaryNodeWork() at a time.  Refer to the platform-common
/// documentation for more information.
/// </summary>
public class V2MonitorService : QueueService<V2MonitorService.Data>
{
    private const    string             KEY_TIMESTAMP = "lastRead";
    private          SlackMessageClient _slack;
    private readonly V2RoomService      _rooms;
    private readonly ApiService         _api;
    
    public V2MonitorService(V2RoomService rooms, ApiService api) : base(collection: "monitor", intervalMs: 5_000)
    {
        _rooms = rooms;
        _api = api;
        Confiscate();
    }

    protected override void OnTasksCompleted(Data[] data) => Log.Local(Owner.Will, "All monitor messages sent.");

    protected override void PrimaryNodeWork()
    {
        string channel = PlatformEnvironment.Optional<string>("monitorChannel");

        // QueueService can be initialized before DynamicConfig is ready.
        // If we're missing DC values, exit early, and wait for the next cycle.
        if (string.IsNullOrWhiteSpace(channel))
        {
            Log.Warn(Owner.Will, "Chat monitor channel not yet available; this could be a lack of dynamic config values or the service is starting up");
            return;
        }
        _slack ??= new SlackMessageClient(
            channel: channel,
            token: PlatformEnvironment.SlackLogBotToken
        );
        
        // Get all new messages sent after the previous max timestamp.
        long ts = Get<long>(KEY_TIMESTAMP);
        Data[] data = _rooms.GetMonitorData(ts);

        if (!data.Any())
            return;
        
        // We have some messages that have yet to be sent to Slack; create tasks to send them out.
        // NOTE: it's possible we could be rate-limited, and we could see some dropped messages if 5 separate
        // containers are processing tasks at the same time.  In this situation, we could mitigate this by
        // increasing the interval, or by moving the processing to this task in the primary node and adding a hard
        // wait (not ideal).
        foreach (Data d in data)
            CreateTask(d);

        // Grab the max timestamp from the messages.  Store it as our "last read" measure in the queue config.
        long max = data
            .Where(monitorData => monitorData != null && monitorData.ChatMessages.Any())
            .SelectMany(monitorData => monitorData.ChatMessages)
            .Max(message => message.Timestamp);

        Set(KEY_TIMESTAMP, max);
    }

    protected override void ProcessTask(Data taskData)
    {
        // Player Service expects only Mongo-compatible IDs.
        string[] accountIds = taskData.ChatMessages
            .Select(message => message.AccountId)
            .Distinct()
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.CanBeMongoId())
            .ToArray();

        V2PlayerInfo[] players = Array.Empty<V2PlayerInfo>();

        // Attempt to get player details from Player Service
        _api
            .Request(PlatformEnvironment.Url("/player/v2/lookup"))
            .AddAuthorization(DynamicConfig.Instance?.AdminToken)
            .AddParameter("accountIds", string.Join(',', accountIds))
            .OnSuccess(response => players = response.Require<V2PlayerInfo[]>("results"))
            .OnFailure(response => Log.Warn(Owner.Will, "Could not get player information for the chat monitor.", data: new
            {
                Response = response
            }))
            .Get();

        SlackMessage message = new SlackMessage
        {
            Channel = null,
            Blocks = new List<SlackBlock>
            {
                new SlackBlock(SlackBlock.BlockType.HEADER, $"{PlatformEnvironment.Deployment}.{taskData.Room}"),
                new SlackBlock(SlackBlock.BlockType.MARKDOWN, text: $"Portal links: {string.Join(", ", players.Select(player => player.PortalLink))}"),
                new SlackBlock(SlackBlock.BlockType.DIVIDER)
            }
        };

        foreach (V2Message chat in taskData.ChatMessages)
        {
            string author = players
                .FirstOrDefault(player => player.AccountId == chat.AccountId)
                ?.DisplayName
                ?? chat.AccountId;

            string time = DateTimeOffset.FromUnixTimeSeconds(chat.Timestamp).ToString("HH:mm");

            message.Blocks.Add(new SlackBlock(
                type: SlackBlock.BlockType.MARKDOWN, 
                text: $"```{time} {author.PadLeft(20, ' ')}: {chat.Text}```"
            ));
        }
        
        _slack.Send(message.Compress()).Wait();
    }

    public class V2PlayerInfo : PlatformDataModel
    {
        [JsonPropertyName("accountId")]
        public string AccountId { get; set; }
        [JsonPropertyName("screenname")]
        public string Screenname { get; set; }
        [JsonPropertyName("discriminator")]
        public string Discriminator { get; set; }
        [JsonPropertyName("accountAvatar")]
        public string Avatar { get; set; }
        [JsonPropertyName("accountLevel")]
        public int Level { get; set; }
        [JsonIgnore]
        public string DisplayName => $"{Screenname}#{Discriminator.PadLeft(4, '0')}";
        [JsonIgnore] 
        public string PortalLink => $"<{PlatformEnvironment.Url($"/player/{AccountId}").Replace("://", "://portal.")}|{DisplayName}>";
    }
    
    public class Data : PlatformDataModel
    {
        public string      Room         { get; set; }
        public V2Message[] ChatMessages { get; set; }
    }
}