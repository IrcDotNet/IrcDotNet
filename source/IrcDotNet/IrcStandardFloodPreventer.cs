using System;

namespace IrcDotNet
{
    /// <summary>
    ///     Represents a flood protector that throttles data sent by the client according to the standard rules implemented
    ///     by modern IRC servers.
    /// </summary>
    /// <remarks>
    ///     The principle is that no message may be sent by the client once the value of an internal counter has reached
    ///     the value of <see cref="MaxMessageBurst" />. The counter is incremented every time a message is sent, and
    ///     decremented by one every duration of <see cref="CounterPeriod" />. Hence, messages may be sent immediately in
    ///     bursts so long as the high rate is not sustained, else a delay is introduced between the sending of
    ///     successive messages, such that the data.
    /// </remarks>
    /// <threadsafety static="true" instance="false" />
    public class IrcStandardFloodPreventer : IIrcFloodPreventer
    {
        private const int ticksPerMillisecond = 10000;

        // Period between each decrement of counter, in milliseconds.

        // Absolute time of last counter decrement, in milliseconds.
        private long lastCounterDecrementTime;

        // Maximum number of messages that can be sent in burst.

        // Number of messages sent within current burst.
        private int messageCounter;

        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcStandardFloodPreventer" /> class.
        /// </summary>
        /// <param name="maxMessageBurst">The maximum number of messages that can be sent in a burst.</param>
        /// <param name="counterPeriod">
        ///     The number of milliseconds between each decrement of the message counter.
        /// </param>
        public IrcStandardFloodPreventer(int maxMessageBurst, long counterPeriod)
        {
            MaxMessageBurst = maxMessageBurst;
            CounterPeriod = counterPeriod;

            messageCounter = 0;
            lastCounterDecrementTime = 0;
        }

        /// <summary>
        ///     Gets the maximum message number of messages that can be sent in a burst.
        /// </summary>
        /// <value>The maximum message number of messages that can be sent in a burst..</value>
        public int MaxMessageBurst { get; }

        /// <summary>
        ///     Gets the number of milliseconds between each decrement of the message counter.
        /// </summary>
        /// <value>The period of the counter, in milliseconds.</value>
        public long CounterPeriod { get; }

        #region IIrcFloodPreventer Members

        /// <inheritdoc />
        public long GetSendDelay()
        {
            // Subtract however many counter periods have elapsed since last decrement of counter.
            var currentTime = DateTime.Now.Ticks/ticksPerMillisecond;
            var elapsedMilliseconds = currentTime - lastCounterDecrementTime;
            var tempMessageCounter = Math.Max(0L, messageCounter - elapsedMilliseconds/CounterPeriod);
            messageCounter = tempMessageCounter > int.MaxValue ? int.MaxValue : (int) tempMessageCounter;

            // Update time of last decrement of counter to theoretical time of decrement.
            lastCounterDecrementTime = currentTime - elapsedMilliseconds%CounterPeriod;

            // Return time until next message can be sent.
            return Math.Max((messageCounter - MaxMessageBurst)*CounterPeriod - elapsedMilliseconds, 0);
        }

        /// <inheritdoc />
        public void HandleMessageSent()
        {
            // Increment message count.
            messageCounter++;
        }

        #endregion
    }
}