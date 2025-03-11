using Npgsql;
using OPCCommonLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCCommonLibrary
{
    public class DatabaseHelper
    {
        private const string connectionString = "Host=localhost;Port=5432;Username=postgres;Password=123456;Database=OPCUABase;SearchPath=TESASch";
        public static List<OpcTag> GetTagsFromDatabase()
        {
            List<OpcTag> tags = new List<OpcTag>();

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("SELECT id, \"TagName\", \"TagValue\" FROM \"TESASch\".\"comp_tag_dtl\"", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tags.Add(new OpcTag
                        {
                            Id = reader.GetInt32(0),
                            TagName = reader.GetString(1),
                            TagValue = reader.GetInt32(2),
                            LastUpdate = DateTime.Now
                        });
                    }
                }
            }
            return tags;
        }


        public static void UpdateTagValue(string tagName, int newValue)
        {
            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("UPDATE \"TESASch\".\"comp_tag_dtl\" SET \"TagValue\" = @value WHERE \"TagName\" = @name", conn))
                {
                    cmd.Parameters.AddWithValue("value", newValue);
                    cmd.Parameters.AddWithValue("name", tagName);
                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        Console.WriteLine($"✅ PostgreSQL Güncellendi: {tagName} = {newValue}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ PostgreSQL Güncellenemedi: {tagName}");
                    }
                }
            }
        }
        //public static void InsertNewTag(string tagName, int tagValue)
        //{
        //    using (var conn = new NpgsqlConnection(connectionString))
        //    {
        //        conn.Open();
        //        using (var cmd = new NpgsqlCommand("INSERT INTO \"TESASch\".\"comp_tag_dtl\" (\"TagName\", \"TagValue\") VALUES (@name, @value)", conn))
        //        {
        //            cmd.Parameters.AddWithValue("name", tagName);
        //            cmd.Parameters.AddWithValue("value", tagValue);

        //            int rowsAffected = cmd.ExecuteNonQuery();

        //            if (rowsAffected > 0)
        //            {
        //                Console.WriteLine($"✅ PostgreSQL Yeni Tag Eklendi: {tagName} = {tagValue}");
        //            }
        //            else
        //            {
        //                Console.WriteLine($"⚠️ PostgreSQL Tag Eklenemedi: {tagName}");
        //            }
        //        }
        //    }
        //}

    }
}
