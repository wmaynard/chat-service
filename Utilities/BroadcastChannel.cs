using System;

namespace Rumble.Platform.ChatService.Utilities;

[Flags]
public enum BroadcastChannel
{
    None = 0b0000_0000,
    Global = 0b0000_0001,
    Guild = 0b0000_0010,
    All = 0b1111_1111
}