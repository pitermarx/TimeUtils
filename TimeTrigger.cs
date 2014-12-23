using System;
using System.Threading;
using System.Threading.Tasks;

namespace pitermarx.TimeUtils
{
    public class TimeTrigger
    {
        // private internal structures
        private CancellationTokenSource cancellationTokenSource;

        private bool started;

        private SimpleQuartz sq;

        public TimeSpan Period { get; private set; }

        /// <summary>
        /// An object to be passed to the TimerElapsed event
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// The event to be fired on the period defined in the constructor
        /// </summary>
        public event Action<object, object> TimerElapsed;

        private void OnTimerElapsed(object sender, object obj)
        {
            var handler = this.TimerElapsed;
            if (handler != null)
            {
                handler(sender, obj);
            }
        }

        /// <summary>A class that fires an event after some time</summary>
        /// <param name="period">The period in which the event is called</param>
        public TimeTrigger(TimeSpan period)
        {
            if (period < TimeSpan.FromMilliseconds(1))
            {
                throw new ArgumentException("Time span cannot be smaller than 1 millisecond.");
            }

            this.Period = period;
            this.started = false;
        }

        /// <summary>
        /// Start the TimeTrigger now
        /// </summary>
        public void Start()
        {
            this.Start(TimeSpan.Zero);
        }

        /// <summary>
        /// Start the TimeTrigger after a delay
        /// </summary>
        /// <param name="delay">The delay</param>
        public void Start(TimeSpan delay)
        {
            // validate. cannot start twice
            if (this.started)
            {
                throw new Exception("Cannot start. Already started");
            }

            this.cancellationTokenSource = new CancellationTokenSource();
            this.started = true;

            // after a delay, start the scheduler
            Task.Delay(delay)
                .ContinueWith(t =>
                    {
                        // only if it was not disposed
                        if (!this.cancellationTokenSource.IsCancellationRequested)
                        {
                            this.sq = new SimpleQuartz();
                            sq.RepeatForever(this.Period, this.Data, o => this.OnTimerElapsed(this, o));
                        }
                    });
        }

        public void Stop()
        {
            if (this.sq != null)
            {
                this.sq.Shutdown();
                this.sq = null;
            }

            if (this.cancellationTokenSource != null)
            {
                this.cancellationTokenSource.Cancel();
            }

            this.started = false;
        }

        public bool IsRunning()
        {
            return this.started;
        }
    }
}