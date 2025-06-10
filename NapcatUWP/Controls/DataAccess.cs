using System;
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
            Insert("Account", "");
            Insert("Token", "");
        }

        public static void Insert(string inputKey, string inputText)
        {
            if (inputKey == null || inputKey.Equals(string.Empty)) return;
            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (var db =
                   new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                var insertCommand = new SqliteCommand();
                insertCommand.Connection = db;

                // Use parameterized query to prevent SQL injection attacks
                insertCommand.CommandText = "INSERT or IGNORE INTO AppSettings VALUES (@Key, @Entry);";
                insertCommand.Parameters.AddWithValue("@Key", inputKey);
                insertCommand.Parameters.AddWithValue("@Entry", inputText);

                insertCommand.ExecuteReader();
            }
        }

        public static NameValueCollection GetAllDatas()
        {
            var entries = new NameValueCollection();

            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (var db =
                   new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                var selectCommand = new SqliteCommand
                    ("SELECT * from AppSettings", db);

                var query = selectCommand.ExecuteReader();

                while (query.Read()) entries.Add(query.GetString(0), query.GetString(1));
            }

            return entries;
        }

        public static void UpdateSetting(string name, string value)
        {
            if (name == null || name.Equals(string.Empty))
                return;
            var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "setting.db");
            using (var db =
                   new SqliteConnection($"Filename={dbpath}"))
            {
                db.Open();

                var updateCommand =
                    new SqliteCommand("UPDATE AppSettings SET Text_Entry=@Entry WHERE Primary_Key=@Key", db);
                updateCommand.Parameters.AddWithValue("@Entry", value);
                updateCommand.Parameters.AddWithValue("@Key", name);
                updateCommand.ExecuteNonQuery();
            }
        }
    }
}