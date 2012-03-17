//   SparkleShare, an instant update workflow to Git.
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
using System.Drawing;
using System.IO;
using System.Timers;

using Mono.Unix;
using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;

namespace SparkleShare {

    // The statusicon that stays in the
    // user's notification area
    public class SparkleStatusIcon : NSObject {

        public SparkleStatusIconController Controller = new SparkleStatusIconController ();

        // TODO: Fix case
        private Timer Animation;
        private int FrameNumber;
        private string StateText;

        private NSStatusItem StatusItem;
        private NSMenu Menu;
        private NSMenuItem StateMenuItem;
        private NSMenuItem FolderMenuItem;
        private NSMenuItem [] FolderMenuItems;
        private NSMenuItem SyncMenuItem;
        private NSMenuItem AboutMenuItem;
        private NSMenuItem NotificationsMenuItem;
        private NSMenuItem RecentEventsMenuItem;
        private NSMenuItem QuitMenuItem;
        private NSImage [] AnimationFrames;
        private NSImage [] AnimationFramesActive;
        private NSImage ErrorImage;
        private NSImage ErrorImageActive;
        private NSImage FolderImage;
        private NSImage CautionImage;
        private NSImage SparkleShareImage;

        private delegate void Task ();
        private EventHandler [] Tasks;

        
        // Short alias for the translations
        public static string _ (string s)
        {
            return Catalog.GetString (s);
        }

        
        public SparkleStatusIcon () : base ()
        {
            using (var a = new NSAutoreleasePool ())
            {
                ErrorImage        = new NSImage (NSBundle.MainBundle.ResourcePath + "/Pixmaps/sparkleshare-syncing-error-mac.png");
                ErrorImageActive  = new NSImage (NSBundle.MainBundle.ResourcePath + "/Pixmaps/sparkleshare-syncing-error-mac-active.png");
                FolderImage       = NSImage.ImageNamed ("NSFolder");
                CautionImage      = NSImage.ImageNamed ("NSCaution");
                SparkleShareImage = NSImage.ImageNamed ("sparkleshare-mac");

                Animation = CreateAnimation ();

                StatusItem = NSStatusBar.SystemStatusBar.CreateStatusItem (28);
                StatusItem.HighlightMode = true;
    
                if (Controller.Folders.Length == 0)
                    StateText = _("Welcome to SparkleShare!");
                else
                    StateText = _("Files up to date") + Controller.FolderSize;

                CreateMenu ();
    
                Menu.Delegate = new SparkleStatusIconMenuDelegate ();
            }


            Controller.UpdateQuitItemEvent += delegate (bool quit_item_enabled) {
                using (var a = new NSAutoreleasePool ())
                {
                    InvokeOnMainThread (delegate {
                        if (QuitMenuItem != null) {
                            QuitMenuItem.Enabled = quit_item_enabled;
                            StatusItem.Menu.Update ();
                        }
                    });
                }
            };

            Controller.UpdateMenuEvent += delegate (IconState state) {
                using (var a = new NSAutoreleasePool ())
                {
                    InvokeOnMainThread (delegate {
                        switch (state) {
                        case IconState.Idle:
    
                            Animation.Stop ();
                            
                            if (Controller.Folders.Length == 0)
                                StateText = _("Welcome to SparkleShare!");
                            else
                                StateText = _("Files up to date") + Controller.FolderSize;
    
                            StateMenuItem.Title = StateText;
                            CreateMenu ();
    
                            break;
    
                        case default:
							string state_text;
						
							if (state == IconState.SyncingUp)
								state_text = "Sending files…";
							else if (state == IconState.SyncingDown)
								state_text = "Receiving files…";
							else
								state_text = "Syncing…";
    
                            StateText = state_text +
                                        Controller.ProgressPercentage + "%  " +
                                        Controller.ProgressSpeed;

                            StateMenuItem.Title = StateText;
    
                            if (!Animation.Enabled)
                                Animation.Start ();
    
                            break;
    
                        case IconState.Error:

                            Animation.Stop ();
    
                            StateText = _("Not everything is synced");
                            StateMenuItem.Title = StateText;
                            CreateMenu ();

                            StatusItem.Image               = ErrorImage;
                            StatusItem.AlternateImage      = ErrorImageActive;
                            StatusItem.Image.Size          = new SizeF (16, 16);
                            StatusItem.AlternateImage.Size = new SizeF (16, 16);

                            break;
                        }

                        StatusItem.Menu.Update ();
                    });
                }
            };
        }


        public void CreateMenu ()
        {
            using (NSAutoreleasePool a = new NSAutoreleasePool ())
            {
                StatusItem.Image               = AnimationFrames [0];
                StatusItem.AlternateImage      = AnimationFramesActive [0];
                StatusItem.Image.Size          = new SizeF (16, 16);
                StatusItem.AlternateImage.Size = new SizeF (16, 16);
    
                Menu = new NSMenu ();
                Menu.AutoEnablesItems = false;
                
                    StateMenuItem = new NSMenuItem () {
                        Title = StateText,
                        Enabled = false
                    };
                
                Menu.AddItem (StateMenuItem);
                Menu.AddItem (NSMenuItem.SeparatorItem);
    
                    FolderMenuItem = new NSMenuItem () {
                        Title = "SparkleShare"
                    };
    
                    FolderMenuItem.Activated += delegate {
                        Controller.SparkleShareClicked ();
                    };
                
                    FolderMenuItem.Image = SparkleShareImage;
                    FolderMenuItem.Image.Size = new SizeF (16, 16);
                    FolderMenuItem.Enabled = true;
                
                Menu.AddItem (FolderMenuItem);
    
                    FolderMenuItems = new NSMenuItem [Program.Controller.Folders.Count];
    
                    if (Controller.Folders.Length > 0) {
                        Tasks = new EventHandler [Program.Controller.Folders.Count];
    
                        int i = 0;
                        foreach (string folder_name in Program.Controller.Folders) {
                            NSMenuItem item = new NSMenuItem ();
    
                            item.Title = folder_name;
    
                            if (Program.Controller.UnsyncedFolders.Contains (folder_name))
                                item.Image = CautionImage;
                            else
                                item.Image = FolderImage;

                            item.Image.Size = new SizeF (16, 16);
                            Tasks [i] = OpenFolderDelegate (folder_name);
    
                            FolderMenuItems [i] = item;
                            FolderMenuItems [i].Activated += Tasks [i];
                            FolderMenuItem.Enabled = true;

                            i++;
                        };
    
                    } else {
                        FolderMenuItems = new NSMenuItem [1];
    
                        FolderMenuItems [0] = new NSMenuItem () {
                            Title   = "No projects yet",
                            Enabled = false
                        };
                    }
    
                foreach (NSMenuItem item in FolderMenuItems)
                    Menu.AddItem (item);
                    
                Menu.AddItem (NSMenuItem.SeparatorItem);
    
                    SyncMenuItem = new NSMenuItem () {
                        Title   = "Add Hosted Project…",
                        Enabled = true
                    };
                
                    SyncMenuItem.Activated += delegate {
                        Controller.AddHostedProjectClicked ();
                    };

    
                Menu.AddItem (SyncMenuItem);
    
                    RecentEventsMenuItem = new NSMenuItem () {
                        Title = "View Recent Changes…",
                        Enabled = (Controller.Folders.Length > 0)
                    };
    
                    if (Controller.Folders.Length > 0) {
                        RecentEventsMenuItem.Activated += delegate {
                            Controller.OpenRecentEventsClicked ();
                        };
                    }
    
                Menu.AddItem (RecentEventsMenuItem);
				Menu.AddItem (NSMenuItem.SeparatorItem);
    
                    NotificationsMenuItem = new NSMenuItem () {
                        Enabled = true,
						Title   = "Notifications"
                    };
    
                    if (Program.Controller.NotificationsEnabled)
                        NotificationsMenuItem.State = NSCellStateValue.On;
                    else
                        NotificationsMenuItem.State = NSCellStateValue.Off;
    
                    NotificationsMenuItem.Activated += delegate {
                        Program.Controller.ToggleNotifications ();
    					CreateMenu ();
                    };
    
                Menu.AddItem (NotificationsMenuItem);
                Menu.AddItem (NSMenuItem.SeparatorItem);
    
                    AboutMenuItem = new NSMenuItem () {
                        Title = "About SparkleShare",
                        Enabled = true
                    };

                    AboutMenuItem.Activated += delegate {
                        Controller.AboutClicked ();
                    };
				
				Menu.AddItem (AboutMenuItem);
			    Menu.AddItem (NSMenuItem.SeparatorItem);

				
                QuitMenuItem = new NSMenuItem () {
                    Title   = "Quit",
                    Enabled = Controller.QuitItemEnabled
                };
    
                    QuitMenuItem.Activated += delegate {
                        Controller.QuitClicked ();
                    };

                Menu.AddItem (QuitMenuItem);

                StatusItem.Menu = Menu;
                StatusItem.Menu.Update ();
            }
        }


        // A method reference that makes sure that opening the
        // event log for each repository works correctly
        private EventHandler OpenFolderDelegate (string name)
        {
            return delegate {
                Controller.SubfolderClicked (name);
            };
        }


        // Creates the Animation that handles the syncing animation
        private Timer CreateAnimation ()
        {
            FrameNumber = 0;

            AnimationFrames = new NSImage [] {
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-i.png")),
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-ii.png")),
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-iii.png")),
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-iiii.png")),
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-iiiii.png"))
            };

            AnimationFramesActive = new NSImage [] {
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-i-active.png")),
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-ii-active.png")),
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-iii-active.png")),
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-iiii-active.png")),
                new NSImage (Path.Combine (NSBundle.MainBundle.ResourcePath, "Pixmaps", "process-syncing-sparkleshare-mac-iiiii-active.png"))
            };

            Timer Animation = new Timer () {
                Interval = 40
            };

            Animation.Elapsed += delegate {
                if (FrameNumber < 4)
                    FrameNumber++;
                else
                    FrameNumber = 0;

                InvokeOnMainThread (delegate {
                    StatusItem.Image               = AnimationFrames [FrameNumber];
                    StatusItem.AlternateImage      = AnimationFramesActive [FrameNumber];
                    StatusItem.Image.Size          = new SizeF (16, 16);
                    StatusItem.AlternateImage.Size = new SizeF (16, 16);
                });
            };

            return Animation;
        }

    }
    
    
    public class SparkleStatusIconMenuDelegate : NSMenuDelegate {
        
        public override void MenuWillHighlightItem (NSMenu menu, NSMenuItem item) { }
    
        public override void MenuWillOpen (NSMenu menu)
        {
            InvokeOnMainThread (delegate {
                NSApplication.SharedApplication.DockTile.BadgeLabel = null;
            });
        }
    }
}
