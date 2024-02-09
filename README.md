# Chat V2

 An API for in-game social messaging - now new and improved!

## Introduction

Chat V1 was a particularly interesting Platform project - because it predated platform-common, it had a lot of wild west-style code and a healthy amount of duct tape binding it together.  It was lightweight, responsive, and never complained about its RPS (requests per second) though, so despite the stress we hit it with, it actually performed quite well.

However, it had some significant downfalls.  It was very difficult to maintain, not being built on the tooling that most of Platform relies on now, and adding new features such as guild chat was not going to be clean or easy.

This guide will walk you through how to use the second iteration of chat.  Before we get to the meat of the docs, there are some project-specific terms we should define:

## Glossary

| Term                   | Definition                                                                                                                                                                                                                                                     |
|:-----------------------|:---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Client                 | Any application that hits the service, either directly or indirectly.  Could be the game, Portal, or Postman, as examples.                                                                                                                                     |
| Client - Admin         | An application that is specifically hitting the service with an admin token.  This cannot be the game client, but could be a server or other Platform service.                                                                                                 |
| Janitor                | A background task that runs in the service, responsible for ongoing maintenance tasks such as but not limited to data deletion.  For more information, see the [Data Retention](DATA_RETENTION.md) document.                                                   |
| Message                | An object representing some text to display to users; has optional client-defined data associated with it and an internally-used type.                                                                                                                         |
| Message - Announcement | A specific type of message representing a system-wide critical message.  Announcements appear in all chat respones when active, regardless of player rooms.  These are sent in a separate array from `roomUpdates`.                                            |
| Message - Broadcast    | Any message that was sent from an **admin client** that is not an **announcement**.  Examples might include summon evenets or congratulatory messages from another Platform service.  V1 had broadcasts too, but they were much narrower in scope and utility. |
| Message - Direct (DM)  | A message that is privately sent between two or more players.  Think of these like Slack DMs.                                                                                                                                                                  |
| Message - Unread       | Any message that has a CreatedOn timestamp that is larger than the `lastRead` timestamp sent with every non-admin client's requests.                                                                                                                           |
| Paging                 | New to most Platform services; when returning a large number of records, the data clients receive will be limited to a small number of matching records.  Requests will need to pass in a page number to see different records.                                |
| Player / User          | An end user of a client.                                                                                                                                                                                                                                       |
| Report                 | A snapshot of messages that is not modifyable by anyone, even an administrator.  These represent action items an admin has to review, at which point they can update the status but nothing else.  Player consequences are handled externally from Chat.       |
| Report - New           | A report that has not yet been reviewed.                                                                                                                                                                                                                       |
| Report - Mild          | A report that an admin wants to keep around for a short while, for example, in case the reported player continues to misbehave.                                                                                                                                |
| Report - Severe        | A report that an admin wants to keep around for a long time but not indefinitely.                                                                                                                                                                              |
| Report - Permanent     | A report that an admin wants to keep around indefinitely.                                                                                                                                                                                                      |
| Room                   | A registry of who is participating in a given room.  Each room has a type that defines slightly different experiences.                                                                                                                                         |
| Room - DM              | A room that can be created by a standard client by sending messages to one or more people.  Acts as an impromptu private chat.                                                                                                                                 |
| Room - Global          | A room that represents public chat.  Users are automatically placed into a global room if they aren't already in one with any Chat request.  Players are removed automatically after inactivity.                                                               |
| Room - Hacked          | Not actually a room.  Since clients can specify a room to send messages to, this is a classification indicating there's no actual "room" associated with the message.  Used internally to identify bad actors.                                                 |
| Room - Private         | A room only managed by admin clients.  Represents special rooms for other servers or game features.                                                                                                                                                            |

There is only one guiding principle that carries over from V1...

## Every Request Returns Unread Messages

Any endpoint that a game client or other token representing a player hits the chat-service, Platform will return unread messages for the player.  This helps keep the traffic minimal.  For _every single request_ made to Chat, the consuming client should:

* Include a UTC Unix timestamp in the query parameters with the key of `lastRead`.
* Optionally include a boolean in the query paramaters with the key of `detailed`.  If true, this will return `room.data` fields.
* Find the maximum timestamp returned in the unread messages and store it for use in the next request to Chat.
* Cache chat messages locally

It's important to state this right at the beginning as it's a critical point to both a performant client and service.

Room updates are limited to 100 rooms, sorted first by room type (Global, Private, then finally DMs), then by the last time the room's member list changed.  If someone happens to hit this 100-room limit, it's possible they will miss messages without knowing it; it may be necessary in this case to page the unread messages or otherwise force them to leave DMs, which we'll get to later.  

Returned messages are limited to `Math.Min(100 * numberOfActiveRooms, 1000)`.  Consequently, when an update is received, the client should store the most recent message's timestamp for `lastRead` - **not** the current timestamp, as there may be more messages the client is simply behind on.

Throughout these documents, please keep in mind:
1. All endpoints start with `{environment}/chat`.  This is not specified; instead all of the request documentation omits this as it is a shared root.  So when you see `PATCH /rooms/join`, your actual request should be `PATCH http://dev.nonprod.tower.cdrentertainment.com/chat/rooms/join`.
2. A unix timestamp, `lastRead` should appear in every request.  It should be in the body of every request that takes one, and as a query parameter in GET / DELETE requests (which don't support a JSON body).  This, too, is not specified in the samples.
3. The Platform Services Postman Collection should be considered the authoritative source of requests, _not_ this document, as changes will be tested using it, so it's more likely to be up-to-date than these documents.

Without further ado, let's get into implementation details.

<hr />

## Room Management

Rooms are fundamentally different in V2, though from a client perspective they might not appear to be.  Importantly, there's no need to "launch" chat anymore.  That was a requirement in V1 to initialize various bits, but with MINQ, we no longer need that.  So, now, **any endpoint in Chat** will guarantee you're in one - and exactly one - global room.

Rooms come in four flavors:

* Global Rooms, which every active player is in.  These are large capacity rooms and players can bounce between them at will.
* DM Rooms, which can be created at will by players.  Players are joined to these rooms both when sending or receiving a message.
* Private Rooms, which are admin-created, special rooms.  As an example of what a private room might be, Guilds would need a private room for all of its members; membership of private rooms is managed by admin tokens.
* Possibly Hacked Rooms, which aren't actually Rooms.  If someone decompiles the client and sends chat messages manually, they may discover the client-authoritative ability to send messages anywhere they want, including to room IDs that don't exist.  These are invisible to all clients, but this is used for cleanup / data deletion, and may be used to identify malicious actors silently in the future.

### Joining A Different Global Room

There are two endpoints we need for this.  The first is the ability to list all current global rooms:

```
GET /rooms?page=0

200 OK
{
    "rooms": [
        {
            "members": [
                "65781a2ee074f00f1e9b37e6"
            ],
            "type": "Global",
            "unread": null,
            "number": 1,
            "id": "657827358be2aefc0e263cfd",
            "createdOn": 1702373173
        },
        ...
    ],
    "page": 0,
    "roomsPerPage": 10,
    "remainingRoomCount": 0,
    "roomUpdates": [],
    "announcements": []
}
```

If you're familiar with V1, there's a very important difference here: paging.  In preparation for global scaling, V2 now has paging for its global room selection.  If we have 1 million concurrent players and 200 players per room, that would mean we have at least 5,000 global rooms - which is just far too much data to return at once.

Chat now returns only global rooms that have open capacity with this endpoint.  In its response, it also includes details for paging UI, including how many rooms are returned in each page, and how many remaining rooms beyond the page have capacity.  In order to browse rooms not returned in this, you'll need to update the `page` query parameter.

Once you have a room's ID you want to join, you'll need to hit the next endpoint:

```
PATCH /rooms/join
{
    "roomId": "deadbeefdeadbeefdeadbeef"
}

200 OK
{
    "room": {
        "members": [
            "65796aced37c60ebf6cafd80"
        ],
        "type": "Global",
        "unread": null,
        "number": 2,
        "id": "deadbeefdeadbeefdeadbeef",
        "createdOn": 1702450444
    },
    "roomUpdates": [],
    "announcements": []
}
```

Be aware that this does remove you from whatever other global room you were a part of.  Clients should block the request from going out if the target room ID is the same as the global room the player is already in; there are no Platform checks for it since it would add a database hit, so saving Chat the traffic will improve performance.

### Leaving Direct Message Rooms

There's little more annoying than a group chat you want no part of.  Luckily, Chat provides a way out of unwanted group chats:

```
DELETE /rooms/leave?roomId={...}

200 OK
{
    "roomUpdates": [],
    "announcements": []
}
```

Chat doesn't currently have support to add people to an existing DM room - either by invitation or rejoining on your own.  So, once you're out, you're out for good until someone creates a group with the same members.

You cannot leave other types of rooms.

<hr />

## Receiving Messages

As covered earlier, every client-side request to Chat will return unread messages, and should contain a `lastRead` timestamp.  The following endpoint is completely empty but intended to be the default request:

```
GET /?lastRead=1702603902

200 OK
{
    "roomUpdates": [
        {
            "members": [
                "65781a2ee074f00f1e9b37e6"
            ],
            "type": "Global",
            "unread": [ ... ],
            "number": 1,
            "id": "657956f98be2aefc0e26982b",
            "createdOn": 1702450937
        }
    ],
    "announcements": []
}
```

## Sending Messages

### Standard Chat (Global Rooms & Guild Rooms)

A message consists of three parts:

* Some `text` for the main message.
* Any additional `context`, represented by a JSON object.  Chat is completely agnostic about context, so the client can send whatever it needs to to create a rich frontend experience.  Need to include item linking?  Throw whatever you need into the context.  Private match request?  Context!
* A `roomId`, a valid 24-digit hex string representing the target room you wish to post to.

```
POST /message
{
    "lastRead": 1702371942,
    "message": {
        "text": "Hello World!",
        "context": { ... },
        "roomId": "..."             // <---- this must be included!
    }
}
```

This request will send any message to any room - though it will only be visible if you're a member of that room.

#### So, what are some use cases for the new Context?

* Content linking.  Wnat to share an item in your collection?  We could include that item's entire details in the message, where it can be parsed and used by other clients.
* PvP challenges.  In games like Clash Royale, a player can issue a challenge in chat to play a friendly, unranked PvP match.  Add some match details and add a server-side claim functionality to upadte the message and we can pair people off in easier private matches.
* Stickers & Emoji.  Add some flair to chat with context that links the message to art assets.
* Shared rewards.  Some games offer IAPs where a purchase not only grants the buyer bonuses, but also allows other players to claim a smaller bonus from a chat message.  Use a server-side rewards claim / update the message if necessary.

### Direct Messages

Chat supports direct messages for up to 20 players.  Keep in mind that a player's token constitutes one of the spots when creating a new DM.

When sending any DM, the `players` array is a requirement.  This must include valid mongo IDs - 24-digit hex strings - as they represent other accountIds.

To create a new DM room:

```
POST /dm
{
    "lastRead": 1702371942,
    "message": {
        "text": "Hey buddies!",
        "context": { ... }
    },
    "players": [
        "deadbeefdeadbeefdeadbeef",
        "badf00dbadf00dbadf00dbad",
        ...
    ]
}
```

When you're starting a new DM, there's no existing room, and consequently no way to populate the `roomId` field, so it is omitted here.

Once the DM room exists, you should go back to using `/message` to send subsequent messages, along with the DM roomId.

<hr />

#### IMPORTANT NOTES

1. If the _wrong roomId_ is supplied, the message will not go to its intended recipients, but rather _whatever room is specified!_
2. Chat filters out unread messages based on a room's members.  Message injection into a room you're not a member of isn't discoverable by other clients - though the data will still get created.
3. DMs must contain at least one other player.

<hr />

We've had some discussions around PvP / Coop chat rooms.  With DM support, we now have the ability to implement this if we want!  Some considerations:

* On a PvP match start, if we want to guarantee the room exists, we can send a message from a server with all players as recipients.
* We have some options:
  * Include the server's account ID, so that the DM is actually 3 "players".  This means the DM room will be separate from two players that match up who already have DMs together.  At the end of the match, if we want the messages to be unavailable after the match, we can have the server remove the players from it / delete the room.  This is the lightest on the database load since we can remove data as soon as it's no longer relevant.
  * Leave the DM as just between the two players.  If we want to hide non-PvP messages, messages sent during PvP can include a value in the `context` field to act as a filter.  This gives us the option of allowing PvP messages to persist beyond the match, so if we want post-game banter or more social connection, it could be available this way.


<hr />

## Preferences

In V1, preferences consisted of a squelch list, and managing it required multiple endpoints.  In V2, it's been simplified to be more flexible and client-authoritative.

Now you can store any data you want and Platform won't care.

To retrieve preferences:
```
GET /preferences
{
    "preferences": {
        "accountId": "65781a2ee074f00f1e9b37e6",
        "settings": {
            "hello": 123,
            "world": [
                "abc",
                "def",
                "ghi"
            ]
        },
        "updatedOn": 1702457871,
        "id": "65796e0c8be2aefc0e26c79f",
        "createdOn": 1702456844
    }
}
```

Then, to update them:

```
PUT /preferences/update
{
    "settings": {
        // Any data you want
    }
}
```

This was something that was impossible before platform-common had the ability to store data agnostically.  This endpoint does have a request size limit attached to it, however.  It's large enough that it's unlikely for simple settings to grow to the point of becoming problematic - though it is smaller than normal request size limits Platform usually sees.

Keep in mind though that this request **replaces whatever is stored** with the request data.  It will be easy to wipe it clean if not careful, and once that data is erased, it's gone for good.

So, what are some use cases for the new Preferences?

* Squelching.  This can be used to hide any messages sent by a player.  Chat won't enforce it, but this allows each client to maintain its own squelch list with data persistence across devices.
  * Important note: now that we have DMs, we have a new edge case: if a squelched player sends you a message, even if you have a preference to squelch them, you'll still see unread messages.  If unhandled, this will cause a blank room to appear when the game client can support DMs, so squelches should also remove rooms from updates when all other players in the room are squelched.
* Saved messages.  Maybe a player hit a really rare achievement in the game, such as pulling a really rare item that was broadcast, complete with rich item linking in the context.  Being able to save that message in its entirety guarantees the player has access to it and it won't be deleted!
* Favorite rooms.  When we have millions of players, community members may want to pick certain rooms to meet up in.
* Chat themes.  Perhaps as achievements, we could have different UI skins for chat.
* Localization preferences
* Mute PvP chat

Rather than build both a client and server pairing for all of these features (and possibly more), the client can now define whatever logic it wants by throwing a blob of data at the service and having it stick.

<hr />

## Reports

Players can report messages for objectionable content:

```
POST /report
{
    "messageId": "deadbeefdeadbeefdeadbeef"
}

200 OK
{
    "report": {
        "offendingMessageId": "65797740214a66de422ac5ee",
        "firstReporterId": "65781a2ee074f00f1e9b37e6",
        "reporterIds": [
            "65781a2ee074f00f1e9b37e6"
        ],
        "reportedCount": 1,
        "messageLog": [
            ...
            {
                "accountId": "65781a2ee074f00f1e9b37e6",
                "text": "2",
                "context": {
                    "foo": 123,
                    "bar": null,
                    "fubar": true
                },
                "id": "6579773d214a66de422ac5ec",
                "createdOn": 1702459197
            },
            {
                "accountId": "deadbeefdeadbeefdeadbeef",
                "text": "3",
                "context": {
                    "foo": 123,
                    "bar": null,
                    "fubar": true
                },
                "id": "65797740214a66de422ac5ee",
                "createdOn": 1702459200
            },
            {
                "accountId": "65781a2ee074f00f1e9b37e6",
                "text": "4",
                "context": {
                    "foo": 123,
                    "bar": null,
                    "fubar": true
                },
                "id": "65797744214a66de422ac5f0",
                "createdOn": 1702459204
            },
            ...
        ],
        "roomId": "65796e0c8be2aefc0e26c7a5",
        "status": 0,
        "adminNote": null,
        "id": "657977708be2aefc0e26db1f",
        "createdOn": 1702459248
    },
    "roomUpdates": [ ... ],
    "announcements": []
}
```

No further action is needed on a player's side.  Players can report a message as many times as they want - this will only increase `reportedCount` when repeating a request.  As more players report the same message, `reporterIds` will also track secondary reporters.

The `messageLog` will also continue to evolve up to a point; up to 25 messages before and after the offending message are stored, for a total of 51 messages.  So, if the message in question is reported as soon as it's sent, and then reported 10 minutes later with more chatter, more messages will be merged into the report.

## Regarding Bans V2

In July 2023, we added Bans V2 to our Platform suite.  This update allows us to ban player tokens from a central point, and selectively ban them from various Platform servers.  So, as an example, we could ban a player from just Chat or just Leaderboards, or even IAPs.

Chat V1 manages its own ban list using multiple endpoints, and caused inconsistent behaviors when a player was banned.  Now that bans are standardized, chat no longer is responsible for bans.

However, it's important that a chat ban does not cause a consuming client to crash.  When a token is banned from the service, a 401 Unauthorized error will be returned as a response from the server.  When a ban is in effect, **no endpoint** will work.  Bans may be temporary or permanent.

Bans can be issued either by a customer service rep acting on reports or _automatically_ by Chat.  Chat V2 has some features to automatically detect bad behavior and issues bans to stop it.

Bans can be temporary or permanent.

### Automatic Bans

The logic of automatic bans is subject to change, so information here may be out-of-date, but the goal of automatic bans is to curb malicious actors before they become problems.  So many games have chat spam that promote links, or otherwise copy / paste spam in various channels, that chat systems become unusable Twitch-style streams of garbage posts.

Chat discourages this in two ways:

1. By regularly checking to see if players have sent a large number of _identical_ messages in a short period of time.  If Chat determines the player is too spammy - and particularly if it's across multiple rooms - Chat will ban the player.  This is a progressive ban; the first offense will be light - say a few minutes - but continued offenses will start increasing the ban time.  These bans are never permanent - that's only for CS to decide - but on the upper end they will last several days. 
2. Chat will issue a short ban for players who spam too many requests in a short period of time.  This is intended to keep our servers open for traffic, as every request to chat results in multiple database hits.  This ban will not increase in duration and is just intended to force a cooldown.  The goal is to set the bar high enough that only someone intentionally spammy would ever see it.  The error code associated with this will be `HTTP 418 I'm a teapot`; go make some tea, and maybe when you're done with it you'll be able to chat again.