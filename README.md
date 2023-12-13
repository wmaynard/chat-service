# Chat V2

 An API for in-game social messaging - now new and improved!

## Introduction

Chat V1 was a particularly interesting Platform project - because it predated platform-common, it had a lot of wild west-style code and a healthy amount of duct tape binding it together.  It was lightweight, responsive, and never complained about its RPS (requests per second) though, so despite the stress we hit it with, it actually performed quite well.

However, it had some significant downfalls.  It was very difficult to maintain, not being built on the tooling that most of Platform relies on now, and adding new features such as guild chat was not going to be clean or easy.

This guide will walk you through how to use the second iteration of chat.  There is only one guiding principle that carries over from V1...

## Every Request Returns Unread Messages

Any endpoint that a game client or other token representing a player hits the chat-service, Platform will return unread messages for the player.  This helps keep the traffic minimal.  For _every single request_ made to Chat, the consuming client should:

* Include a UTC Unix timestamp in the body or parameters (as appropriate) with the key of `lastRead`.
* Find the maximum timestamp returned in the unread messages and store it for use in the next request to Chat.

It's important to state this right at the beginning as it's a critical point to both a performant client and service.

Room updates are limited to 100 rooms, sorted first by room type (Global, Private, then finally DMs), then by the last time the room's member list changed.  If someone happens to hit this 100-room limit, it's possible they will miss messages without knowing it; it may be necessary in this case to page the unread messages or otherwise force them to leave DMs, which we'll get to later.  

Returned messages are limited to `Math.Min(100 * numberOfActiveRooms, 1000)`.  Consequently, when an update is received, the client should store the most recent message's timestamp for `lastRead` - **not** the current timestamp, as there may be more messages the client is simply behind on.

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
    "roomUpdates": []
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
    "roomUpdates": []
}
```

Be aware that this does remove you from whatever other global room you were a part of.

### Leaving Direct Messages

There's little more annoying than a group chat you want no part of.  Luckily, Chat provides a way out of unwanted group chats:

```
DELETE /rooms/leave?roomId={...}

200 OK
{
    "roomUpdates": []
}
```

Chat doesn't currently have support to add people to an existing DM room - either by invitation or rejoining on your own.  So, once you're out, you're out for good until someone creates a group with the same members.

<hr />

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
PUT /preferences/save
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
    "roomUpdates": [ ... ]
}
```

## Regarding Bans V2