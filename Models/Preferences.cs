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