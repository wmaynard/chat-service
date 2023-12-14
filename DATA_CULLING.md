# Data Culling in Chat

User-generated content is **notoriously** heavy for storage requirements and always runs a risk of skyrocketing cloud computing bills.  Consequently, we need to have a strict policy on how long we actually retain all information in Chat.  This document will cover all types of data, how long we keep it, and guidelines on using the service in a data-friendly way.

Chat has a Janitor service that runs in the background multiple times per day and creates cleanup tasks to clean out unneeded data at regular intervals.  Once the Janitor purges data, it is _gone_.

Refer to the following table to see how long Chat keeps its various data.

| Data Type              | Retention Policy                         |
|:-----------------------|:-----------------------------------------|
| Global Messages        | 2 weeks                                  |
| Direct Messages        | 1 month                                  |
| Private Messages       | 3 months                                 |
| Broadcasts             | 2 weeks default; overrideable            |
| Announcements          | Permanent                                |
| Global Rooms           | Permanent                                |
| Direct Message Rooms   | Permanent so long as messages exist      |
| Private Rooms          | Permanent so long as members exist       |
| Player Preferences     | Permanent                                |
| Player Activity Record | Permanent                                |
| New Reports            | 1 month                                  |
| Mild Reports           | 2 weeks                                  |
| Severe Reports         | 3 months                                 |
| Permanent Reports      | Permanent, did you expect anything else? |
