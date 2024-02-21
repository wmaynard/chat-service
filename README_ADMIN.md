# Admin Chat Features

Admin features have some different behaviors from regular chat.  This document assumes that you've first read through the normal [README](README.md).  Consequently some details, such as response structure, may appear as shorthand since the models are defined in more detail there.

Please note that some of these examples are not set in stone.  Chat V2 grants much more flexibility to client applications.  As a result, some previously-known features of chat will depend on client decisions to implement, such as the previous "sticky" messages.

Being an admin client, there won't be any "unread messages" returned, unlike the normal client.

## Sending Messages

The admin version of sending messages is quite different from the regular endpoint.  The V2 admin send has done away with the previous "Broadcast" functionality in favor of allowing an admin token to send whatever messages it wants.  The elevator pitch for a V1 Broadcast was that a message would be sent out to all rooms that a player was in - now, the admin client is going to have to craft a Message for each room it wants to send to and submit them all together.

### Broadcasts

Let's say that we want to announce that a player summoned a rare hero to both a global room and his guild, among other rooms:

```
POST /admin/broadcast
{
    "messages": [
        {
            "accountId": "deadbeefdeadbeefdeadbeef",    // ADMIN ONLY
            "text": "{accountId} summoned {titan}",
            "context": {
                "type": "broadcast",
                "titan":  {
                    "rarity": 5,
                    "name": "Mongorc"
                }
            },
            "channel": 3                                // BroadcastChannel flags
        },
        ...
    ]
}
```

Messages that specify a `channel` will all be assigned the message type `Administrator`.  This has no bearing on how they behave as messages but is used by some background services to inspect messages.

The endpoint uses the term "broadcast" to adopt and expand on the definition used in V1.  Now, a "broadcast" is any message sent from an admin source.  Put simply, this really is just sending a message the same as any other user - just with the caveat that there are fewer restrictions, such as being able to spoof the sender's Account ID or manually set an expiration.

#### Broadcast Channels

The channel is a flags enum.  If you want to send a broadcast out to multiple audiences, you would pipe it:

```csharp
ChatInterop.Broadcast(message, BroadcastChannel.Global | BroadcastChannel.Guild); // sends 3
```

| Channel | Name   | Definition                                                                        |
|:--------|:-------|:----------------------------------------------------------------------------------|
| 0       | None   | Rooms with this channel cannot receive broadcasts.                                |
| 1       | Global | Rooms that are created as necessary to support player population for public chat. |
| 2       | Guild  | Rooms that are created by the guild-service interop.                              |
| 255     | All    | Matches all rooms above None.                                                     |

There's room for more channels to be added, but there is no need at this time to add more.

### Announcements

V1 had "sticky" messages.  These lived in a special room that were piped out to new and existing Rooms as necessary, and were a pain to maintain and update.  These have been dramatically simplified in V2 as a subtype of broadcast by simply omitting the `roomId`:

```
POST /admin/broadcast
{
    "expiration": 1734170280,                           // ADMIN ONLY - bypasses the normal expiration of all messages in the request
    "messages": [
        {
            "text": "Vote for Boaty McBoatface",
            "context": { ... }
        }
    ]
}
```

When an admin send is received and there is **no `channel` specified**, the message type becomes an `Announcement`.  These special messages are returned exactly once in _every_ response, whether they meet the `lastRead` criteria or not.  They don't belong to any specific Room.

A maximum of 10 announcements can be active at any time.  If an 11th is issued, only the 10 most recent announcements will remain unexpired (the oldest messages are immediately expired).  This is done to guarantee a malicious admin can't abuse the system and prevent message deliveries to players by spamming endless announcements.

<hr />

#### IMPORTANT

Broadcasts and announcements share the same endpoint; it's just "admin sending" now.  This means that if you send 10 messages with room IDs and one message without in the same request, you will be sending 10 broadcasts and 1 announcement at the same time.

The use case of doing so might be rare - but an example of when this might be desirable is when adding a leaderboard season end tie-in to chat.  There might be an announcement that the season ended - along with broadcasts for the top players celebrating their achievement.

<hr />

## Listing Messages

To get a list of messages, there are various parameters you can use, and to allow a better browsing experience, listing messages now supports paging:

```
GET /admin/messages?roomId={room}&accountId={account}&messageId={id}&page={pageNumber}

200 OK
{
    "messages": [ ... ],
    "page": 0,
    "messagesPerPage": 100,
    "remainingMessageCount": 24
}
```

**All of these parameters are optional.**  Furthermore, they're also additive - meaning that they just further refine your request.  If you pass in a `messageId`, for example, you're guaranteed to get either 0 or 1 result, depending on if that message exists.

If you just pass a `roomId`, you'll only see messages from that room - and if you pass in an `accountId` as well, you'll only see messages in that room from that player.

If your request has returned enough results, you'll have a page number you can continue loading records with.

<hr />

#### Making an Admin Chat Monitor

This is the only endpoint that would be needed to display messages from a room.  If you know the `roomId`, it's easy enough to create an interval that refreshes the data.  This would still require player info lookups from player-service - but the messages can be loaded easily enough.

There is a chance, however, that at scale we would need to add a `lastRead` filter on the backend side to further limit the messages Mongo needs to pull - though that would be a good problem to have.

<hr />

## Searching Messages

With the introduction of MINQ, we now have the built-in ability to search documents by providing a single term.  This is a very new and very beta feature, and will likely need iteration, but is as simple as:

```
GET /admin/messages/search?term={your term here}

200 OK
{
    "messages": [ ... ]
}
```

Relevance and sort order will be in rough states until enough messages exist for adequate testing / MINQ has had some iterations, as this is the first time MINQ's default search will be used for a live project.

**While currently untested, it's suspected that this is an expensive operation.  This is intended for manual requests only, not automated frequently from a script.**

## Editing Messages

Admin clients can change any and all aspects of a message with one exception: a message's `Type` is final.  You can't change an Announcement to a Global player message, or change a player authored message into an Administrator's message.  But text content, context, and even the room ID are all fair game - but probably best not to change that last one.

```
PUT /admin/messages/update
{
    "message": { ... } // Must include the `id` field to be successful.
}
```

**Any time you edit a message, your admin token information is logged as the editor.**  This is done so that we can catch misuse of the system.

An HTTP 400 is returned if the edited message would match the record that exists on the database.  This functionality can be used by clients to determine if the update was successful or not.

## Listing Reports

Similar behavior to listing messages; you have multiple parameters, which are optional.  A `reportId` guarantees 0 or 1 result.  `accountId` filters out all reports that don't have an account as either a reporter or a message author in the log.  With no parameters, all reports will be returned.

Reports are sorted by the following fields: status (unacknowledged first), times the offending message was reported (highest first), and finally the time the report was first sent (newest first).

```
GET /admin/reports?reportId={room}&accountId{account}&page={pageNumber}

200 OK
{
    "reports": [ ... ],
    "page": 0,
    "reportsPerPage": 10,
    "remainingReportCount": 24
}
```

TODO: Sample report structure in above code

## Updating Reports

Updating reports works quite differently from other updates.  An administrator can only change the status of a report and nothing more.  Reports are designed to be untouchable in their content, as their purpose is to provide a written-in-stone snapshot of events.

An administrator can mark a report with a status.  That status determines the **retention duration** for that report.  For more information, see the [Data Retention documentation](DATA_RETENTION.md).

```
PATCH /admin/reports/update
{
    "reportId": "deadbeefdeadbeefdeadbeef",
    "status": 50
}
```

As with editing messages, admin tokens are logged as part of this update for internal auditing purposes.

## Listing Rooms

Again, we have a paging request with multiple parameters.  You know the drill by now; they're optional.  When using an `accountId`, this query will list all rooms that player is currently in.

```
GET /admin/rooms?roomId={room}&accountId={account}&page={pageNumber}

200 OK
{
    "rooms": [ ... ],
    "page": 0,
    "roomsPerPage": 10,
    "remainingRoomCount": 24
}
```

## Creating Private Rooms

This is an important feature for other interop activities, such as guilds.  Private rooms can only be created or modified with an admin token; they can't be altered by players.  These are different in DMs in that they're intended to be more or less permanent fixtures and rare to see created.  A list of `accountIds` must be provided and must contain at least one valid ID.

A `data` object can be optionally passed in, but is not required.  It is not used directly by chat service, but will be available to any consuming clients if specified.

Private rooms have a maximum capacity of 50 players.

```
POST /admin/rooms/new
{
    "accountIds": [
        "deadbeefdeadbeefdeadbeef",
        "badf00dbadf00dbadf00dbad"
    ],
    "data": {
        "guildName": "Hitchhiker's Guides",
        "guildLeader": "Joe McFugal"
    }
}

200 OK
{
    "room": { ... }
}
```

The token used to create the room is logged for internal auditing only.

## Maintaining Private Rooms

As private rooms can only be modified by admin clients, any feature using a private room must make requests through a proxy server or just be managed by the server on its own.  For example, guild chat will need to change who's in it based on their membership in that guild.  This is accomplished through the following endpoint:

```
PATCH /admin/rooms/update
{
    "roomId": "deadbeefdeadbeefdeadbeef",
    "accountIds": [
        "badf00dbadf00dbadf00dbad"
        ...
    ],
    "data": {
        "guildName": "Hitchhiker's Guides",
        "guildLeader": "Joe McFugal"
    }
}
```

For a service like Guilds, there should be a task on a timer to send updates at regular intervals to ensure data integrity / fix any out-of-sync issues.  For example, if Chat is down and the guild roster changes, we don't want our data to be too out-of-sync.

**Caution: if the `accountIds` field is an empty array, the room will be deleted.**