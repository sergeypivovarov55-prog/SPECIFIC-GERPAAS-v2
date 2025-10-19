// -----------------------------------------------------------------------------
// SPECIFIC-GERPAAS : CatalogSqlite.cs  (Schema: catalog_raw)
// -----------------------------------------------------------------------------

using Microsoft.Data.Sqlite;
using SpecificGerpaas.Core;
using SpecificGerpaas.Utils;
using System;
using System.IO;
using System.Reflection;

namespace SpecificGerpaas.Data
{
    public class CatalogRow
    {
        public string spec_article;
        public string spec_description;
    }

    public class CatalogSqlite
    {
        private readonly string _connString;
        private readonly string _dbPath;

        static CatalogSqlite()
        {
            try
            {
                SQLitePCL.Batteries_V2.Init();
                //Log.Info("[CatalogSqlite] SQLitePCLRaw initialized (Batteries_V2)");
            }
            catch (Exception ex)
            {
                Log.Error("[CatalogSqlite] Batteries_V2.Init() failed: " + ex.Message);
            }
        }

        public CatalogSqlite()
        {
            string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            _dbPath = Path.Combine(asmDir, "Data", "gerpaas.db");

            _connString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            if (!File.Exists(_dbPath))
                Log.Error($"[CatalogSqlite] Database file not found: {_dbPath}");
            //else
                //Log.Info($"[CatalogSqlite] DB path = {_dbPath}");
        }

        public int TryGetByArticleExact(string article, out CatalogRow row)
        {
            row = null;
            int count = 0;

            try
            {
                using (var conn = new SqliteConnection(_connString))
                {
                    conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT spec_article, spec_description
                            FROM catalog_raw
                            WHERE spec_article = $a;";
                        cmd.Parameters.AddWithValue("$a", article);

                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                if (row == null)
                                {
                                    row = new CatalogRow
                                    {
                                        spec_article = rd["spec_article"]?.ToString(),
                                        spec_description = rd["spec_description"]?.ToString()
                                    };
                                }
                                count++;
                                if (count > 1) break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[CatalogSqlite] TryGetByArticleExact(): " + ex.Message);
            }

            return count;
        }
    }
}
