﻿//   CmisSync, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using CmisSync.Lib;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.ObjectModel;

using CmisSync.Lib.Cmis;
using CmisSync.Lib.Events;
using CmisSync.Auth;

using System.Windows.Forms;

#if __COCOA__
// using Edit = CmisSync.EditWizardController;
#endif

namespace CmisSync
{
    /// <summary>
    /// Platform-independant part of the main CmisSync controller.
    /// </summary>
    public abstract class ControllerBase : IActivityListener
    {
        /// <summary>
        /// Log.
        /// </summary>
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(ControllerBase));

        /// <summary>
        /// Whether it is the first time that CmisSync is being run.
        /// </summary>
        private bool firstRun;

        /// <summary>
        /// All the info about the CmisSync synchronized folder being created.
        /// </summary>
        private RepoInfo repoInfo;

        /// <summary>
        /// Whether the repositories have finished loading.
        /// </summary>
        public bool RepositoriesLoaded { get; private set; }

        /// <summary>
        /// List of the CmisSync synchronized folders.
        /// </summary>
        private List<RepoBase> repositories = new List<RepoBase>();

        /// <summary>
        /// Path where the CmisSync synchronized folders are by default.
        /// </summary>
        public string FoldersPath { get; private set; }

        /// <summary>
        /// Show setup window event.
        /// </summary>
        public event ShowSetupWindowEventHandler ShowSetupWindowEvent = delegate { };

        /// <summary>
        /// Show setup window event.
        /// </summary>
        public delegate void ShowSetupWindowEventHandler(PageType page_type);

        /// <summary>
        /// Show about window event.
        /// </summary>
        public event Action ShowAboutWindowEvent = delegate { };

        /// <summary>
        /// Folder list changed.
        /// </summary>
        public event Action FolderListChanged = delegate { };

        /// <summary>
        /// Called with status changes to idle.
        /// </summary>
        public event Action OnIdle = delegate { };

        /// <summary>
        /// Called with status changes to syncing.
        /// </summary>
        public event Action OnSyncing = delegate { };

        /// <summary>
        /// Called with status changes to error.
        /// </summary>
        public event Action<Tuple<string, Exception>> OnError = delegate { };

        /// <summary>
        /// Called with status changes to error resolved.
        /// </summary>
        public event Action OnErrorResolved = delegate { };

        /// <summary>
        /// Alert notification.
        /// </summary>
        public event AlertNotificationRaisedEventHandler AlertNotificationRaised = delegate { };

        /// <summary>
        /// Alert notification.
        /// </summary>
        public delegate void AlertNotificationRaisedEventHandler(string title, string message);

        /// <summary>
        /// Get the repositories configured in CmisSync.
        /// </summary>
        public RepoBase[] Repositories
        {
            get
            {
                lock (this.repo_lock)
                    return this.repositories.GetRange(0, this.repositories.Count).ToArray();
            }
        }

        /// <summary>
        /// Whether it is the first time that CmisSync is being run.
        /// </summary>
        public bool FirstRun
        {
            get
            {
                return firstRun;
            }
        }

        /// <summary>
        /// The list of synchronized folders.
        /// </summary>
        public List<string> Folders
        {
            get
            {
                List<string> folders = new List<string>();
                foreach (Config.SyncConfig.Folder f in ConfigManager.CurrentConfig.Folders)
                    folders.Add(f.DisplayName);
                folders.Sort();

                return folders;
            }
        }

        /// <summary>
        /// Add CmisSync to the list of programs to be started up when the user logs into Windows.
        /// </summary>
        public abstract void CreateStartupItem();

        /// <summary>
        /// Add CmisSync to the user's Windows Explorer bookmarks.
        /// </summary>
        public abstract void AddToBookmarks();

        /// <summary>
        /// Creates the CmisSync folder in the user's home folder.
        /// </summary>
        public abstract bool CreateCmisSyncFolder();

        /// <summary>
        /// Keeps track of whether a download or upload is going on, for display of the task bar animation.
        /// </summary>
        private IActivityListener activityListenerAggregator;


        /// <summary>
        /// A folder lock for the base directory.
        /// </summary>
        private FolderLock folderLock;

        /// <summary>
        /// Concurrency locks.
        /// </summary>
        private Object repo_lock = new Object();

        /// <summary>
        /// Constructor.
        /// </summary>
        public ControllerBase()
        {
            activityListenerAggregator = new ActivityListenerAggregator(this);
        }

        /// <summary>
        /// Initialize the controller.
        /// </summary>
        /// <param name="firstRun">Whether it is the first time that CmisSync is being run.</param>
        public virtual void Initialize(Boolean firstRun)
        {
            this.firstRun = firstRun;

            FoldersPath = ConfigManager.CurrentConfig.FoldersPath;

            // Create the CmisSync folder and add it to the bookmarks
            bool syncFolderCreated = CreateCmisSyncFolder();

            if (syncFolderCreated)
            {
                AddToBookmarks();
            }

            if (firstRun)
            {
                ConfigManager.CurrentConfig.Notifications = true;
            }

            folderLock = new FolderLock(FoldersPath);
        }

        /// <summary>
        /// Once the GUI has loaded, show setup window if it is the first run, or check the repositories.
        /// </summary>
        public void UIHasLoaded()
        {
            if (firstRun)
            {
                ShowSetupWindow(PageType.Setup);
            }

            Thread t = new Thread(() =>
            {
                CheckRepositories();
                RepositoriesLoaded = true;
                // Update GUI.
                FolderListChanged();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        /// <summary>
        /// Initialize (in the GUI and syncing mechanism) an existing CmisSync synchronized folder.
        /// </summary>
        /// <param name="repositoryInfo">Synchronized folder path</param>
        private void AddRepository(RepoInfo repositoryInfo)
        {
            RepoBase repo = null;
            repo = new CmisSync.Lib.Sync.CmisRepo(repositoryInfo, activityListenerAggregator);
            this.repositories.Add(repo);
            repo.Initialize();
        }

        /// <summary>
        /// Update settings for repository.
        /// </summary>
        public void UpdateRepositorySettings(string repoName, string password, int pollInterval, bool syncAtStartup)
        {
            foreach (RepoBase repoBase in this.repositories)
            {
                if (repoBase.Name == repoName)
                {
                    repoBase.UpdateSettings(password, pollInterval, syncAtStartup);
                    OnErrorResolved();
                    FolderListChanged();
                }
            }
        }

        /// <summary>
        /// Remove repository from sync.
        /// </summary>
        public void RemoveRepositoryFromSync(string reponame)
        {
            Config.SyncConfig.Folder f = ConfigManager.CurrentConfig.GetFolder(reponame);
            if (f != null)
            {
                RemoveRepository(f);
                ConfigManager.CurrentConfig.RemoveFolder(reponame);
                FolderListChanged();
            }
            else
            {
                Logger.Warn("Reponame \"" + reponame + "\" could not be found: Removing Repository failed");
            }
        }

        /// <summary>
        /// Run a sync manually.
        /// </summary>
        public void ManualSync(string reponame)
        {
            foreach (RepoBase aRepo in this.repositories)
            {
                if (aRepo.Name == reponame && aRepo.Status == SyncStatus.Idle)
                {

                    aRepo.ManualSync();
                    Logger.Debug("Requested to manually sync " + aRepo.Name);
                }
            }
        }

        /// <summary>
        /// Remove a synchronized folder from the CmisSync configuration.
        /// This happens after the user removes the folder.
        /// </summary>
        /// <param name="folder">The synchronized folder to remove</param>
        private void RemoveRepository(Config.SyncConfig.Folder folder)
        {
            foreach (RepoBase repo in this.repositories)
            {
                if (repo.LocalPath.Equals(folder.LocalPath))
                {
                    repo.CancelSync();
                    repo.Dispose();
                    this.repositories.Remove(repo);
                    Logger.Info("Removed Repository: " + repo.Name);
                    break;
                }
            }

            // Remove Cmis Database File
            string dbfilename = folder.DisplayName;
            dbfilename = dbfilename.Replace("\\", "_");
            dbfilename = dbfilename.Replace("/", "_");
            RemoveDatabase(dbfilename);
        }

        /// <summary>
        /// Remove the local database associated with a CmisSync synchronized folder.
        /// </summary>
        /// <param name="folder_path">The synchronized folder whose database is to be removed</param>
        private void RemoveDatabase(string folder_path)
        {
            string databasefile = Path.Combine(ConfigManager.CurrentConfig.ConfigPath, Path.GetFileName(folder_path) + ".cmissync");
            if (File.Exists(databasefile))
            {
                File.Delete(databasefile);
                Logger.Info("Removed database: " + databasefile);
            }
        }

        /// <summary>
        /// Pause or un-pause synchronization for a particular folder.
        /// </summary>
        /// <param name="repoName">the folder to pause/unpause</param>
        public void SuspendOrResumeRepositorySynchronization(string repoName)
        {
            lock (this.repo_lock)
            {
                //FIXME: why are we sospendig all repositories instead of the one passed?
                foreach (RepoBase aRepo in this.repositories)
                {
                    if (aRepo.Status != SyncStatus.Suspend)
                    {
                        SuspendRepositorySynchronization(repoName);
                    }
                    else
                    {
                        ResumeRepositorySynchronization(repoName);
                    }
                }
            }
        }

        /// <summary>
        /// Pause synchronization for a particular folder.
        /// </summary>
        /// <param name="repoName">the folder to pause</param>
        public void SuspendRepositorySynchronization(string repoName)
        {
            lock (this.repo_lock)
            {
                //FIXME: why are we sospendig all repositories instead of the one passed?
                foreach (RepoBase aRepo in this.repositories)
                {
                    if (aRepo.Status != SyncStatus.Suspend)
                    {
                        aRepo.Suspend();
                        Logger.Debug("Requested to suspend sync of repo " + aRepo.Name);
                    }
                }
            }
        }

        /// <summary>
        /// Un-pause synchronization for a particular folder.
        /// </summary>
        /// <param name="repoName">the folder to unpause</param>
        public void ResumeRepositorySynchronization(string repoName)
        {
            lock (this.repo_lock)
            {
                //FIXME: why are we sospendig all repositories instead of the one passed?
                foreach (RepoBase aRepo in this.repositories)
                {
                    if (aRepo.Status == SyncStatus.Suspend)
                    {
                        aRepo.Resume();
                        Logger.Debug("Requested to resume sync of repo " + aRepo.Name);
                    }
                }
            }
        }

        /// <summary>
        /// Check the configured CmisSync synchronized folders.
        /// </summary>
        private void CheckRepositories()
        {
            lock (this.repo_lock)
            {
                Queue<Config.SyncConfig.Folder> missingFolders = new Queue<Config.SyncConfig.Folder>();
                foreach (Config.SyncConfig.Folder f in ConfigManager.CurrentConfig.Folders)
                {
                    string folder_path = f.LocalPath;

                    if (!Directory.Exists(folder_path))
                    {
                        // If folder has been deleted, ask the user what to do.
                        Logger.Info("ControllerBase | Found missing folder '" + f.DisplayName + "'");
                        missingFolders.Enqueue(f);
                    }
                    else
                    {
                        AddRepository(f.GetRepoInfo());
                    }
                }

                while (missingFolders.Count != 0)
                {
                    // Disabled because incompatible wit Mac OS X: handleMissingSyncFolder(missingFolders.Dequeue());
                }

                ConfigManager.CurrentConfig.Save();
            }

            // Update GUI.
            FolderListChanged();
        }

        /*private void handleMissingSyncFolder(Config.SyncConfig.Folder f)
        {
            bool handled = false;

            while (handled == false)
            {
                MissingFolderDialog dialog = new MissingFolderDialog(f);
                dialog.ShowDialog();
                if (dialog.action == MissingFolderDialog.Action.MOVE)
                {
                    String startPath = Directory.GetParent(f.LocalPath).FullName;
                    FolderBrowserDialog fbd = new FolderBrowserDialog();
                    fbd.SelectedPath = startPath;
                    fbd.Description = "Select the folder you have moved or renamed";
                    fbd.ShowNewFolderButton = false;
                    DialogResult result = fbd.ShowDialog();

                    if (result == DialogResult.OK && fbd.SelectedPath.Length > 0)
                    {
                        if (!Directory.Exists(fbd.SelectedPath))
                        {
                            throw new InvalidDataException();
                        }
                        Logger.Info("ControllerBase | Folder '" + f.DisplayName + "' ('" + f.LocalPath + "') moved to '" + fbd.SelectedPath + "'");
                        f.LocalPath = fbd.SelectedPath;

                        AddRepository(f.GetRepoInfo());
                        handled = true;
                    }
                }
                else if (dialog.action == MissingFolderDialog.Action.REMOVE)
                {
                    RemoveRepository(f);
                    ConfigManager.CurrentConfig.Folders.Remove(f);

                    Logger.Info("ControllerBase | Removed folder '" + f.DisplayName + "' from config");
                    handled = true;
                }
                else if (dialog.action == MissingFolderDialog.Action.RECREATE)
                {
                    RepoInfo info = f.GetRepoInfo();
                    RemoveRepository(f);
                    ConfigManager.CurrentConfig.Folders.Remove(f);
                    CreateRepository(info.Name,
                        info.Address,
                        info.User,
                        info.Password.ToString(),
                        info.RepoID,
                        info.RemotePath,
                        info.TargetDirectory,
                        info.getIgnoredPaths().OfType<String>().ToList(),
                        info.SyncAtStartup);
                    Logger.Info("ControllerBase | Folder '" + f.DisplayName + "' recreated");
                    handled = true;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            // handled == true
            // now resume the synchronization (if ever was suspended)
            // FIXME: the problem is that if the user suspended this repo it will get resumed anyway (ignoring the user setting)
            Program.Controller.ResumeRepositorySynchronization(f.DisplayName);
        }*/

        /// <summary>
        /// Fix the file attributes of a folder, recursively.
        /// </summary>
        /// <param name="path">Folder to fix</param>
        private void ClearFolderAttributes(string path)
        {
            if (!Directory.Exists(path))
                return;

            string[] folders = Directory.GetDirectories(path);

            foreach (string folder in folders)
                ClearFolderAttributes(folder);

            string[] files = Directory.GetFiles(path);

            foreach (string file in files)
                if (!CmisSync.Lib.Utils.IsSymlink(file))
                    File.SetAttributes(file, FileAttributes.Normal);
        }

        /// <summary>
        /// Create a new CmisSync synchronized folder.
        /// </summary>
        public void CreateRepository(string name, Uri address, string user, string password, string repository, string remote_path, string local_path,
            List<string> ignoredPaths, bool syncAtStartup)
        {
            repoInfo = new RepoInfo(name, ConfigManager.CurrentConfig.ConfigPath);
            repoInfo.Address = address;
            repoInfo.User = user;
            repoInfo.Password = new Password(password);
            repoInfo.RepoID = repository;
            repoInfo.RemotePath = remote_path;
            repoInfo.TargetDirectory = local_path;
            repoInfo.PollInterval = Config.DEFAULT_POLL_INTERVAL;
            repoInfo.IsSuspended = false;
            repoInfo.LastSuccessedSync = new DateTime(1900, 01, 01);
            repoInfo.SyncAtStartup = syncAtStartup;
            repoInfo.MaxUploadRetries = 2;

            foreach (string ignore in ignoredPaths)
                repoInfo.addIgnorePath(ignore);

            // Check that the CmisSync root folder exists.
            if (!Directory.Exists(ConfigManager.CurrentConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} does not exist", ConfigManager.CurrentConfig.FoldersPath));
                throw new DirectoryNotFoundException("Root folder don't exist !");
            }

            // Check that the folder is writable.
            if (!CmisSync.Lib.Utils.HasWritePermissionOnDir(ConfigManager.CurrentConfig.FoldersPath))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Default Folder {0} is not writable", ConfigManager.CurrentConfig.FoldersPath));
                throw new UnauthorizedAccessException("Root folder is not writable!");
            }

            // Check that the folder exists.
            if (Directory.Exists(repoInfo.TargetDirectory))
            {
                Logger.Fatal(String.Format("Fetcher | ERROR - Cmis Repository Folder {0} already exist", repoInfo.TargetDirectory));
                throw new UnauthorizedAccessException("Repository folder already exists!");
            }

            // Create the local folder.
            Directory.CreateDirectory(repoInfo.TargetDirectory);

            // Add folder to XML config file.
            ConfigManager.CurrentConfig.AddFolder(repoInfo);

            // Initialize in the GUI.
            AddRepository(repoInfo);
            FolderListChanged();
        }

        /// <summary>
        /// Show first-time wizard.
        /// </summary>
        public void ShowSetupWindow(PageType page_type)
        {
            ShowSetupWindowEvent(page_type);
        }

        /// <summary>
        /// Show info about CmisSync
        /// </summary>
        public void ShowAboutWindow()
        {
            ShowAboutWindowEvent();
        }

        /// <summary>
        /// Show an alert to the user.
        /// </summary>
        public void ShowAlert(string title, string message)
        {
            AlertNotificationRaised(Properties_Resources.CmisSync + " " + title, message);
        }

        /// <summary>
        /// Quit CmisSync.
        /// </summary>
        public virtual void Quit()
        {
            foreach (RepoBase repo in Repositories)
                repo.Dispose();

            folderLock.Dispose();

            Logger.Info("Exiting.");
            Environment.Exit(0);
        }

        /// <summary>
        /// A download or upload has started, so run task icon animation.
        /// </summary>
        public void ActivityStarted()
        {
            OnSyncing();
        }

        /// <summary>
        /// No download nor upload, so no task icon animation.
        /// </summary>
        public void ActivityStopped()
        {
            OnIdle();
        }

        /// <summary>
        /// Error occured.
        /// </summary>
        public void ActivityError(Tuple<string, Exception> error)
        {
            //FIXME: why a Tuple? We should get delegate(ErrorEvent event) or delegate(string repoName, Exception error)
            String reponame = error.Item1;
            Exception exception = error.Item2;

            if (exception is MissingSyncFolderException)
            {
                //Suspend sync... (should be resumed after the user has handled the error)
                Program.Controller.SuspendRepositorySynchronization(reponame);
                //FIXME: should update the suspended menu item, but i can't from here
                //UpdateSuspendSyncFolderEvent(reponame);
                
                //handle in a new thread, becouse this is the syncronization one and can be killed if the user decide to remove the repo or resync it
                /* Disabled because incompatible with Mac OS X: Thread t = new Thread(() =>
                {
                    handleMissingSyncFolder(ConfigManager.CurrentConfig.GetFolder(reponame));
                });
                t.SetApartmentState(ApartmentState.STA);
                t.Start();*/

                //dont resume here, the handler thread will if needed (or kill this thread)
                //Program.Controller.ResumeRepositorySynchronization(reponame);

                //handled, no need to do anything else
                return;
            }

            OnError(error);
        }
    }
}
