using System;
using System.Threading;
using NUnit.Framework;

namespace pitermarx.TimeUtils
{
    [TestFixture]
    public class TimeTriggerTests
    {
        private static TimeTrigger CreateTimeTrigger(EventWaitHandle wait, TimeSpan period)
        {
            var trigg = new TimeTrigger(period);
            trigg.TimerElapsed += (s,d) => wait.Set();
            return trigg;
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void TriggerToSmallTimeSpan()
        {
            // Act: Create TimeTrigger with span < 1ms (should throw an exception)
            var trigg = new TimeTrigger(new TimeSpan(50));
            Assert.IsNull(trigg);
        }

        [Test]
        public void TriggerFires()
        {
            // arrange
            var wait = new AutoResetEvent(false);
            var trigg = CreateTimeTrigger(wait, TimeSpan.FromSeconds(1));

            // act
            trigg.Start();

            // assert
            Assert.IsTrue(wait.WaitOne(2000));
            Assert.IsTrue(wait.WaitOne(2000));
            Assert.IsTrue(wait.WaitOne(2000));

            trigg.Stop();
        }

        [Test]
        public void DisposeWorks()
        {
            // arrange
            var wait = new AutoResetEvent(false);
            var trigg = CreateTimeTrigger(wait, TimeSpan.FromSeconds(10));

            // act
            trigg.Start(TimeSpan.Zero);
            Assert.IsTrue(wait.WaitOne(2000));
            trigg.Stop();

            // assert
            Assert.IsFalse(wait.WaitOne(2000));
        }

        [Test]
        public void DelayWorks()
        {
            // arrange
            var wait = new AutoResetEvent(false);
            var trigg = CreateTimeTrigger(wait, TimeSpan.FromMinutes(2));

            // act
            trigg.Start(TimeSpan.FromSeconds(10));

            // assert
            Assert.IsFalse(wait.WaitOne(2000));
            Assert.IsTrue(wait.WaitOne(10000));
            Assert.IsFalse(wait.WaitOne(1000));

            trigg.Stop();
        }

        [Test]
        public void CannotStartTwice()
        {
            var t = CreateTimeTrigger(null, TimeSpan.FromMinutes(5));
            t.Start(TimeSpan.FromDays(5));
            Assert.Throws<Exception>(t.Start);
            t.Stop();
        }
    }
}