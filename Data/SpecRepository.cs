using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SpecificGerpaas.Models;

namespace SpecificGerpaas.Data
{
    public class SpecRepository
    {
        private readonly string _dbPath;
        public SpecRepository(string dbPath = null)
        {
            if (!string.IsNullOrWhiteSpace(dbPath)) _dbPath = dbPath;
            else
            {
                var asmDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                _dbPath = System.IO.Path.Combine(asmDir, "Data", "gerpaas.db");
            }
        }

        private SqliteConnection Open()
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            var con = new SqliteConnection(cs);
            con.Open();
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                cmd.ExecuteNonQuery();
            }
            return con;
        }

        private const string TableSpecs = "specs";
        private const string ColArticle = "spec_article";
        private const string ColDescr = "spec_description";

        public bool TryFindByArticleLike(string articleLike, out SpecRow row, out string err)
        {
            row = null; err = null;
            try
            {
                using (var con = Open())
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = $@"
SELECT {ColArticle}, {ColDescr}
FROM   {TableSpecs}
WHERE  {ColArticle} LIKE $p
LIMIT  2;";
                    cmd.Parameters.AddWithValue("$p", articleLike + "%");

                    var list = new List<SpecRow>();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            list.Add(new SpecRow
                            {
                                Article = rd[ColArticle]?.ToString(),
                                Description = rd[ColDescr]?.ToString()
                            });
                        }
                    }

                    if (list.Count == 1) { row = list[0]; return true; }
                    err = list.Count == 0 ? "not_found" : "ambiguous";
                    return false;
                }
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return false;
            }
        }
    }
}
