//   CmisSync, a collaboration and sharing tool.
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


using System;
using System.IO;
using System.Timers;

using Threading = System.Threading;

using CmisSync.Lib;
using System.Globalization;

using CmisSync.Properties;

namespace CmisSync {

    public enum IconState {
        Idle,
        SyncingUp,
        SyncingDown,
        Syncing,
        Error
    }


    public class StatusIconController {

        public event UpdateIconEventHandler UpdateIconEvent = delegate { };
        public delegate void UpdateIconEventHandler (int icon_frame);

        public event UpdateMenuEventHandler UpdateMenuEvent = delegate { };
        public delegate void UpdateMenuEventHandler (IconState state);

        public event UpdateStatusItemEventHandler UpdateStatusItemEvent = delegate { };
        public delegate void UpdateStatusItemEventHandler (string state_text);

        public event UpdateQuitItemEventHandler UpdateQuitItemEvent = delegate { };
        public delegate void UpdateQuitItemEventHandler (bool quit_item_enabled);
		
		public event UpdateOpenRecentEventsItemEventHandler UpdateOpenRecentEventsItemEvent = delegate { };
        public delegate void UpdateOpenRecentEventsItemEventHandler (bool open_recent_events_item_enabled);
		
        public IconState CurrentState = IconState.Idle;
        public string StateText = Resources.ResourceManager.GetString("Welcome", CultureInfo.CurrentCulture);


        public readonly int MenuOverflowThreshold   = 9;
        public readonly int MinSubmenuOverflowCount = 3;


        public string [] Folders {
            get {
                int overflow_count = (Program.Controller.Folders.Count - MenuOverflowThreshold);

                if (overflow_count >= MinSubmenuOverflowCount)
                    return Program.Controller.Folders.GetRange (0, MenuOverflowThreshold).ToArray ();
                else
                    return Program.Controller.Folders.ToArray ();
            }
        }

        public string [] OverflowFolders {
            get {
                int overflow_count = (Program.Controller.Folders.Count - MenuOverflowThreshold);

                if (overflow_count >= MinSubmenuOverflowCount)
                    return Program.Controller.Folders.GetRange (MenuOverflowThreshold, overflow_count).ToArray ();
                else
                    return new string [0];
            }
        }

        public string FolderSize {
            get {
                double size = 0;

                foreach (RepoBase repo in Program.Controller.Repositories)
                    size += repo.Size;

                if (size == 0)
                    return "";
                else
                    return "— " + Program.Controller.FormatSize (size);
            }
        }

        public int ProgressPercentage {
            get {
                return (int) Program.Controller.ProgressPercentage;
            }
        }

        public string ProgressSpeed {
            get {
                return Program.Controller.ProgressSpeed;
            }
        }

        public bool QuitItemEnabled {
            get {
                // return (CurrentState == IconState.Idle || CurrentState == IconState.Error);
                return true;
            }
        }

        public bool OpenRecentEventsItemEnabled {
            get {
                return (Program.Controller.RepositoriesLoaded && Program.Controller.Folders.Count > 0);
            }
        }

        private Timer animation;
        private int animation_frame_number;


        public StatusIconController ()
        {
            InitAnimation ();

            Program.Controller.FolderListChanged += delegate {
                if (CurrentState != IconState.Error) {
                    CurrentState = IconState.Idle;

                    if (Program.Controller.Folders.Count == 0)
                        StateText = Resources.ResourceManager.GetString("Welcome", CultureInfo.CurrentCulture);
                    else
                        StateText = Resources.ResourceManager.GetString("FilesUpToDate", CultureInfo.CurrentCulture) + " " + FolderSize;
                }

                UpdateStatusItemEvent (StateText);
                UpdateMenuEvent (CurrentState);
            };

            Program.Controller.OnIdle += delegate {
                if (CurrentState != IconState.Error) {
                    CurrentState = IconState.Idle;

                    if (Program.Controller.Folders.Count == 0)
                        StateText = Resources.ResourceManager.GetString("Welcome", CultureInfo.CurrentCulture);
                    else
                        StateText = Resources.ResourceManager.GetString("FilesUpToDate", CultureInfo.CurrentCulture) + " " + FolderSize;
                }

                UpdateQuitItemEvent (QuitItemEnabled);
                UpdateStatusItemEvent (StateText);

                this.animation.Stop ();

                UpdateIconEvent (0);
                UpdateMenuEvent (CurrentState);
            };

            Program.Controller.OnSyncing += delegate {
				int repos_syncing_up   = 0;
				int repos_syncing_down = 0;
				
				foreach (RepoBase repo in Program.Controller.Repositories) {
					if (repo.Status == SyncStatus.SyncUp)
						repos_syncing_up++;
					
					if (repo.Status == SyncStatus.SyncDown)
						repos_syncing_down++;
				}
				
				if (repos_syncing_up > 0 &&
				    repos_syncing_down > 0) {
					
					CurrentState = IconState.Syncing;
                    StateText = Resources.ResourceManager.GetString("SyncingChanges", CultureInfo.CurrentCulture);
				
				} else if (repos_syncing_down == 0) {
					CurrentState = IconState.SyncingUp;
                    StateText = Resources.ResourceManager.GetString("SendingChanges", CultureInfo.CurrentCulture);
					
				} else {
					CurrentState = IconState.SyncingDown;
                    StateText = Resources.ResourceManager.GetString("ReceivingChanges", CultureInfo.CurrentCulture);
				}

                StateText += " " + ProgressPercentage + "%  " + ProgressSpeed;

                UpdateStatusItemEvent (StateText);
                UpdateQuitItemEvent (QuitItemEnabled);

                this.animation.Start ();
            };

            Program.Controller.OnError += delegate {
                CurrentState = IconState.Error;
                StateText = Resources.ResourceManager.GetString("FailedToSendSomeChanges", CultureInfo.CurrentCulture);

                UpdateQuitItemEvent (QuitItemEnabled);
                UpdateStatusItemEvent (StateText);

                this.animation.Stop ();

                UpdateIconEvent (-1);
            };
        }


        public void CmisSyncClicked ()
        {
            Program.Controller.OpenCmisSyncFolder ();
        }


        public void LocalFolderClicked (string reponame)
        {
            Program.Controller.OpenCmisSyncFolder (reponame);
        }

        public void RemoteFolderClicked(string reponame)
        {
            Program.Controller.OpenRemoteFolder(reponame);
        }

        public void AddHostedProjectClicked ()
        {
            Program.Controller.ShowSetupWindow (PageType.Add1);
        }


        public void OpenRecentEventsClicked ()
        {
            new Threading.Thread (() => Program.Controller.ShowEventLogWindow ()).Start ();
        }


        public void AboutClicked ()
        {
            Program.Controller.ShowAboutWindow ();
        }
        
		
        public void QuitClicked ()
        {
                Program.Controller.Quit ();
        }


        private void InitAnimation ()
        {
            this.animation_frame_number = 0;

            this.animation = new Timer () {
                Interval = 50
            };

            this.animation.Elapsed += delegate {
                if (this.animation_frame_number < 4)
                    this.animation_frame_number++;
                else
                    this.animation_frame_number = 0;

                UpdateIconEvent (this.animation_frame_number);
            };
        }
    }
}
