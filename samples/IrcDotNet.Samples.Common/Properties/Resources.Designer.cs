﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace IrcDotNet.Properties {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///    A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        internal Resources() {
        }
        
        /// <summary>
        ///    Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
#if NETSTANDARD1_5  || NETCOREAPP1_0
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("IrcDotNet.Properties.Resources", typeof(Resources).GetTypeInfo().Assembly);
#else
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("IrcDotNet.Properties.Resources", typeof(Resources).Assembly);
#endif
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///    Overrides the current thread's CurrentUICulture property for all
        ///    resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Bot does not have connection to any server matching `{0}&apos;..
        /// </summary>
        internal static string MessageBotNoConnection {
            get {
                return ResourceManager.GetString("MessageBotNoConnection", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The command `{0}&apos; does not take any parameters..
        /// </summary>
        internal static string MessageCommandTakesNoParams {
            get {
                return ResourceManager.GetString("MessageCommandTakesNoParams", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The command `{0}&apos; takes {1} parameter(s)..
        /// </summary>
        internal static string MessageCommandTakesXParams {
            get {
                return ResourceManager.GetString("MessageCommandTakesXParams", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The command `{0}&apos; takes between {1} and {2} parameters..
        /// </summary>
        internal static string MessageCommandTakesXToYParams {
            get {
                return ResourceManager.GetString("MessageCommandTakesXToYParams", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Not enough arguments were specified for the given command..
        /// </summary>
        internal static string MessageNotEnoughArgs {
            get {
                return ResourceManager.GetString("MessageNotEnoughArgs", resourceCulture);
            }
        }
    }
}
