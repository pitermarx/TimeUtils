using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Quartz.Simpl;

namespace pitermarx.TimeUtils
{
    /// <summary>
    /// A facade for the quartz library
    /// Can only schedule simple "Actions"
    /// </summary>
    public class SimpleQuartz
    {
        /// <summary>
        /// Internally used IJob for quartz
        /// </summary>
        private class SimpleActionJob<T> : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                // assert if it has an action
                if (context.JobDetail.JobDataMap.ContainsKey("Action"))
                {
                    var action = context.JobDetail.JobDataMap["Action"] as Action<T>;
                    if (action != null)
                    {
                        // execute action
                        action((T) context.JobDetail.JobDataMap["Data"]);
                    }
                }
            }
        }

        private readonly IScheduler scheduler;
        private readonly string name = Guid.NewGuid().ToString();

        private static readonly List<IScheduler> Schedulers = new List<IScheduler>();

        public SimpleQuartz()
        {
            // create scheduler
            DirectSchedulerFactory.Instance.CreateScheduler(
                this.name,
                this.name,
                new SimpleThreadPool(5, ThreadPriority.Normal),
                new RAMJobStore());

            // and start it
            this.scheduler = DirectSchedulerFactory.Instance.GetScheduler(this.name);
            this.scheduler.Start();

            lock (Schedulers) Schedulers.Add(this.scheduler);
        }

        public DateTimeOffset? RepeatForever(TimeSpan interval, Action action)
        {
            return this.Schedule(x => x.WithInterval(interval).RepeatForever(), new object(), _ => action());
        }

        public DateTimeOffset? RepeatForever<T>(TimeSpan interval, T data, Action<T> action)
        {
            return this.Schedule(x => x.WithInterval(interval).RepeatForever(), data, action);
        }

        public DateTimeOffset? Schedule(Func<SimpleScheduleBuilder, SimpleScheduleBuilder> sch, Action a)
        {
            return this.Schedule(sch, new object(), _ => a());
        }

        public DateTimeOffset? Schedule<T>(Func<SimpleScheduleBuilder, SimpleScheduleBuilder> sch, T data,
            Action<T> action)
        {
            var itrigger = TriggerBuilder.Create()
                .StartNow()
                .WithSimpleSchedule(s => sch(s).Build())
                .Build();

            return this.ScheduleTrigger(itrigger, data, action);
        }

        public DateTimeOffset? ScheduleTrigger(ITrigger trigger, Action action)
        {
            return this.ScheduleTrigger(trigger, new object(), _ => action());
        }

        public DateTimeOffset? ScheduleTrigger<T>(ITrigger trigger, T data, Action<T> action)
        {
            IDictionary<string, object> dataMap = new Dictionary<string, object> {{"Action", action}, {"Data", data}};
            var jobDataMap = new JobDataMap(dataMap);

            var ijob = JobBuilder.Create<SimpleActionJob<T>>()
                .WithIdentity(new JobKey(Guid.NewGuid().ToString()))
                .UsingJobData(jobDataMap)
                .Build();

            this.scheduler.ScheduleJob(ijob, trigger);

            return trigger.GetNextFireTimeUtc();
        }

        public DateTimeOffset? RunAt<T>(DateTime time, T data, Action<T> action)
        {
            return this.ScheduleTrigger(
                TriggerBuilder.Create()
                    .StartAt(DateBuilder.DateOf(time.Hour, time.Minute, time.Second, time.Day, time.Month, time.Year))
                    .WithSimpleSchedule(s => s.WithRepeatCount(0))
                    .Build(),
                data,
                action);
        }

        public void Unschedule(TriggerKey key)
        {
            this.scheduler.UnscheduleJob(key);
        }

        public void Unschedule(GroupMatcher<TriggerKey> matcher)
        {
            this.scheduler.UnscheduleJobs(this.scheduler.GetTriggerKeys(matcher).ToList());
        }

        /// <summary> Shutsdown the scheduler </summary>
        public void Shutdown(bool waitForJobs = false)
        {
            this.scheduler.Shutdown(waitForJobs);
            lock (Schedulers) Schedulers.Remove(this.scheduler);
        }

        /// <summary> Unschedules all jobs </summary>
        public void UnscheduleAll()
        {
            var triggers = this.scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup()).ToList();
            this.scheduler.UnscheduleJobs(triggers);
        }

        /// <summary>
        /// Shuts down all simple quartz
        /// </summary>
        public static void ShutdownAllQuartzInstances()
        {
            Schedulers.ForEach(s => s.Shutdown());
        }

        public Task<bool> Retryer<T>(TimeSpan retryInterval, int retryCount, T data, Func<T, bool> action,
            Action<T> fallback = null)
        {
            // if the action return true, no retry is needed
            if (action(data))
            {
                return Task.FromResult(true);
            }

            return Task.Run(() =>
            {
                int counter = 0;
                bool success;
                do
                {
                    counter += 1;
                    Task.Delay(retryInterval);
                    success = action(data);
                } while (counter <= retryCount && !success);

                if (!success && fallback != null)
                {
                    fallback(data);
                }

                return success;
            });
        }
    }
}