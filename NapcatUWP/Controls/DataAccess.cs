using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Windows.Storage;
using Microsoft.Data.Sqlite;

namespace NapcatUWP.Controls
{
    public class DataAccess
    {
        public static async void InitializeDatabase()
        {
            await ApplicationData.Current.LocalFolder.CreateFileAsync("setting.db",
                CreationCollisionOption.OpenIfExists);
            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (var db =
                   new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                var tableCommand = "CREATE TABLE IF NOT " +
                                   "EXISTS AppSettings (Primary_Key NVARCHAR(2048) PRIMARY KEY, " +
                                   "Text_Entry NVARCHAR(2048) NULL)";

                var createTable = new SqliteCommand(tableCommand, db);

                createTable.ExecuteReader();
            }
        }

        public static void InitInsert()
        {
            Insert("Server", "http://140.83.32.184:3000");
            Insert("Account","");
            Insert("Token","");
        }

        public static void Insert(String inputKey, String inputText)
        {
            if (inputKey == null || inputKey.Equals(String.Empty))
            {
                return;
            }
            string dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (SqliteConnection db =
                   new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                SqliteCommand insertCommand = new SqliteCommand();
                insertCommand.Connection = db;

                // Use parameterized query to prevent SQL injection attacks
                insertCommand.CommandText = "INSERT or IGNORE INTO AppSettings VALUES (@Key, @Entry);";
                insertCommand.Parameters.AddWithValue("@Key",inputKey);
                insertCommand.Parameters.AddWithValue("@Entry", inputText);

                insertCommand.ExecuteReader();
            }
        }
        public static NameValueCollection GetAllDatas()
        {
            NameValueCollection entries = new NameValueCollection();
            
            string dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (SqliteConnection db =
                   new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                SqliteCommand selectCommand = new SqliteCommand
                    ("SELECT * from AppSettings", db);

                SqliteDataReader query = selectCommand.ExecuteReader();

                while (query.Read())
                {
                    entries.Add(query.GetString(0),query.GetString(1));
                }
            }
            return entries;
        }

    }
}