using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using Microsoft.Silverlight.Testing;
using Microsoft.Silverlight.Testing.Harness;

namespace IrcDotNet.Tests
{
    public partial class App : Application
    {
        private const string messageUnhandledError = "Unhandled error in Silverlight test application.";

        public App()
            : base()
        {
            InitializeComponent();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            this.RootVisual = UnitTestSystem.CreateTestPage(CreateUnitTestSettings());
        }

        private void Application_Exit(object sender, EventArgs e)
        {
            //
        }

        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                Debug.Assert(false, e.ExceptionObject.Message);
            }
            else
            {
                e.Handled = true;
                if (App.Current.IsRunningOutOfBrowser)
                {
                    MessageBox.Show(string.Format("{0}\n{1}", messageUnhandledError, e.ExceptionObject.Message));
                }
                else
                {
                    Deployment.Current.Dispatcher.BeginInvoke(delegate { ReportErrorToBrowser(e); });
                }
            }
        }

        private void ReportErrorToBrowser(ApplicationUnhandledExceptionEventArgs e)
        {
            try
            {
                var errorMessage = e.ExceptionObject.Message + e.ExceptionObject.StackTrace;
                errorMessage = errorMessage.Replace('"', '\'').Replace("\r\n", "\\n");
                System.Windows.Browser.HtmlPage.Window.Eval(string.Format(
                    "throw new Error(\"{0}.\\n{1}\");", messageUnhandledError, errorMessage));
            }
            catch (Exception)
            {
            }
        }

        private UnitTestSettings CreateUnitTestSettings()
        {
            var settings = new UnitTestSettings();
            settings.TestHarness = new UnitTestHarness();
            settings.LogProviders.Add(new DebugOutputProvider());
            settings.LogProviders.Add(new VisualStudioLogProvider());
            settings.StartRunImmediately = true;
            settings.ShowTagExpressionEditor = false;
            settings.TestAssemblies.Add(Assembly.GetExecutingAssembly());
            settings.TagExpression = "Integration";
            
            return settings;
        }
    }
}
