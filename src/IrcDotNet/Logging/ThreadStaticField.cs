// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ThreadStaticField.cs" company="Yet another App Factory">
//   @ Matthias Dittrich
// </copyright>
// <summary>
//   Helperclass to make a thread threadsafe like [ThreadStatic] does
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Yaaf.Utils.Helper
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Helperclass to make a field threadsafe like [ThreadStatic] does
    /// </summary>
    /// <typeparam name="T">
    /// the type of the field
    /// </typeparam>
    /// <remarks>
    /// NOTE: add no logging (dependency of Logger)
    /// See http://msdn.microsoft.com/en-us/library/system.threadstaticattribute%28v=vs.90%29.aspx
    /// </remarks>
    public class ThreadStaticField<T>
    {
        #region Constants and Fields

        /// <summary>
        ///   The create field.
        /// </summary>
        private readonly Func<T> createField;

        /// <summary>
        ///   The field values.
        /// </summary>
        private readonly Dictionary<Thread, T> fieldValues = new Dictionary<Thread, T>();

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///   Initializes a new instance of the <see cref = "ThreadStaticField{T}" /> class.
        /// </summary>
        public ThreadStaticField()
        {
            this.createField = () => default(T);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadStaticField{T}"/> class.
        /// </summary>
        /// <param name="createField">
        /// The create field.
        /// </param>
        public ThreadStaticField(Func<T> createField)
        {
            this.createField = createField ?? (() => default(T));
        }

        #endregion

        #region Properties

        /// <summary>
        ///   Gets or sets Value.
        /// </summary>
        public T Value
        {
            get
            {
                Thread currentThread = Thread.CurrentThread;
                T toGet;
                lock (((ICollection)this.fieldValues).SyncRoot)
                {
                    if (!this.fieldValues.TryGetValue(currentThread, out toGet))
                    {
                        toGet = this.createField();
                        this.fieldValues.Add(currentThread, toGet);
                    }
                }

                return toGet;
            }

            set
            {

                Thread currentThread = Thread.CurrentThread;
                lock (((ICollection)this.fieldValues).SyncRoot)
                {
                    this.fieldValues[currentThread] = value;
                }
            }
        }

        #endregion
    }
}
