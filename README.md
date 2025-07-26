## Events

Events are facts. They represent info about what has happened., Just like in real life, what has been seen cannot be unseen. Therefore, events are immutable. So, it doesnt make sense to name events like `BankAccountCreated`, `BankAccountUpdated` etc, because that is not info. So we should name it properly. Something like `BankAccountOpened`, `DepositRecorded` etc.

All of those events gather specific info that is for that particular result. If you take a look at the `Events.cs` you will see this.

