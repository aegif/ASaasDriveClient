﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;

namespace CmisSync
{
    /// <summary>
    /// Edit folder diaglog
    /// It allows user to edit the selected and ignored folders
    /// </summary>
    class Edit : SetupWindow
    {
        /// <summary>
        /// Controller
        /// </summary>
        public EditController Controller = new EditController();


        /// <summary>
        /// Synchronized folder name
        /// </summary>
        public string Name;


        private string username;
        private string password;
        private string address;
        private string id;
        private string remotePath;
        private List<string> ignores;
        private string localPath;


        /// <summary>
        /// Constructor
        /// </summary>
        public Edit(string name, string username, string password, string address, string id, string remotePath, List<string> ignores, string localPath)
        {
            Name = name;
            this.username = username;
            this.password = password;
            this.address = address;
            this.id = id;
            this.remotePath = remotePath;
            this.ignores = ignores;
            this.localPath = localPath;

            CreateEdit();

            Controller.ShowWindowEvent += delegate
            {
                this.Show();
            };
        }


        protected override void Close(object sender, CancelEventArgs args)
        {
            Controller.HideWindow();
        }


        /// <summary>
        /// Create the UI
        /// </summary>
        private void CreateEdit()
        {
            System.Uri resourceLocater = new System.Uri("/DataSpaceSync;component/TreeView.xaml", System.UriKind.Relative);
            TreeView treeView = Application.LoadComponent(resourceLocater) as TreeView;

            CmisSync.CmisTree.CmisRepo repo = new CmisSync.CmisTree.CmisRepo(username, password, address, ignores, localPath)
            {
                Name = Name,
                Id = id
            };
            repo.LoadingSubfolderAsync();

            List<CmisSync.CmisTree.CmisRepo> repos = new List<CmisSync.CmisTree.CmisRepo>();
            repos.Add(repo);
            repo.Selected = true;

            treeView.DataContext = repos;

            ContentCanvas.Children.Add(treeView);
            Canvas.SetTop(treeView, 70);
            Canvas.SetLeft(treeView, 185);

            Controller.HideWindowEvent += delegate
            {
                repo.cancelLoadingAsync();
            };


            Button finish_button = new Button()
            {
                Content = Properties_Resources.Finish,
                IsDefault = true
            };

            Button cancel_button = new Button()
            {
                Content = Properties_Resources.Cancel,
                IsDefault = false
            };

            Buttons.Add(finish_button);
            Buttons.Add(cancel_button);

            finish_button.Focus();

            finish_button.Click += delegate
            {
                Controller.SaveFolder();
                this.Close();
            };

            cancel_button.Click += delegate
            {
                this.Close();
            };


            this.ShowAll();
        }
    }
}
