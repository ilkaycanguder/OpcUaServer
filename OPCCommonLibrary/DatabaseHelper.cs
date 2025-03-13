using Npgsql;
using System;
using System.Collections.Generic;

namespace OPCCommonLibrary
{
    public class DatabaseHelper
    {
        public const string connectionString = "Host=localhost;Port=5432;Username=postgres;Password=123456;Database=OPCUABase;SearchPath=TESASch";

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

        /// **🔹 İstemcinin Yetkilendirilmiş OPC UA Tag'lerini Getir**
        public static async Task<List<OpcTag>> GetAuthorizedTagsAsync(Guid clientGuid)
        {
            List<OpcTag> authorizedTags = new List<OpcTag>();

            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // 🔥 PostgreSQL'de text UUID ile karşılaştırma yaparken CAST etmek gerekebilir
                string query = "SELECT tagid FROM \"TESASch\".\"clientyetkilendirme\" WHERE clientguid::text = @ClientGuid";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ClientGuid", clientGuid.ToString());

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int[] tagIds = (int[])reader["tagid"];

                            if (tagIds.Length > 0)
                            {
                                using (var tagConn = new NpgsqlConnection(connectionString))
                                {
                                    await tagConn.OpenAsync();

                                    string tagQuery = "SELECT id, \"TagName\", \"TagValue\" FROM \"TESASch\".\"comp_tag_dtl\" WHERE id = ANY(@TagIds)";

                                    using (var tagCmd = new NpgsqlCommand(tagQuery, tagConn))
                                    {
                                        tagCmd.Parameters.AddWithValue("@TagIds", tagIds);

                                        using (var tagReader = await tagCmd.ExecuteReaderAsync())
                                        {
                                            while (await tagReader.ReadAsync())
                                            {
                                                authorizedTags.Add(new OpcTag
                                                {
                                                    Id = tagReader.GetInt32(0),
                                                    TagName = tagReader.GetString(1),
                                                    TagValue = tagReader.GetInt32(2),
                                                    LastUpdate = DateTime.Now
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine($"🔍 Yetkilendirilmiş {authorizedTags.Count} tag bulundu!");
            return authorizedTags;
        }
    }
}