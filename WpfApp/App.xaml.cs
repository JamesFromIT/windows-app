﻿using EduroamConfigure;
using Newtonsoft.Json;
using SingleInstanceApp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;

using LetsWifi = EduroamConfigure.LetsWifi;

namespace WpfApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstance
    {
        private const string SingleInstanceUid = "7aab8621-df45-4eb5-85c3-c70c06e8a22e";

#if RUN_PERSISTENT
        public bool StartHiddenInTray { get; private set; } = false;
#endif

        [STAThread]
        public static void Main(string[] args)
        {
            if (SingleInstance<App>.InitializeAsFirstInstance(SingleInstanceUid))
            {
                // making it this far means that we are THE single instance
                try
                {
                    if (PreGuiCommandLineArgs(args))
                        return;
                    var app = new App();
                    app.InitializeComponent();
                    app.Run();
                }
                finally
                {
                    SingleInstance<App>.Cleanup();
                }
            }
        }

        /// <summary>
        /// Handles command line args not related to wpf behaviour
        /// </summary>
        /// <returns>true if startup is to be aborted</returns>
        static bool PreGuiCommandLineArgs(string[] args)
        {
            // shorthand
            bool contains(string check) =>
                args.Any(param => string.Equals(param, check, StringComparison.InvariantCultureIgnoreCase));

            if (contains("/?")
                || contains("/help"))
            {
                ShowHelpText();
                return true;
            }

            if (contains("/install")) // todo: MessageBox.Show(yes/no)
            {
                Installer.EnsureIsInstalled();
                return true;
            }

            if (contains("/uninstall"))
            {
                PromptAndUninstallSelf(success =>
                    {
                        Environment.Exit(0);
                        return success;
                    }
                );
                return true;
            }

            if (contains("/refresh")
                || contains("/force-refresh") || contains("/refresh-force"))
            {
                RefreshInstalledProfile(s => false, force: contains("/refresh-force"));
                return true;
            }

            return contains("/close")
#if !RUN_PERSISTENT
                // Just quit when being started with /background
                || contains("/background")
#endif
                ;
        }

#if RUN_PERSISTENT
        /// <summary>
        /// WPF startup handler, first instance runs this.
        /// Handles command line args related to wpf behaviour
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // shorthand
            bool contains(string check) =>
                e?.Args.Any(param => string.Equals(param, check, StringComparison.InvariantCultureIgnoreCase)) ?? false;

            if (contains("/refresh")
                || contains("/refresh-force"))
            {
                // TODO: check if ((MainWindow)MainWindow).ShowNotification works at this point during startup
                RefreshInstalledProfile(s => ((MainWindow)MainWindow).ShowNotification(s), force: contains("/refresh-force"));
            }

            if (contains("/background")
                || contains("/refresh")
                || contains("/refresh-force"))
            {
                Debug.WriteLine("Starting hidden in tray");
                StartHiddenInTray = true;
            }

            base.OnStartup(e);
        }
#endif

        /// <summary>
        /// Signal handler from secondary instances.
        /// Handles command line arguments sent from second instance.
        /// </summary>
        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            // shorthand
            bool contains(string check) =>
                args.Skip(1).Any(param => string.Equals(param, check, StringComparison.InvariantCultureIgnoreCase));

            Debug.WriteLine("Got external cli args: {0} from {1}",
                JsonConvert.SerializeObject(args.Skip(1).ToList()), args.FirstOrDefault());

            bool activateMainWindow = true;

            if (contains("/?")
                || contains("/help"))
            {
                activateMainWindow = false;
                ShowHelpText();
            }

            if (contains("/close"))
            {
                activateMainWindow = false;
                ((MainWindow)MainWindow).Shutdown();
            }

            if (contains("/refresh")
                || contains("/refresh-force"))
            {
                activateMainWindow = false;
#if RUN_PERSISTENT
                RefreshInstalledProfile(s => ((MainWindow)MainWindow).ShowNotification(s), force: contains("/refresh-force"));
#else
                RefreshInstalledProfile(s => false, force: contains("/refresh-force"));
                ((MainWindow)MainWindow).Shutdown();
#endif
            }

            if (contains("/uninstall"))
            {
                activateMainWindow = false;
                PromptAndUninstallSelf(success =>
                    {
                        ((MainWindow)MainWindow).Shutdown();
                        return "foo";
                    }
                );
            }

            // TODO: this should be made into a method in MainWindow.
            if (activateMainWindow)
            {
                var window = ((MainWindow)MainWindow);
                window.Show();
                if (window.WindowState == WindowState.Minimized)
                    window.WindowState = WindowState.Normal;
                window.Activate();
            }

            return false; // dont have the library show the window for us
        }

        private static void RefreshInstalledProfile(Func<string, bool> f, bool force)
        {
            _ = LetsWifi.RefreshAndInstallEapConfig(force) switch
			{
				LetsWifi.RefreshResponse.UpdatedEapXml => true, // fine

				LetsWifi.RefreshResponse.Success => true, // nice!
				LetsWifi.RefreshResponse.StillValid => true, // no work needed
				LetsWifi.RefreshResponse.NotRefreshable => false, // ignore, since we currently always schedule the task in windows

#if RUN_PERSISTENT
				// TODO: show tray icon when not running persistent, but one of these three happens?
				// TODO: reword these into something better
				LetsWifi.RefreshResponse.NewRootCaRequired => f("Your geteduroam setup should be reconfigured"),
				LetsWifi.RefreshResponse.AccessDenied => f("Your geteduroam setup should be reconfigured"),
				LetsWifi.RefreshResponse.Failed => f("Your geteduroam setup should be reconfigured"),
#endif

#if DEBUG
                _ => throw new NotImplementedException(nameof(RefreshInstalledProfile))
#else
                _ => false
#endif
            };
        }

        private static void PromptAndUninstallSelf<T>(Func<bool, T> shutdown)
        {

            var choice = MessageBox.Show(
                "You are currently in the process of completly uninstalling geteduroam.\n" +
                (CertificateStore.AnyRootCaInstalledByUs()
                    ? "This means uninstalling all the trusted root certificates installed by this application.\n\n"
                    : "\n") +
                "Are you sure you want to continue?",
                caption: "geteduroam",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (choice != MessageBoxResult.Yes)
            {
                MessageBox.Show(
                    "geteduroam has not been uninstalled.",
                    caption: "geteduroam",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            Installer.ExitAndUninstallSelf(
                success =>
                {
                    // we cannot show a normal message box on success,
                    // since we've dispatched a job to delete the running binary at this point
                    // but we can spawn a PowerShell that will show the success message
                    if (success) {
                        var extinguishMe = new ProcessStartInfo
                        {
                            FileName = "mshta",
                            Arguments = "vbscript:Execute(\"msgbox \"\"The application and its configuration have been uninstalled\"\", 0, \"\"Uninstall geteduroam\"\":close\")",
                            WindowStyle = ProcessWindowStyle.Normal, // Shows a console in the taskbar, but it's hidden
                            CreateNoWindow = true,
                            WorkingDirectory = "C:\\"
                        };
                        Process.Start(extinguishMe);
                    }
                    else
                    {
                        MessageBox.Show(
                        "geteduroam is not yet uninstalled! The uninstallation was aborted.",
                        caption: "Uninstall geteduroam",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    }
                    return shutdown(success);
                },
                doDeleteSelf: true);
        }

        public static void ShowHelpText()
            => MessageBox.Show(string.Join("\n", new List<string> {
                    "Supported CLI commands:",
                    "",
                    "    /? : ",
                    "            Show this help text",
                    "    /help : ",
                    "            Show this help text",
                    "    /install : ",
                    "            Install this binary to %USER%/AppData/Local",
                    "    /uninstall : ",
                    "            Uninstall this binary from %USER%/AppData/Local along",
                    "            with any configured data",
                    "    /background : ",
                    "            Start this application hidden to the tray",
                    "            (works only if run from install directory)",
                    "    /close : ",
                    "            Close the current running instance",
                    "    /refresh : ",
                    "            Refresh the user certificate using the refresh token.",
                    "            Is called automatically by a scheduled task in windows.",
                    "    /refresh-force : ",
                    "            Refresh the user certificate using the refresh token.",
                    "            Will refresh the profile even if the validity period",
                    "            of the current client certificate has more than a 3rd left",
                }), caption: "geteduroam");


        private static AssemblyName AssemblyName
        { get => Assembly.GetExecutingAssembly().GetName(); }

        // TODO: can we populate with from AssemblyName? in general, all mentions of "eduroam" and "geteduroam" should be configurable
        public static readonly SelfInstaller Installer = new SelfInstaller(
            applicationIdentifier: "geteduroam",
            applicationMetadata: new SelfInstaller.ApplicationMeta()
            {
                DisplayName = "geteduroam",  // [REQUIRED] ProductName
                Publisher = "Uninett AS",  // [REQUIRED] Manufacturer
                Version = AssemblyName.Version.ToString(),
                VersionMajor = AssemblyName.Version.Major.ToString(CultureInfo.InvariantCulture),
                VersionMinor = AssemblyName.Version.Minor.ToString(CultureInfo.InvariantCulture),
                HelpLink = null,  // ARPHELPLINK
                HelpTelephone = null,  // ARPHELPTELEPHONE
                InstallSource = null,  // SourceDir
                URLInfoAbout = null,  // ARPURLINFOABOUT
                URLUpdateInfo = null,  // ARPURLUPDATEINFO
                AuthorizedCDFPrefix = null,  // ARPAUTHORIZEDCDFPREFIX
                Comments = null,  // [NICE TO HAVE] ARPCOMMENTS. Comments provided to the Add or Remove Programs control panel.
                Contact = null,  // [NICE TO HAVE] ARPCONTACT. Contact provided to the Add or Remove Programs control panel.
                Language = null,  // ProductLanguage
                Readme = null,  // [NICE TO HAVE] ARPREADME. Readme provided to the Add or Remove Programs control panel.
                SettingsIdentifier = null,  // MSIARPSETTINGSIDENTIFIER. contains a semi-colon delimited list of the registry locations where the application stores a user's settings and preferences.
                NoRepair = true,
                NoModify = true,
            }
        );

    }
}
