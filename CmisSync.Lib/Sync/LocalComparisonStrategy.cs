﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using DotCMIS.Client;
using System.IO;
using CmisSync.Lib.Database;

namespace CmisSync.Lib.Sync
{
    public partial class CmisRepo : RepoBase
    {
        /// <summary>
        /// Synchronization by comparizon with local database.
        /// </summary>
        public partial class SynchronizedFolder
        {
            /// <summary>
            /// Detect what has changed using the local database, and apply these
            /// modifications to the remote server.
            /// </summary>
            /// <param name="rootFolder">Full path of the local synchronized folder, for instance "/User Homes/nicolas.raoul/demos"</param>
            public bool ApplyLocalChanges(string rootFolder)
            {
                Logger.Debug("Checking for local changes");

                var deletedFolders = new List<string>();
                var deletedFiles = new List<string>();
                var modifiedFiles = new List<string>();
                var addedFolders = new List<string>();
                var addedFiles = new List<string>();

                // Crawl through all entries in the database, and record the ones that have changed on the filesystem.
                // Check for deleted folders.
                var folders = database.GetLocalFolders();
                foreach(string folder in folders)
                {
                    if (!Directory.Exists(Utils.PathCombine(rootFolder, folder)))
                    {
                        deletedFolders.Add(folder);
                    }
                }
                var files = database.GetChecksummedFiles();
                foreach (ChecksummedFile file in files)
                {
                    // Check for deleted files.
                    if (File.Exists(Path.Combine(rootFolder, file.RelativePath)))
                    {
                        // Check for modified files.
                        if(file.HasChanged(rootFolder))
                        {
                            modifiedFiles.Add(file.RelativePath);
                        }
                    }
                    else
                    {
                        deletedFiles.Add(file.RelativePath);
                    }
                }

                // Check for added folders and files.
                // TODO performance improvement: To reduce the number of database requests, count files and folders, and skip this step if equal to the numbers of database rows?
                FindNewLocalObjects(rootFolder, ref addedFolders, ref addedFiles);

                // Ignore added files that are sub-items of an added folder.
                // Folder addition is done recursively so no need to add files twice.
                foreach (string file in new List<string>(addedFiles)) // Copy the list to avoid modifying it while iterating.
                {
                    addedFolders.RemoveAll(p => p.StartsWith(file) && p.Length > file.Length);

                }

                // Ignore removed folders that are sub-items of a removed folder.
                // Folder removal is done recursively so no need to remove sub-items twice.
                foreach (string addedFolder in new List<string>(addedFolders)) // Copy the list to avoid modifying it while iterating.
                {
                    addedFolders.RemoveAll(p => p.StartsWith(addedFolder) && p.Length > addedFolder.Length);
                }

                // TODO: Try to make sense of related changes, for instance renamed folders.

                // TODO: Check local metadata modification cache.

                int numberOfChanges = deletedFolders.Count + deletedFiles.Count + modifiedFiles.Count + addedFolders.Count + addedFiles.Count;
                Logger.Debug(numberOfChanges + " local changes to apply.");

                if (numberOfChanges == 0)
                {
                    return true; // Success: Did nothing.
                }

                // Apply changes to the server.
                bool success = true;

                activityListener.ActivityStarted();

                // Apply: Deleted folders.
                foreach(string deletedFolder in deletedFolders)
                {
                    SyncItem deletedItem = SyncItemFactory.CreateFromLocalPath(deletedFolder, true, repoInfo, database);
                    try
                    {
                        IFolder deletedIFolder = (IFolder)session.GetObjectByPath(deletedItem.RemotePath);
                        DeleteRemoteFolder(deletedIFolder, deletedItem, Utils.UpperFolderLocal(deletedItem.LocalPath));
                    }
                    catch (ArgumentNullException e)
                    {
                        // Typical error when the document does not exist anymore on the server
                        // TODO Make DotCMIS generate a more precise exception.

                        Logger.Error("The folder has probably been deleted on the server already: " + deletedFolder, e);

                        // Delete local database entry.
                        database.RemoveFolder(SyncItemFactory.CreateFromLocalPath(deletedFolder, true, repoInfo, database));

                        // Note: This is not a failure per-se, so we don't need to modify the "success" variable.
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error applying local folder deletion to the server: " + deletedFolder, e);
                        success = false;
                    }
                }

                // Apply: Deleted files.
                foreach (string deletedFile in deletedFiles)
                {
                    SyncItem deletedItem = SyncItemFactory.CreateFromLocalPath(deletedFile, true, repoInfo, database);
                    try
                    {
                        IDocument deletedDocument = (IDocument)session.GetObjectByPath(deletedItem.RemotePath);
                        DeleteRemoteDocument(deletedDocument, deletedItem);
                    }
                    catch (ArgumentNullException e)
                    {
                        // Typical error when the document does not exist anymore on the server
                        // TODO Make DotCMIS generate a more precise exception.

                        Logger.Error("The document has probably been deleted on the server already: " + deletedFile, e);

                        // Delete local database entry.
                        database.RemoveFile(SyncItemFactory.CreateFromLocalPath(deletedFile, false, repoInfo, database));

                        // Note: This is not a failure per-se, so we don't need to modify the "success" variable.
                    }
                    catch (Exception e)
                    {
                        // Could be a network error.
                        Logger.Error("Error applying local file deletion to the server: " + deletedFile, e);
                        success = false;
                    }
                }

                // Apply: Modified files.
                foreach (string modifiedFile in modifiedFiles)
                {
                    SyncItem modifiedItem = SyncItemFactory.CreateFromLocalPath(modifiedFile, true, repoInfo, database);
                    try
                    {
                        IDocument modifiedDocument = (IDocument)session.GetObjectByPath(modifiedItem.RemotePath);
                        UpdateFile(modifiedItem.LocalPath, modifiedDocument);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error applying local file modification to the server: " + modifiedFile, e);
                        success = false;
                    }
                }

                // Apply: Added folders.
                foreach (string addedFolder in addedFolders)
                {
                    string destinationFolderPath = Path.GetDirectoryName(addedFolder);
                    SyncItem folderItem = SyncItemFactory.CreateFromLocalPath(destinationFolderPath, true, repoInfo, database);
                    try
                    {
                        IFolder destinationFolder = (IFolder)session.GetObjectByPath(folderItem.RemotePath);
                        UploadFolderRecursively(destinationFolder, addedFolder);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error applying local folder addition to the server: " + addedFolder, e);
                        success = false;
                    }
                }

                // Apply: Added files.
                foreach (string addedFile in addedFiles)
                {
                    string destinationFolderPath = Path.GetDirectoryName(addedFile);
                    SyncItem folderItem = SyncItemFactory.CreateFromLocalPath(destinationFolderPath, true, repoInfo, database);
                    try
                    {
                        IFolder destinationFolder = (IFolder)session.GetObjectByPath(folderItem.RemotePath);
                        UploadFile(addedFile, destinationFolder);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error applying local file addition to the server: " + addedFile, e);
                        success = false;
                    }
                }

                Logger.Debug("Finished applying local changes.");
                activityListener.ActivityStopped();

                return success;
            }

            public void FindNewLocalObjects(string folder, ref List<string> addedFolders, ref List<string> addedFiles)
            {
                // Check files in this folder.
                string[] files;
                try
                {
                    files = Directory.GetFiles(folder);
                }
                catch (Exception e)
                {
                    Logger.Warn("Could not get the files list from folder: " + folder, e);
                    return;
                }

                foreach (string file in files)
                {
                    // Check whether this file is present in database.
                    string filePath = Path.Combine(folder, file);
                    if ( ! database.ContainsLocalFile(filePath))
                    {
                        addedFiles.Add(filePath);
                    }
                }

                // Check folders and recurse.
                string[] subFolders;
                try
                {
                    subFolders = Directory.GetDirectories(folder);
                }
                catch (Exception e)
                {
                    Logger.Warn("Could not get the folders list from folder: " + folder, e);
                    return;
                }

                foreach (string subFolder in subFolders)
                {
                    // Check whether this sub-folder is present in database.
                    string folderPath = Path.Combine(folder, subFolder);
                    if (!database.ContainsLocalPath(folderPath))
                    {
                        addedFolders.Add(folderPath);
                    }

                    // Recurse.
                    FindNewLocalObjects(folderPath, ref addedFolders, ref addedFiles);
                }
            }
        }
    }
}