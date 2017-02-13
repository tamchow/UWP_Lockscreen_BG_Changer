using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.UserProfile;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using static Windows.System.Launcher;

namespace LockscreenBGChnager
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private Frame _rootFrame;
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif
            _rootFrame = Window.Current.Content as Frame;
            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (_rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                _rootFrame = new Frame();

                _rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //nothing to do in our case
                }

                // Place the frame in the current Window
                Window.Current.Content = _rootFrame;
            }

            if (e.PrelaunchActivated) return;
            if (_rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                _rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }
            // Ensure the current window is active
            Window.Current.Activate();
            //await LaunchUriAsync(new Uri("lsbg:18000"));//debug
        }
        protected override async void OnActivated(IActivatedEventArgs e)
        {
            if (e.Kind != ActivationKind.Protocol) return;
            var pEArgs = e as ProtocolActivatedEventArgs;
            if (pEArgs == null) return;
            Window.Current.Activate();
            var uri = pEArgs.Uri.ToString();
            ShowMessage($"Activated via protocol, URI: {uri}, path = {uri.Substring(uri.IndexOf(':') + 1)}");
            var success = await SetWallpaperAsync(uri.Substring(uri.IndexOf(':')+1));
            ShowMessage(success?"Image set":"Image not set", true);
        }
        private static async void ShowMessage(string message, bool exit = false)
        {
            //Message Box.
            var msg = new MessageDialog(message);
            //Commands
            if(exit) msg.Commands.Add(new UICommand("Ok", ExitHandler));
            
            await msg.ShowAsync();
        }
        private static void ExitHandler(IUICommand commandLabel)
        {
            var actions = commandLabel.Label;
            switch (actions)
            {
                //Okay Button.
                case "Yes":
                    Current.Exit();
                    break;
            }
            
        }
        static async Task<bool> SetWallpaperAsync(string port)
        {
            var success = false;
            var networkStream = new StreamSocket();
            await networkStream.ConnectAsync(new HostName(IPAddress.Loopback.ToString()), port);
            //generate new file name to avoid colliaions
            var newFileName = $"{Guid.NewGuid()}";
            if (UserProfilePersonalizationSettings.IsSupported())
            {
                var profileSettings = UserProfilePersonalizationSettings.Current;
                //Copy the file to Current.LocalFolder because TrySetLockScreenImageAsync
                //Will fail if the image isn't located there 
                using (var readStream = networkStream.InputStream.AsStreamForRead())
                {
                    using (var writestream = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync(newFileName,
                        CreationCollisionOption.GenerateUniqueName))
                    {
                        await readStream.CopyToAsync(writestream);
                    }
                }
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(newFileName);
                success = await profileSettings.TrySetLockScreenImageAsync(file);
            }

            Debug.WriteLine($"Set wallpaper: {success}");
            return success;
        }
        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private static void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
