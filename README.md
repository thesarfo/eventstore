## Events

Events are facts. They represent info about what has happened., Just like in real life, what has been seen cannot be unseen. Therefore, events are immutable. So, it doesnt make sense to name events like `BankAccountCreated`, `BankAccountUpdated` etc, because that is not info. So we should name it properly. Something like `BankAccountOpened`, `DepositRecorded` etc.

All of those events gather specific info that is for that particular result. If you take a look at the `Events.cs` you will see this.

### Event Stores
They are simple data structures that are used to store events. An example is an append-only log, where once we record an event, we do not update existing facts but rather append the new change to what we already have. And this data structure as represented in real memory can be shareded or indexed in a way. We can break it down to find that it contains streams. In event sourcing, streams represent entities. Each entity represent its own streams. so a stream is an event id and all the events that were recorded for that

There are 2 types of event stores
1. EventStoreDB -> Like a db that was natively built for event sourcing. 
2. EventStores built on other storage. like relational dbs. For instance, marten with postgres.

for instance in postgres, we can create an event like

```sql
create table events(
    id uuid not null primary key,
    stream_id uuid not null,
    version bigint not null,
    type varchar(500) not null,
    data jsonb not null
)
```

### Optimistic Concurrency
talk about it if we pass the version of the specific record and lets say that 2 people want to take 100$ they can sync their clocks to try to cheat the bank. So the version of the stream will be the number of events recorded in the stream. so when we try to withdraw we pass the version to the api and the api will check if the version exists in the db and we pass. if not we reject. then the next person will try to do the same thing but the system will pass version number 3 cause the previous withdrawal updated the version to 4

to handle optimistic concurrency we need to pass the expected stream version because then instead of querying all possible events we can just check if the stream already exists and check its version if the strem doesnt exist the version is set to 0 and we insert the new record of the stream which is to be created and then we can check whether the version matches.


### Transactions
so now sure, we know that we can create our events and streams, but we want the inserts to be transactional such that if one operation fails inside our process ,the entire things is rolled back.

### Getting events
so we can get all the events for a specific stream by querying the db with the stream id. and then we can get the events in order of their version. so we can get all the events for a specific stream and then we can replay them to get the current state of the entity. for instance in finance, we might want to get a report at a specific version or date so we should be able to get the events for that stream and then replay them to get the current state of the entity.

### Aggregations


### projections
projections are write models. how do we get a list of bank accounts, should we get al the list of events for all the bank accounts? it doesnt make sense so we use projections. projections are different interpretations of the same information. this means that facts can be interpreted differently like a boxing match result whereby the winner, loser and spectators will all have different interprations of what has heppened.

on the code level, we take the set of events, materialize them and store them into a db. we usually do it using a left fold method this means we take events on the left and then we apply them to the right. 

