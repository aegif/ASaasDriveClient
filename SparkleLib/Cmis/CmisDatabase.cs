﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.IO;

namespace SparkleLib.Cmis
{
    /**
     * Database to cache remote information from the CMIS server.
     * Implemented with SQLite.
     */
    public class CmisDatabase
    {
        /**
         * Name of the SQLite database file.
         */
        private string databaseFileName;

        /**
         * SQLite connection to the underlying database.
         */
        private SQLiteConnection sqliteConnection;

        /**
         * Length of the prefix to remove before storing paths.
         */
        private int pathPrefixSize;


        /**
         * Constructor.
         */
        public CmisDatabase(string dataPath)
        {
            this.databaseFileName = dataPath + ".cmissync";
            pathPrefixSize = dataPath.Length + 1; // +1 for the slash
        }


        /**
         * Create the database if it does not exist already.
         */
        public void RecreateDatabaseIfNeeded()
        {
            if (!File.Exists(databaseFileName))
                CreateDatabase();
        }


        /**
         * Create database and tables, if it does not exist yet.
         */
        public void CreateDatabase()
        {
            ConnectToSqliteIfNeeded();
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                command.CommandText =
                      "CREATE TABLE files ("
                    + "    path TEXT PRIMARY KEY,"
                    + "    serverSideModificationDate DATE,"
                    + "    checksum TEXT);" // Checksum of both data and metadata
                    + "CREATE TABLE folders ("
                    + "    path TEXT PRIMARY KEY,"
                    + "    serverSideModificationDate DATE);";
                command.ExecuteNonQuery();
            }
        }


        /**
         * Connect to SQLite if needed.
         */
        public void ConnectToSqliteIfNeeded()
        {
            if (sqliteConnection == null)
            {
                sqliteConnection = new SQLiteConnection("Data Source=" + databaseFileName);
                sqliteConnection.Open();
            }
        }


        /**
         * Normalize a path.
         * All paths stored in database must be normalized.
         * Goals:
         * - Make data smaller in database
         * - Reduce OS-specific differences
         */
        public string Normalize(string path)
        {
            // Remove path prefix
            path = path.Substring(pathPrefixSize, path.Length - pathPrefixSize);
            // Normalize all slashes to forward slash
            path = path.Replace(@"\", "/");
            return path;
        }


        /*
         *
         * 
         *
         * Database operations
         * 
         * 
         * 
         */

        public void AddFile(string path, DateTime? serverSideModificationDate)
        {
            path = Normalize(path);
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                try
                {
                    command.CommandText =
                        "INSERT OR REPLACE INTO files (path, serverSideModificationDate)"
                        + " VALUES (@filePath, @serverSideModificationDate)";
                    command.Parameters.AddWithValue("filePath", path);
                    command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                }
            }
        }


        public void AddFolder(string path, DateTime? serverSideModificationDate)
        {
            path = Normalize(path);
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                try
                {
                    command.CommandText =
                        "INSERT OR REPLACE INTO folders (path, serverSideModificationDate)"
                        + " VALUES (@path, @serverSideModificationDate)";
                    command.Parameters.AddWithValue("path", path);
                    command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                }
            }
        }


        public void RemoveFile(string path)
        {
            path = Normalize(path);
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                try
                {
                    command.CommandText =
                        "DELETE FROM files WHERE path=@filePath";
                    command.Parameters.AddWithValue("filePath", path);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                }
            }
        }


        public void RemoveFolder(string path)
        {
            path = Normalize(path);

            // Remove folder itself
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                try
                {
                    command.CommandText =
                        "DELETE FROM folders WHERE path='" + path + "'";
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                }
            }

            // Remove all folders under this folder
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                try
                {
                    command.CommandText =
                        "DELETE FROM folders WHERE path LIKE '" + path + "/%'";
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                }
            }

            // Remove all files under this folder
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                try
                {
                    command.CommandText =
                        "DELETE FROM files WHERE path LIKE '" + path + "/%'";
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                }
            }
        }


        public DateTime? GetServerSideModificationDate(string path)
        {
            path = Normalize(path);
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                try
                {
                    command.CommandText =
                        "SELECT serverSideModificationDate FROM files WHERE path=@path";
                    command.Parameters.AddWithValue("path", path);
                    object obj = command.ExecuteScalar();
                    return (DateTime?)obj;
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                    return null;
                }
            }
        }


        public void SetFileServerSideModificationDate(string path, DateTime? serverSideModificationDate)
        {
            path = Normalize(path);
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                try
                {
                    command.CommandText =
                        "UPDATE files"
                        + " SET serverSideModificationDate=@serverSideModificationDate"
                        + " WHERE path=@path";
                    command.Parameters.AddWithValue("serverSideModificationDate", serverSideModificationDate);
                    command.Parameters.AddWithValue("path", path);
                    command.ExecuteNonQuery();
                }
                catch (SQLiteException e)
                {
                    SparkleLogger.LogInfo("CmisDatabase", e.Message);
                }
            }
        }


        public bool ContainsFile(string path)
        {
            path = Normalize(path);
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                command.CommandText =
                    "SELECT serverSideModificationDate FROM files WHERE path=@path";
                command.Parameters.AddWithValue("path", path);
                object obj = command.ExecuteScalar();
                return obj != null;
            }
        }


        public bool ContainsFolder(string path)
        {
            path = Normalize(path);
            using (var command = new SQLiteCommand(sqliteConnection))
            {
                command.CommandText =
                    "SELECT serverSideModificationDate FROM folders WHERE path=@path";
                command.Parameters.AddWithValue("path", path);
                object obj = command.ExecuteScalar();
                return obj != null;
            }
        }
    }
}
