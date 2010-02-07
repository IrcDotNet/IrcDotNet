using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcStandardFloodPreventer : IIrcFloodPreventer
    {
        private const int ticksPerMillisecond = 10000;

        // Number of messages sent within current burst.
        private int messageCounter;
        // Absolute time of last counter decrement, in milliseconds.
        private long lastCounterDecrementTime;

        private int maxMessageBurst;
        private int counterPeriod;

        public IrcStandardFloodPreventer(int maxMessageBurst, int counterPeriod)
        {
            this.maxMessageBurst = maxMessageBurst;
            this.counterPeriod = counterPeriod;

            this.messageCounter = 0;
            this.lastCounterDecrementTime = 0;
        }

        public int MaxMessageBurst
        {
            get { return this.maxMessageBurst; }
        }

        public int CounterPeriod
        {
            get { return this.counterPeriod; }
        }

        #region IIrcFloodPreventer Members

        public bool CanSendMessage()
        {
            // Subtract however many counter periods have elapsed since last decrement of counter.
            var currentTime = DateTime.Now.Ticks / ticksPerMillisecond;
            var elapsedMilliseconds = currentTime - this.lastCounterDecrementTime;
            this.messageCounter = Math.Max(0, this.messageCounter -
                (int)(elapsedMilliseconds / this.counterPeriod));
            // Update time of last decrement of counter to theoretical time of decrement.
            this.lastCounterDecrementTime = currentTime - (elapsedMilliseconds % this.counterPeriod);

            //return this.messageCounter <= this.maxMessageBurst;
            return true;
        }

        public void HandleMessageSent()
        {
            this.messageCounter++;
        }

        #endregion
    }
}
