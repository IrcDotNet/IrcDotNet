// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Logger.cs" company="Yet another App Factory">
//   @ Matthias Dittrich
// </copyright>
// <summary>
//   The trace activity.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Yaaf.Utils.Logging
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;

    using Yaaf.Utils.Extensions;
    using Yaaf.Utils.Helper;

    /// <summary>
    /// The trace activity.
    /// </summary>
    public class TraceActivity
    {
        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceActivity"/> class.
        /// </summary>
        /// <param name="activity">
        /// The activity.
        /// </param>
        /// <param name="switchInfo">
        /// </param>
        public TraceActivity(Guid activity, string switchInfo)
        {
            this.Activity = activity;
            this.SwitchInfo = switchInfo;
        }

        #endregion

        #region Properties

        /// <summary>
        ///   Gets Activity.
        /// </summary>
        internal Guid Activity { get; private set; }

        /// <summary>
        ///   Gets or sets SwitchInfo.
        /// </summary>
        internal string SwitchInfo { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// The equals.
        /// </summary>
        /// <param name="other">
        /// The other.
        /// </param>
        /// <returns>
        /// The equals.
        /// </returns>
        public bool Equals(TraceActivity other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other.Activity.Equals(this.Activity);
        }

        /// <summary>
        /// The equals.
        /// </summary>
        /// <param name="obj">
        /// The obj.
        /// </param>
        /// <returns>
        /// The equals.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != typeof(TraceActivity))
            {
                return false;
            }

            return this.Equals((TraceActivity)obj);
        }

        /// <summary>
        /// The get hash code.
        /// </summary>
        /// <returns>
        /// The get hash code.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return this.Activity.GetHashCode() * 397;
            }
        }

        #endregion
    }

    /// <summary>
    /// A logger helper
    ///   use this class to log your things
    /// </summary>
    /// <remarks>
    /// Use this to add a global listener
    ///   <![CDATA[
    /// <configuration>
    ///     <system.diagnostics>
    ///        <trace autoflush="false" indentsize="4">
    ///            <listeners>
    ///                <add name="myListener" type="System.Diagnostics.TextWriterTraceListener" initializeData="logfiles\global.log"/>
    ///            </listeners>
    ///        </trace>
    ///    </system.diagnostics>
    /// </configuration>
    /// ]]>
    ///   or use this to add a assembly specific listener
    ///   <![CDATA[
    /// <configuration>
    ///     <system.diagnostics>
    ///        <sources>
    ///            <source name="AssemblyName" switchValue="All">
    ///                <listeners>
    ///                    <add name="console" type="System.Diagnostics.ConsoleTraceListener"/>
    ///                    <add name="myListener"
    ///                         type="System.Diagnostics.TextWriterTraceListener"
    ///                         initializeData="logfiles\AssemblyName.log"/>
    ///                </listeners>
    ///            </source>
    ///        </sources>
    ///    </system.diagnostics>
    /// </configuration>
    /// ]]>
    /// </remarks>
    public static class Logger
    {
        #region Constants and Fields

        /// <summary>
        ///   The counter (to indicate the nested method size (callstack)).
        /// </summary>
        private static readonly ThreadStaticField<int> Counter = new ThreadStaticField<int>(() => -1);

        /// <summary>
        ///   The global Trace source
        /// </summary>
        private static readonly TraceSource GlobalTrace = new TraceSource("global");

        /// <summary>
        ///   The thread activity stack.
        /// </summary>
        private static readonly ThreadStaticField<Stack<Guid>> ThreadActivityStack =
            new ThreadStaticField<Stack<Guid>>(CreateFirstActivityStack);

        /// <summary>
        ///   The top thread activity.
        /// </summary>
        private static readonly ThreadStaticField<Guid> TopThreadActivity =
            new ThreadStaticField<Guid>(CreateActivity);

        /// <summary>
        ///   The trace list, to enable Assembly seperated Logging.
        /// </summary>
        private static readonly Dictionary<Assembly, TraceSource> TraceList = new Dictionary<Assembly, TraceSource>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Indicates that an Action has started.
        ///   Consider using TraceAction instead.
        /// </summary>
        /// <param name="actionName">
        /// The action name.
        /// </param>
        [Conditional("TRACE")]
        public static void BeginAction(string actionName)
        {
            Assembly calling = Assembly.GetCallingAssembly();
            string line = string.Format("Action {0}", actionName);
            WriteLinePrivate(calling, line, TraceEventType.Start);
        }

        /// <summary>
        /// Indicates that an Method has started.
        ///   Consider using TraceMethod instead.
        /// </summary>
        /// <param name="parameters">
        /// The parameters.
        /// </param>
        [Conditional("TRACE")]
        public static void BeginMethod(params object[] parameters)
        {
            Assembly calling = Assembly.GetCallingAssembly();
            var trace = new StackTrace(1);
            StackFrame frame = trace.GetFrame(0);
            MethodBase method = frame.GetMethod();
            BeginMethodPrivate(calling, method, parameters);
        }

        /// <summary>
        /// Creates an activity and links the current point in the Tracing with this activity
        ///   use this if you start an async process so you can jump right to this activity
        ///   (This overload is provided so that string.Format has not to be executed when TRACE is disabled)
        /// </summary>
        /// <param name="switchInfoFormat">
        /// the switch info format string
        /// </param>
        /// <param name="args">
        /// the switch info format string arguments
        /// </param>
        /// <returns>
        /// the activity to use with TraceActivity later on
        /// </returns>
        public static TraceActivity CreateAndLinkActivity(string switchInfoFormat, params object[] args)
        {
#if TRACE
            var calling = Assembly.GetCallingAssembly();
            return CreateAndLinkActivity(calling, string.Format(switchInfoFormat, args));
#else
            return null;
#endif
        }

        /// <summary>
        /// Creates an activity and links the current point in the Tracing with this activity
        ///   use this if you start an async process so you can jump right to this activity
        /// </summary>
        /// <param name="switchInfo">
        /// the switch info
        /// </param>
        /// <returns>
        /// the activity to use with TraceActivity later on
        /// </returns>
        public static TraceActivity CreateAndLinkActivity(string switchInfo)
        {
#if TRACE
            var calling = Assembly.GetCallingAssembly();
            return CreateAndLinkActivity(calling, switchInfo);
#else
            return null;
#endif
        }

        /// <summary>
        /// Indicates that an Action has been performed.
        ///   Consider using TraceAction instead.
        /// </summary>
        /// <param name="actionName">
        /// The action name.
        /// </param>
        [Conditional("TRACE")]
        public static void EndAction(string actionName)
        {
            Assembly calling = Assembly.GetCallingAssembly();
            string line = string.Format("Action {0}", actionName);
            WriteLinePrivate(calling, line, TraceEventType.Stop);
        }

        /// <summary>
        /// Indicates that an Method has finished.
        ///   Consider using TraceMethod instead.
        /// </summary>
        [Conditional("TRACE")]
        public static void EndMethod()
        {
            Assembly calling = Assembly.GetCallingAssembly();
            var trace = new StackTrace(1);
            StackFrame frame = trace.GetFrame(0);
            MethodBase method = frame.GetMethod();
            string line = GetMethodLine(method);
            EndMethodPrivate(calling, line);
        }

        /// <summary>
        /// Sets the name of the Activity for the current Thread
        ///   NOTE: DOES ONLY WORK IF IT IS THE FIRST CALL IN A THREAD OF ANY LOGGER METHODS
        /// </summary>
        /// <param name="threadInfo">
        /// The activity name
        /// </param>
        [Conditional("TRACE")]
        public static void SetTopThreadActivity(string threadInfo)
        {
            var calling = Assembly.GetCallingAssembly();
            SetTopThreadActivity(calling, threadInfo);
        }

        /// <summary>
        /// Sets the name of the Activity for the current Thread
        ///   NOTE: DOES ONLY WORK IF IT IS THE FIRST CALL IN A THREAD OF ANY LOGGER METHODS
        /// </summary>
        /// <param name="threadInfoFormat">
        /// The activity name
        /// </param>
        /// <param name="args">
        /// The parameter to replace in threadInfoFormat
        /// </param>
        [Conditional("TRACE")]
        public static void SetTopThreadActivity(string threadInfoFormat, params object[] args)
        {
            var calling = Assembly.GetCallingAssembly();
            SetTopThreadActivity(calling, string.Format(threadInfoFormat, args));
        }

        /// <summary>
        /// Traces an action from start to finish see example for an use example.
        ///   (This overload is provided so that string.Format has not to be executed when TRACE is disabled)
        /// </summary>
        /// <param name="actionNameFormat">
        /// The action Name Format.
        /// </param>
        /// <param name="args">
        /// The arguments for the format.
        /// </param>
        /// <example>
        /// <![CDATA[
        /// using(Logger.TraceAction("Sending Message to {0}", message))
        /// {
        ///     // Do my action here     
        /// } 
        /// ]]>
        /// </example>
        /// <returns>
        /// a trace helper object, when disposed signaling the Stop event
        /// </returns>
        public static IDisposable TraceAction(string actionNameFormat, params object[] args)
        {
#if TRACE
            Assembly calling = Assembly.GetCallingAssembly();
            return TraceAction(calling, "Action: " + string.Format(actionNameFormat, args));
#else
            return null;
#endif
        }

        /// <summary>
        /// Traces an action from start to finish see example for an use example.
        /// </summary>
        /// <param name="actionName">
        /// The action name.
        /// </param>
        /// <example>
        /// <![CDATA[
        /// using(Logger.TraceAction("MyAction"))
        /// {
        ///     // Do my action here     
        /// } 
        /// ]]>
        /// </example>
        /// <returns>
        /// a trace helper object, when disposed signaling the Stop event
        /// </returns>
        public static IDisposable TraceAction(string actionName)
        {
#if TRACE
            Assembly calling = Assembly.GetCallingAssembly();
            return TraceAction(calling, "Action: " + actionName);
#else
            return null;
#endif
        }

        /// <summary>
        /// Traces an async activity from start to finish, you get a TraceActivity object from CreateAndLinkActivity
        /// </summary>
        /// <param name="activity">
        /// The activity you want to trace.
        /// </param>
        /// <example>
        /// <![CDATA[
        /// using(Logger.TraceActivity(myActivity))
        /// {
        ///     // Do my async action here     
        /// } 
        /// ]]>
        /// </example>
        /// <returns>
        /// an object designed to be used in an using statement
        /// </returns>
        public static IDisposable TraceActivity(TraceActivity activity)
        {
#if TRACE
            var calling = Assembly.GetCallingAssembly();
            return TraceActivity(calling, activity.Activity, activity.SwitchInfo);
#else
            return null;
#endif
        }

        /// <summary>
        /// Allows you to trace an activity from start to end with an using block
        ///   (This overload is provided so String.Format has not to be executed when trace is not enabled)
        /// </summary>
        /// <param name="switchInfoFormat">
        /// The switch info format.
        /// </param>
        /// <param name="args">
        /// The arguments for the format.
        /// </param>
        /// <returns>
        /// an object to trace within an using block
        /// </returns>
        public static IDisposable TraceActivity(string switchInfoFormat, params object[] args)
        {
#if TRACE
            Assembly calling = Assembly.GetCallingAssembly();
            return TraceActivity(calling, CreateActivity(), string.Format(switchInfoFormat, args));
#else
            return null;
#endif
        }

        /// <summary>
        /// Traces a new Activity and adds some tracelines to track this activity
        /// </summary>
        /// <param name="switchInfo">
        /// The switch info. (You will see this when you click on the activity)
        /// </param>
        /// <returns>
        /// An object to trace the activity with a using block (null if Trace is deaktivated)
        /// </returns>
        public static IDisposable TraceActivity(string switchInfo)
        {
#if TRACE
            Assembly calling = Assembly.GetCallingAssembly();
            return TraceActivity(calling, CreateActivity(), switchInfo);
#else
            return null;
#endif
        }

        /// <summary>
        /// Traces an method from start to finish see example for an use example.
        /// </summary>
        /// <param name="parameters">
        /// (Optional) the parameter to trace.
        /// </param>
        /// <example>
        /// <![CDATA[
        /// public static bool MyTracedMethod(string param1, int param2)
        /// {
        ///     using(Logger.TraceMethod(param1, param2))
        ///     {
        ///         if (param2 == 0) return true; 
        ///         else return param1 == "Hide";
        ///     }
        /// }
        /// ]]>
        /// </example>
        /// <returns>
        /// a trace helper object, when disposed signaling the Stop event
        /// </returns>
        public static IDisposable TraceMethod(params object[] parameters)
        {
            //!++ Note: There is a better solution in returning a object on 
            //!++ which "Activate" must be called (which is Conditional)

#if TRACE
            Assembly calling = Assembly.GetCallingAssembly();
            var trace = new StackTrace(1);
            StackFrame frame = trace.GetFrame(0);
            MethodBase method = frame.GetMethod();
            string line = BeginMethodPrivate(calling, method, parameters);
            return new DoAfterUsing(() => EndMethodPrivate(calling, line));
#else
            return null;
#endif
        }

        /// <summary>
        /// Writes a given line to the event listeners
        /// </summary>
        /// <param name="format">
        /// The format.
        /// </param>
        /// <param name="type">
        /// The type of the message.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        [Conditional("TRACE")]
        public static void WriteLine(
            string format, TraceEventType type = TraceEventType.Information, params object[] args)
        {
            WriteLinePrivate(Assembly.GetCallingAssembly(), string.Format(format, args), type);
        }

        public static TraceSource TraceSource
        {
            get
            {
                return GetTraceSourceFromAssembly(Assembly.GetCallingAssembly());
            }
        }

        public static TraceSource GlobalTraceSource
        {
            get
            {
                return GlobalTrace;
            }
        }

        ///// <summary>
        ///// Writes a given line to the event listeners and additionally to the console
        ///// </summary>
        ///// <param name="format">
        ///// The format.
        ///// </param>
        ///// <param name="type">
        ///// The type of the message.
        ///// </param>
        ///// <param name="args">
        ///// The args.
        ///// </param>
        //public static void WriteLineConsole(
        //    string format, TraceEventType type = TraceEventType.Information, params object[] args)
        //{
        //    WriteLinePrivateConsole(Assembly.GetCallingAssembly(), format, type, args);
        //    Console.WriteLine();
        //}

        //[Conditional("TRACE")]
        //private static void WriteLinePrivateConsole(
        //    Assembly calling, string format, TraceEventType type = TraceEventType.Information, params object[] args)
        //{
        //    WriteLinePrivate(calling, string.Format(format, args), type);
        //}

        #endregion

        #region Methods

        /// <summary>
        /// private little helper to begin tracing a method
        /// </summary>
        /// <param name="calling">
        /// the calling assembly
        /// </param>
        /// <param name="method">
        /// the calling method
        /// </param>
        /// <param name="parameters">
        /// the given parameters
        /// </param>
        /// <returns>
        /// the method line
        /// </returns>
        private static string BeginMethodPrivate(Assembly calling, MethodBase method, params object[] parameters)
        {
            string line = GetMethodLine(method);

            CheckThread(calling);

            Counter.Value++;

            WriteLinePrivate(calling, line, TraceEventType.Start);
            if (parameters.Length > 0)
            {
                var paramLine = parameters.Select(p => ParameterToString(p))
#if NET2
                    .ToArray()
#endif
;
                string advancedLine = "-Parameter Information- \r\n\tParameter: "
                                      + string.Join("\r\n\tParameter: ", paramLine);
                WriteLinePrivate(calling, advancedLine, TraceEventType.Verbose);
            }

            return line;
        }

        /// <summary>
        /// The check thread.
        /// </summary>
        /// <param name="calling">
        /// The calling.
        /// </param>
        private static void CheckThread(Assembly calling)
        {
            if (Counter.Value == -1)
            {
                InitializeUnknownThread(calling);
            }

            Trace.CorrelationManager.ActivityId = ThreadActivityStack.Value.Peek();
        }

        /// <summary>
        /// The create activity.
        /// </summary>
        /// <returns>
        /// </returns>
        private static Guid CreateActivity()
        {
            return Guid.NewGuid();
        }

        /// <summary>
        /// Creates an activity and links the current point in the Tracing with this activity
        ///   use this if you start an async process so you can jump right to this activity
        /// </summary>
        /// <param name="calling">
        /// The calling assembly.
        /// </param>
        /// <param name="switchInfo">
        /// The switch Info.
        /// </param>
        private static TraceActivity CreateAndLinkActivity(Assembly calling, string switchInfo)
        {
            var localTrace = GetTraceSourceFromAssembly(calling);
            var activity = new TraceActivity(CreateActivity(), switchInfo);
            SwitchToActivity(activity.Activity, activity.SwitchInfo, localTrace);
            return activity;
        }

        /// <summary>
        /// The create first activity stack.
        /// </summary>
        /// <returns>
        /// </returns>
        private static Stack<Guid> CreateFirstActivityStack()
        {
            var stack = new Stack<Guid>();
            stack.Push(TopThreadActivity.Value);
            return stack;
        }

        /// <summary>
        /// The end method private.
        /// </summary>
        /// <param name="calling">
        /// The calling.
        /// </param>
        /// <param name="line">
        /// The line.
        /// </param>
        private static void EndMethodPrivate(Assembly calling, string line)
        {
            WriteLinePrivate(calling, line, TraceEventType.Stop);
            Counter.Value--;
        }

        /// <summary>
        /// The escape values.
        /// </summary>
        /// <param name="toEscape">
        /// The to escape.
        /// </param>
        /// <returns>
        /// The escape values.
        /// </returns>
        private static string EscapeValues(IEnumerable<string> toEscape)
        {
            var escaped = "{" + string.Join(
                ";", toEscape.Select(s => s.Replace("_", "__").Replace(";", "_;"))
#if NET2
                .ToArray()
#endif
) + "}";
            return escaped;
        }

        /// <summary>
        /// The get local trace from assembly.
        /// </summary>
        /// <param name="assembly">
        /// The calling.
        /// </param>
        /// <returns>
        /// </returns>
        public static TraceSource GetTraceSourceFromAssembly(Assembly assembly)
        {
            string assemblyName = assembly.GetName().Name;
            return TraceList.TryGetOrCreateValue(() => new TraceSource(assemblyName), assembly);
        }

        /// <summary>
        /// Gets the method line from the given method
        /// </summary>
        /// <param name="method">
        /// the method
        /// </param>
        /// <returns>
        /// the method line
        /// </returns>
        private static string GetMethodLine(MethodBase method)
        {
            return string.Format("Method {0}.{1}", method.DeclaringType.Name, method.Name);
        }

        /// <summary>
        /// The initialize unknown thread.
        /// </summary>
        /// <param name="calling">
        /// The calling.
        /// </param>
        private static void InitializeUnknownThread(Assembly calling)
        {
            var info = string.Format(
                "Thread: {1}, Id: {0}", Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.Name);
            SetTopThreadActivity(calling, info);
        }

        /// <summary>
        /// The parameter to string.
        /// </summary>
        /// <param name="p">
        /// The p.
        /// </param>
        /// <returns>
        /// The parameter to string.
        /// </returns>
        private static string ParameterToString(object p)
        {
            if (p == null)
            {
                return "{NULL}";
            }

            var ienumerable = p as IEnumerable;
            if (ienumerable != null && !(ienumerable is string))
            {
                var values = ienumerable.Cast<object>().Select(ParameterToString);
                return ("{" + EscapeValues(values) + "}").Replace("\n", "\r\n\t");
            }

            return p.ToString().Replace("\n", "\r\n\t");
        }

        /// <summary>
        /// The set top thread activity.
        /// </summary>
        /// <param name="calling">
        /// The calling.
        /// </param>
        /// <param name="switchInfo">
        /// The switch info.
        /// </param>
        private static void SetTopThreadActivity(Assembly calling, string switchInfo)
        {
            if (Counter.Value != -1)
            {
                // Already initialized
                return;
            }

            Guid activity = TopThreadActivity.Value;
            var localTrace = GetTraceSourceFromAssembly(calling);

            Counter.Value = 0;
            var oldId = Trace.CorrelationManager.ActivityId;
            if (oldId != Guid.Empty)
            {
                SwitchToActivity(activity, switchInfo, localTrace);
            }

            Trace.CorrelationManager.ActivityId = activity;
            using (TraceAction(calling, switchInfo))
            {
            }
        }

        /// <summary>
        /// The switch to activity.
        /// </summary>
        /// <param name="activityId">
        /// The activity id.
        /// </param>
        /// <param name="activityInfo">
        /// The activity info.
        /// </param>
        /// <param name="localTrace">
        /// The local trace.
        /// </param>
        private static void SwitchToActivity(Guid activityId, string activityInfo, TraceSource localTrace)
        {
            var activityMessage = string.IsNullOrEmpty(activityInfo) ? "Starting new Activity..." : activityInfo;
            // Trace.TraceInformation("ID: {0}, Message: {1}", activityId, activityMessage);
            GlobalTrace.TraceTransfer(Counter.Value, activityMessage, activityId);
            localTrace.TraceTransfer(Counter.Value, activityMessage, activityId);
        }

        /// <summary>
        /// The trace action.
        /// </summary>
        /// <param name="calling">
        /// The calling.
        /// </param>
        /// <param name="actionName">
        /// The action name.
        /// </param>
        /// <returns>
        /// </returns>
        private static IDisposable TraceAction(Assembly calling, string actionName)
        {
#if TRACE
            CheckThread(calling);
            string line = string.Format("{0}", actionName);
            Action after;

            Counter.Value++;

            WriteLinePrivate(calling, line, TraceEventType.Start);
            return new DoAfterUsing(
                after = () =>
                {
                    WriteLinePrivate(calling, line, TraceEventType.Stop);
                    Counter.Value--;
                });
#else
            return null;
#endif
        }

        /// <summary>
        /// The trace activity.
        /// </summary>
        /// <param name="calling">
        /// The calling.
        /// </param>
        /// <param name="activity">
        /// The activity.
        /// </param>
        /// <param name="switchInfo">
        /// The switch info.
        /// </param>
        /// <returns>
        /// </returns>
        private static IDisposable TraceActivity(Assembly calling, Guid activity, string switchInfo)
        {
            CheckThread(calling); // Maybe there are other activities

            var oldActivity = ThreadActivityStack.Value.Peek();

            // var oldId = Trace.CorrelationManager.ActivityId;
            if (oldActivity == activity)
            {
                Trace.CorrelationManager.ActivityId = oldActivity;
                return null; // do nothing
            }

            var localTrace = GetTraceSourceFromAssembly(calling);
            SwitchToActivity(activity, switchInfo, localTrace);
            Trace.CorrelationManager.ActivityId = activity;
            Counter.Value++;
            ThreadActivityStack.Value.Push(activity);
            using (TraceAction(calling, switchInfo))
            {
            }

            return new DoAfterUsing(
                () =>
                {
                    Counter.Value--;
                    ThreadActivityStack.Value.Pop();
                    var newActivity = ThreadActivityStack.Value.Peek();
                    SwitchToActivity(newActivity, switchInfo, localTrace);
                    Trace.CorrelationManager.ActivityId = newActivity;
                });
        }

        ///// <summary>
        ///// The unescape values.
        ///// </summary>
        ///// <param name="escaped">
        ///// The escaped.
        ///// </param>
        ///// <returns>
        ///// </returns>
        //private static IEnumerable<string> UnescapeValues(string escaped)
        //{
        //    escaped = escaped.Substring(1, escaped.Length - 2);
        //    var semicolonIndexes = escaped.AllIndexOf(";");
        //    var escapedSemicolonIndexes = escaped.AllIndexOf("\\;");
        //    var realSemicolonIndexes = semicolonIndexes.Except(escapedSemicolonIndexes);
        //    int last = 0;
        //    foreach (var realSemicolonIndex in realSemicolonIndexes)
        //    {
        //        yield return escaped.Substring(last, realSemicolonIndex).Replace("_;", ";").Replace("__", "_");
        //        last = realSemicolonIndex + 1;
        //    }
        //}

        /// <summary>
        /// Writes a given line to the event listeners
        /// </summary>
        /// <param name="calling">
        /// The calling.
        /// </param>
        /// <param name="lineToPrint">
        /// The line To Print.
        /// </param>
        /// <param name="type">
        /// The type.
        /// </param>
        private static void WriteLinePrivate(
            Assembly calling, string lineToPrint, TraceEventType type = TraceEventType.Information)
        {
            string assemblyName = calling.GetName().Name;
            string traceLineToPrint = assemblyName + ": " + lineToPrint;
            TraceSource localTrace = GetTraceSourceFromAssembly(calling);

            CheckThread(calling);

            switch (type)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    Trace.TraceError(traceLineToPrint);
                    break;
                case TraceEventType.Warning:
                    Trace.TraceWarning(traceLineToPrint);
                    break;
                case TraceEventType.Information:
                    Trace.TraceInformation(traceLineToPrint);
                    break;
                case TraceEventType.Verbose:
                case TraceEventType.Start:
                case TraceEventType.Stop:
                case TraceEventType.Suspend:
                case TraceEventType.Resume:
                case TraceEventType.Transfer:
                    //Trace.TraceInformation(traceLineToPrint);
                    break;
                default:
                    Trace.TraceError("Could not translate the type: " + type + "!!!");
                    Trace.Flush();
                    throw new ArgumentOutOfRangeException("type");
            }

            Trace.Flush();

            localTrace.TraceEvent(type, Counter.Value, lineToPrint);
            localTrace.Flush();

            GlobalTrace.TraceEvent(type, Counter.Value, lineToPrint);
            GlobalTrace.Flush();
        }

        #endregion
    }

    /// <summary>
    /// Allowes advanced usage of the using statement
    /// </summary>
    public class DoAfterUsing : IDisposable
    {
        #region Constants and Fields

        /// <summary>
        ///   the action.
        /// </summary>
        private readonly Action action;

        /// <summary>
        ///   The disposed.
        /// </summary>
        private bool disposed;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DoAfterUsing"/> class.
        /// </summary>
        /// <param name="action">
        /// the action.
        /// </param>
        public DoAfterUsing(Action action = null)
        {
            this.action = action;
        }

        #endregion

        #region Implemented Interfaces

        #region IDisposable

        /// <summary>
        /// The dispose.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                if (this.action != null)
                {
                    this.action();
                }

                this.disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        #endregion

        #endregion
    }
}
