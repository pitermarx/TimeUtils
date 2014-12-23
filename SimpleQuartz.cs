using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
                        action((T)context.JobDetail.JobDataMap["Data"]);
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

            Schedulers.Add(this.scheduler);
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

        public DateTimeOffset? Schedule<T>(Func<SimpleScheduleBuilder, SimpleScheduleBuilder> sch, T data, Action<T> action)
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
            var jobDataMap = new JobDataMap();
            jobDataMap["Action"] = action;
            jobDataMap["Data"] = data;

            var ijob = JobBuilder.Create<SimpleActionJob<T>>()
                .WithIdentity(new JobKey(Guid.NewGuid().ToString()))
                .UsingJobData(jobDataMap)
                .Build();

            this.scheduler.ScheduleJob(ijob, trigger);

            return trigger.GetNextFireTimeUtc();
        }

        public void Unschedule(TriggerKey key)
        {
            this.scheduler.UnscheduleJob(key);
        }

        /// <summary> Shutsdown the scheduler </summary>
        public void Shutdown(bool waitForJobs = false)
        {
            this.scheduler.Shutdown(waitForJobs);
            Schedulers.Remove(this.scheduler);
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
    }
}