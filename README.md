TimeUtils
=========

Some utility classes to interact with Quartz.NET

#### Project Setup

_Includes a solution with a test class and the SimpleQuartz and TimeTrigger classes_ 

#### License

The content of this project itself is licensed under the
[Creative Commons Attribution 3.0 license](http://creativecommons.org/licenses/by/3.0/us/deed.en_US),
and the underlying source code used to format and display that content
is licensed under the [MIT license](http://opensource.org/licenses/mit-license.php).

#### Example
```cs
var sq = new SimpleQuartz();
// simple Repeats
sq.RepeatForever(TimeSpan.FromMinutes(1), () => Console.WriteLine(DateTime.Now));
// run at specific times. can pass an object to the action
sq.RunAt(DateTime.Now.AddDays(1), sq, q => q.Shutdown());
// schedule quartz.net triggers
var key = new TriggerKey("TriggerName", "GroupName");
var trigger = TriggerBuilder.Create()
    .WithIdentity(key)
    //.WithIdentity("TriggerName", "GroupName")
    .StartAt(DateTimeOffset.Now.AddDays(1))
    .WithCronSchedule("0 0 12 ? * TUE,SAT *")
    .Build();
sq.ScheduleTrigger(trigger, () => Console.Write("Cron expression triggered"));
// unschedule specific trigger keys or by GroupMatcher
sq.Unschedule(GroupMatcher<TriggerKey>.AnyGroup());
sq.Unschedule(key);
// unschedules all jobs
sq.UnscheduleAll();

// retry mechanism
sq.Retryer(
    // retry interval
    TimeSpan.FromSeconds(30),
    // retry count
    5,
    // data for action
    trigger,
    // the retry action
    t =>
    {
        if (t.GetMayFireAgain())
        {
            // if the action returns false, it will be retryed
            return false;
        }
        return true;
    },
    t => Console.Write("Fallback action after all retries"));

//shutdown this instance
sq.Shutdown();
SimpleQuartz.ShutdownAllQuartzInstances();
```

[![Build status](https://ci.appveyor.com/api/projects/status/8m09dfowqm6e9893/branch/master?svg=true)](https://ci.appveyor.com/project/pitermarx/timeutils/branch/master)
