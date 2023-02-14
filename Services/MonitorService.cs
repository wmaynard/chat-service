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

public class MonitorService : QueueService<MonitorData>
{
    private const string KEY_TIMESTAMP = "lastRead";
    private SlackMessageClient _slack;
    private readonly RoomService _rooms;
    private readonly ApiService _api;


    public MonitorService(RoomService rooms, ApiService api) : base(collection: "monitor", intervalMs: 5_000)
    {
        _rooms = rooms;
        _api = api;
    }

    protected override void OnTasksCompleted(MonitorData[] data) => Log.Local(Owner.Will, "All monitor messages sent.");

    protected override void PrimaryNodeWork()
    {
        string channel = PlatformEnvironment.Optional<string>("monitorChannel");

        if (string.IsNullOrWhiteSpace(channel))
        {
            Log.Warn(Owner.Will, "Chat monitor channel not yet available; this could be a lack of dynamic config values or the service is starting up");
            return;
        }
        _slack ??= new SlackMessageClient(
            channel: channel,
            token: PlatformEnvironment.SlackLogBotToken
        );
        long ts = Get<long>(KEY_TIMESTAMP);

        MonitorData[] data = _rooms.GetMonitorData(ts);

        if (!data.Any())
            return;
        
        foreach (MonitorData d in data)
            CreateTask(d);

        long max = data
            .Where(monitorData => monitorData != null && monitorData.Messages.Any())
            .SelectMany(monitorData => monitorData.Messages)
            .Max(message => message.Timestamp);

        // Set(KEY_TIMESTAMP, max);
    }

    protected override void ProcessTask(MonitorData data)
    {
        string[] accountIds = data.Messages
            .Select(message => message.AccountId)
            .Distinct()
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.CanBeMongoId())
            .ToArray();

        PlayerInfo[] players = Array.Empty<PlayerInfo>();

        _api
            .Request(PlatformEnvironment.Url("/player/v2/lookup"))
            .AddAuthorization(DynamicConfig.Instance?.AdminToken)
            .AddParameter("accountIds", string.Join(',', accountIds))
            .OnSuccess(response => players = response.Require<PlayerInfo[]>("results"))
            .OnFailure(response => Log.Warn(Owner.Will, "Could not get player information for the chat monitor.", data: new
            {
                Response = response
            }))
            .Get();

        SlackMessage toSlack = new SlackMessage
        {
            Channel = null,
            Blocks = new List<SlackBlock>
            {
                new SlackBlock(SlackBlock.BlockType.HEADER, $"{PlatformEnvironment.Deployment}.{data.Room}"),
                new SlackBlock(SlackBlock.BlockType.MARKDOWN, text: $"Portal links: {string.Join(", ", players.Select(player => player.PortalLink))}"),
                new SlackBlock(SlackBlock.BlockType.DIVIDER)
            }
        };

        foreach (Message message in data.Messages)
        {
            string author = players
                .FirstOrDefault(player => player.AccountId == message.AccountId)
                ?.DisplayName
                ?? message.AccountId;

            string time = DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).ToString("HH:mm");

            toSlack.Blocks.Add(new SlackBlock(
                type: SlackBlock.BlockType.MARKDOWN, 
                text: $"```{time} {author.PadLeft(24, ' ')}: {message.Text}```"
            ));
        }

        try
        {
            _slack.Send(toSlack.Compress()).Wait();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public class PlayerInfo : PlatformDataModel
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
}

public class MonitorData : PlatformDataModel
{
    public string Room { get; set; }
    public string SlackColor { get; set; }
    public Message[] Messages { get; set; }
}