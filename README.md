# chat-service
 API for social messaging

# About The Service

The Chat Service (Chat) does not differentiate between global chats, guild chats, direct messages (DMs), or any other variants.  After all, a DM is just a chat room with a limit of two people.  To minimize the number of requests, Chat returns a response containing all `RoomUpdates` with every interaction.  These `RoomUpdates` contain every unread message in every room a player is in, regardless of whether or not it's a DM, global chat, or any other room they're a member of.

Joining a room and sending a message alike should then be used to update client-side chats.  More on this in (section).

# Consuming The Service
Every chat-related `POST` endpoint requires the following:
* An `Authorization` header with a value set to `Bearer {token}`, where the token is issued from `player-service`.
    * This is used to authenticate the client via `player-service`'s `/player/verify` endpoint.
* A `lastRead` field, which is the Unix timestamp of the most recently-read message.
    * While `lastRead` _can_ be `0`, this will return _all_ messages in _every_ room the player is in and is strongly discouraged.

Every JSON response contains an object that can be used to update the client, as in the sample below:

    {
        ...
        "roomUpdates": [
            {
                "id": "60e8d73536134314d16b207a",
                "unreadMessages": [
                    {
                        "id": "eda93b9a-3862-4a6f-9a73-a02fd3408a95",
                        "text": "Anger leads to hate.",
                        "timestamp": 1625872345,
                        "type": "chat",
                        "accountId": "5f727b4dc60f5a956eb1c551"
                    },
                    {
                        "id": "2eaee976-e769-4076-bdd5-d55bf6b64d70",
                        "text": "Hate leads to suffering!",
                        "timestamp": 1625872381,
                        "type": "chat",
                        "accountId": "5f727b4dc60f5a956eb1c551"
                    }
                ],
                "members": [
                    {
                        "accountId": "5f727b4dc60f5a956eb1c551",
                        "avatar": "demon_axe_thrower",
                        "inRoomSince": 1626242762,
                        "screenName": "Slartibartfast"
                    },
                    {
                        "accountId": "60a43b0c70edc8aa7cf3bed6",
                        "avatar": "demon_axe_thrower",
                        "inRoomSince": 1626245489,
                        "screenName": "Arthur Dent"
                    }
                ]
            },
            {
                "id": "60ee83aa734d06135565dda7",
                "unreadMessages": [],
                "members": [
                    {
                    "accountId": "5f727b4dc60f5a956eb1c551",
                    "avatar": "demon_axe_thrower",
                    "inRoomSince": 0,
                    "screenName": "Slartibartfast"
                    }
                ]
            }
        ]
        ...
    }
In this sample, two separate `RoomUpdates` are returned.  In the first `Room`, we have two unread messages that have not been seen by the client.  The `members` field contains a list of all of a room's members so that messages can be linked to appropriate accounts, avatars, and screennames.  More data may be included in this as Chat evolves.

In the second room, there are no new messages.

_Future optimization: requests to Chat could include `Room` accountIds so that Chat can return only new members / members that have left the room, reducing data load.  Returning information on a full global room would be substantial on every request._

_Future optimization: JSON keys can be shortened to significantly reduce the amount of data stored in mongo and data load._

# Endpoints

## `POST /message/broadcast`

Make a request to this endpoint to send an Activity broadcast out to the user's global channel.  The `Message` will be sent to all other broadcast channels as they are added (e.g. Friend Activity chat room, guilds).

Expected body example:

    {
        "lastRead": 1625704809,
        "message": {
            "text": "Don't panic!"
        }
    }

## `POST /message/send`

Make a request to this endpoint to send a `Message` to a specified `Room`.  If a user is not a member of the `Room`, the request will fail.

Expected body example:

    {
        "lastRead": 1625704809,
        "message": {
            "text": "Don't panic!"
        },
        "roomId": "deadbeefdeadbeefdeadbeef"
    }

## `POST /message/unread`

Make a request to this endpoint to retrieve all `RoomUpdates` for the player.  This should be a scheduled call anywhere in-game chat is accessible, with the timer restarted anytime another call is made (since these events also return all `RoomUpdates`).

Expected body example:

    {
        "lastRead": 1625704809
    }

## `POST /room/join`

### **Caution:** This is not intended for use with global `Rooms`.  This is for prototyping purposes and will likely be removed in the future.

Make a request to this endpoint to join a `Room`.  Upon joining, the response JSON will contain the joined `Room`'s information in addition to the player's `RoomUpdates`.

Expected body example:

    {
        "lastRead": 1625704809,
        "playerInfo": {
            "avatar": "demon_axe_thrower",
            "sn": "Slartibartfast"
        },
        "roomId": "deadbeefdeadbeefdeadbeef"
    }

Sample output:

    {
        "room": {
            "id": "60e8d73536134314d16b207a",
            "capacity": 50,
            "createdTimestamp": 1625872181,
            "guildId": null,
            "language": "en-US",
            "messages": [
                {
                "id": "eda93b9a-3862-4a6f-9a73-a02fd3408a95",
                "text": "Anger leads to hate.",
                "timestamp": 1625872345,
                "accountId": "5f727b4dc60f5a956eb1c551"
                }, ...
            ],
            "members": [
                {
                    "accountId": "5f727b4dc60f5a956eb1c551",
                    "avatar": "demon_axe_thrower",
                    "screenName": "Slartibartfast"
                }, ...
            ],
            "type": "global",
            "isFull": false
        },
        "roomUpdates": [ ... ]
    }

## `POST /room/global/join`

Make a request to this endpoint to join a global `Room`.  Upon joining, the response JSON will contain the joined `Room`'s information in addition to the player's `RoomUpdates`.

**Required value:** `language`

* Global `Rooms` are automatically created as they reach capacity and are segregated by their language.  This can be any value, so it's up to the client to decide how to handle language assignment.  For Chat V1, use the same value for all requests (e.g. `"language": "global"`).

**Optional value:** `roomId`.  This value must be the ID of a `Room` that is:

* A global `Room` with the same language as the required value above
* A `Room` that is not full
* The user is not already in

The user is removed from their previous global `Room` when this request is successful, so there is no need to call `/room/leave` for global chat.

Expected body example:

    {
        "lastRead": 1625704809,
        "playerInfo": {
            "avatar": "demon_axe_thrower",
            "sn": "Slartibartfast"
        },
        "roomId": "deadbeefdeadbeefdeadbeef"
    }

Sample output:

    {
        "room": {
            "id": "60e8d73536134314d16b207a",
            "capacity": 50,
            "createdTimestamp": 1625872181,
            "guildId": null,
            "language": "en-US",
            "messages": [
                {
                "id": "eda93b9a-3862-4a6f-9a73-a02fd3408a95",
                "text": "Anger leads to hate.",
                "timestamp": 1625872345,
                "accountId": "5f727b4dc60f5a956eb1c551"
                }, ...
            ],
            "members": [
                {
                    "accountId": "5f727b4dc60f5a956eb1c551",
                    "avatar": "demon_axe_thrower",
                    "screenName": "Slartibartfast"
                }, ...
            ],
            "type": "global",
            "isFull": false
        },
        "roomUpdates": [ ... ]
    }
## `POST /room/leave`

### **Caution:** This is for prototyping purposes and will likely be removed in the future, being replaced with a call like `/room/global/leave` or `/room/guild/leave` to guarantee users aren't removed from DMs or other "permanent" chats.

Make a request to this endpoint to leave a `Room`.  Clients should hit this endpoint when exiting - such as a game exit or a user force-closing the app (if possible to capture) - so that users don't sit in global `Rooms` when they're actually offline.
    
Expected body example:
  
    {
        "lastRead": 1625704809,
        "roomId": "deadbeefdeadbeefdeadbeef"
    }