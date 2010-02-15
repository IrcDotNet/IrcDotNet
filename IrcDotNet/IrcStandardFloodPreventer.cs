using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Represents a flood protector that operates according to the de-facto rules implemented by modern IRC servers.
    /// The principle is that no message may be sent by the client once the value of an internal counter has reached
    /// the value of <see cref="MaxMessageBurst"/>. The counter is incremented every time a message is sent, and
    /// decremented by one every duration of <see cref="CounterPeriod"/>. Hence, messages may be sent immediately in
    /// bursts so long as the high rate is not sustained, else a delay is introduced between the sending of
    /// successive messages.
    /// </summary>
    public class IrcStandardFloodPreventer : IIrcFloodPreventer
    {
        private const int ticksPerMillisecond = 10000;

        // Number of messages sent within current burst.
        private int messageCounter;
        // Absolute time of last counter decrement, in milliseconds.
        private long lastCounterDecrementTime;

        private int maxMessageBurst;
        private int counterPeriod;

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcStandardFloodPreventer"/> class.
        /// </summary>
        /// <param name="maxMessageBurst">The maximum number of messages that can be sent in a burst.</param>
        /// <param name="counterPeriod">The number of milliseconds between each decrement of the message counter.
        /// </param>
        public IrcStandardFloodPreventer(int maxMessageBurst, int counterPeriod)
        {
            this.maxMessageBurst = maxMessageBurst;
            this.counterPeriod = counterPeriod;

            this.messageCounter = 0;
            this.lastCounterDecrementTime = 0;
        }

        /// <summary>
        /// Gets the maximum message number of messages that can be sent in a burst.
        /// </summary>
        /// <value>The maximum message number of messages that can be sent in a burst..</value>
        public int MaxMessageBurst
        {
            get { return this.maxMessageBurst; }
        }

        /// <summary>
        /// Gets the number of milliseconds between each decrement of the message counter.
        /// </summary>
        /// <value>The number of milliseconds that is the period of the counter.</value>
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
