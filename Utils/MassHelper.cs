// Utils/MassHelper.cs
using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Microsoft.Data.Sqlite;
using SpecificGerpaas.Core;

namespace SpecificGerpaas.Utils
{
    public static class MassHelper
    {
        private static readonly string _dbPath;

        static MassHelper()
        {
            string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            _dbPath = Path.Combine(asmDir, "Data", "gerpaas.db");
        }

        public static void SetMassFromDb(Element e, string article)
        {
            try
            {
                if (e == null || string.IsNullOrWhiteSpace(article))
                    return;

                if (!File.Exists(_dbPath))
                {
                    Log.Error($"[MassHelper] База данных не найдена: {_dbPath}");
                    return;
                }

                double? mass = null;

                using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT kg_per_unit FROM catalog_raw WHERE spec_article = $article";
                        cmd.Parameters.AddWithValue("$article", article);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read() && !reader.IsDBNull(0))
                            {
                                mass = reader.GetDouble(0);
                            }
                        }
                    }
                }

                if (mass.HasValue)
                {
                    double kg = mass.Value;

                    // Определяем, какой параметр задавать
                    if (ParamHelper.HasParam(e, "DKC_ДлинаФакт"))
                    {
                        ParamHelper.SetNumberParam(e, "DKC_Масса погонного метра", kg);
                        Log.Info($"[MassHelper] Установлена масса погонного метра: {kg} кг для артикула {article}");
                    }
                    else
                    {
                        ParamHelper.SetNumberParam(e, "DKC_Масса", kg);
                        Log.Info($"[MassHelper] Установлена масса за штуку: {kg} кг для артикула {article}");
                    }
                }
                else
                {
                    Log.Warn($"[MassHelper] Масса не найдена в БД для артикула: {article}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MassHelper] Ошибка при установке массы: {ex.Message}");
            }
        }
    }
}
