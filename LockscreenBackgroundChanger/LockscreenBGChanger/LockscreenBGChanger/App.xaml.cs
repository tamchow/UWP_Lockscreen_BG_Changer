﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.System.UserProfile;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace LockscreenBGChanger
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App
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
        protected override void OnLaunched(LaunchActivatedEventArgs e)
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
        }
        protected override async void OnActivated(IActivatedEventArgs e)
        {
            if (e.Kind != ActivationKind.Protocol) return;
            var pEArgs = e as ProtocolActivatedEventArgs;
            if (pEArgs == null) return;
            Window.Current.Activate();
            var uri = pEArgs.Uri.ToString();
            var argument = uri.Substring(uri.IndexOf(':') + 1);
            if (string.IsNullOrWhiteSpace(argument))
            {
                await StoreWallpaperAsync();
                Current.Exit();
                //ShowMessage("Lockscreen wallpaper retrieved", true);
            }
            else
            {
                var success = await SetWallpaperAsync(argument);
                Current.Exit();
                //ShowMessage(success ? "Image set" : "Image not set", true);
            }
        }

        private const string Acknowledgement = "OK";
        private static async void ShowMessage(string message, bool exit = false)
        {
            var msg = new MessageDialog(message);
            if(exit) msg.Commands.Add(new UICommand(Acknowledgement, ExitHandler));
            await msg.ShowAsync();
        }
        private static void ExitHandler(IUICommand commandLabel)
        {
            var actions = commandLabel.Label;
            switch (actions)
            {
                case Acknowledgement:
                    Current.Exit();
                    break;
            }
            
        }
        static async Task<bool> SetWallpaperAsync(string fileName)
        {
            var source = await KnownFolders.PicturesLibrary.GetFileAsync(fileName);
            //generate new file name to avoid collisions
            var newFileName = $"{Guid.NewGuid()}{source.FileType}";
            if (!UserProfilePersonalizationSettings.IsSupported()) return false;
            var profileSettings = UserProfilePersonalizationSettings.Current;
            //Copy the file to Current.LocalFolder because TrySetLockScreenImageAsync
            //Will fail if the image isn't located there 
            await source.CopyAsync(ApplicationData.Current.LocalFolder, newFileName, NameCollisionOption.GenerateUniqueName);
            var file = await ApplicationData.Current.LocalFolder.GetFileAsync(newFileName);
            if (!await profileSettings.TrySetLockScreenImageAsync(file)) return false;
            await source.RenameAsync($"{source.Name}{CompletedExtension}");
            return true;
        }
        static async Task<bool> StoreWallpaperAsync()
        {
            if (!UserProfilePersonalizationSettings.IsSupported()) return false;
            
            using (var source = LockScreen.GetImageStream().AsStreamForRead())
            {
                string fileExtension;
                try
                {
                    fileExtension = Path.GetExtension(LockScreen.OriginalImageFile.LocalPath);
                }
                catch (Exception)
                {
                    fileExtension = DefaultFileExtension;
                }
                if (string.IsNullOrWhiteSpace(fileExtension))
                {
                    fileExtension = DefaultFileExtension;
                }
                var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
                var folders = picturesLibrary.Folders.Select(folder => folder.Path).ToList();
                var destinationFolders = folders.Where(folder => folder.StartsWith("C")).ToList();
                var destinationFolder = await StorageFolder.GetFolderFromPathAsync(!destinationFolders.Any() ? destinationFolders.First() : folders.First());
                var destinationFile = await destinationFolder.CreateFileAsync($"{ResultImageName}{fileExtension}{CompletedExtension}", CreationCollisionOption.ReplaceExisting);
                using (var writeStream = await destinationFile.OpenStreamForWriteAsync())
                {
                    await source.CopyToAsync(writeStream);
                }
            }
            return true;
        }

        private const string DefaultFileExtension = ".jpg";
        private const string ResultImageName = "lockscreen_bg";
        private const string CompletedExtension = ".done";
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
