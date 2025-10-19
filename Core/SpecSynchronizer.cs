using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SpecificGerpaas.Data;
using SpecificGerpaas.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SpecificGerpaas.Core
{
    /// <summary>
    /// Синхронизация параметров спецификации (вариант Б).
    /// Категории и базовые артикулы берём из FamilyMap.ini.
    /// Найменування — из БД (catalog_raw) по точному артикулу.
    /// Количество и ед. изм. — строго по GE_Категорія:
    ///   1. Кабельні лотки  → лотки + их крышки → GE_Кількість=0, Ед.=м
    ///   2. З'єднувальні деталі → фитинги + крышки фитингов → GE_Кількість=1, Ед.=шт
    ///   3. Монтажні вироби → GE_Кількість=1, Ед.=шт
    /// </summary>
    public class SpecSynchronizer
    {
        private readonly Document _doc;
        private readonly UIApplication _uiapp;

        private readonly Dictionary<string, FamilyMapRecord> _familyMap =
            new Dictionary<string, FamilyMapRecord>(StringComparer.OrdinalIgnoreCase);

        private readonly CatalogSqlite _catalog;

        private double _thicknessMm = 0.8;
        private string _metal = "Сендзимір";

        // Счётчики
        private int _countTrays = 0;
        private int _countFittings = 0;
        private int _countAccessories = 0;
        private int _countErrors = 0;
        private int _countProcessed = 0;

        public SpecSynchronizer(Document doc, UIApplication uiapp)
        {
            _doc = doc;
            _uiapp = uiapp;

            // --- Комбики (вкладка "СПП" → панель "Cпецифікації GERPAAS") ---
            string thkText = DkcComboManager.GetSelectedFromComboExact(
                uiapp, "СПП", "Cпецифікації GERPAAS", "SGP_Thickness") ?? "0,8";
            string matText = DkcComboManager.GetSelectedFromComboExact(
                uiapp, "СПП", "Cпецифікації GERPAAS", "SGP_Metal") ?? "Сендзимір";

            _thicknessMm = ParseThicknessMm(thkText);
            _metal = matText;
            Log.Info($"[SpecSynchronizer] Параметры пользователя: Thk={_thicknessMm} мм, Metal='{_metal}'");

            // --- FamilyMap.ini (рядом с DLL: ..\Data\GERP_param_map.ini) ---
            FamilyMapLoader.Load(_familyMap);

            // --- Каталог SQLite ---
            _catalog = new CatalogSqlite();
        }

        private static double ParseThicknessMm(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0.8;
            s = s.Trim().Replace(',', '.');
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                return val;
            return 0.8;
        }

        public Result Run()
        {
            try
            {
                Log.Info("=== SPECIFIC-GERPAAS: Синхронизация (вариант Б) ===");

                // Обходим кабельные лотки и фитинги
                var ids = new List<ElementId>();
                ids.AddRange(new FilteredElementCollector(_doc).OfCategory(BuiltInCategory.OST_CableTray).WhereElementIsNotElementType().ToElementIds());
                ids.AddRange(new FilteredElementCollector(_doc).OfCategory(BuiltInCategory.OST_CableTrayFitting).WhereElementIsNotElementType().ToElementIds());

                using (var t = new Transaction(_doc, "SPECIFIC-GERPAAS: sync"))
                {
                    t.Start();

                    foreach (var id in ids)
                    {
                        var e = _doc.GetElement(id);
                        ProcessOne(e);
                    }

                    t.Commit();
                }

                // Итог
                int total = _countProcessed;
                int undef = _countErrors;
                string summary =
                    "Процес завершено.\n" +
                    $"1. Кабельні лотки: {_countTrays}\n" +
                    $"2. З'єднувальні деталі: {_countFittings}\n" +
                    $"3. Монтажні вироби: {_countAccessories}\n" +
                    $"4. Не визначено та помилки: {undef}\n" +
                    $"Всього: {total}";
                Log.Info(summary);
                TaskDialog.Show("SPECIFIC-GERPAAS", summary);

                Log.Info("=== SPECIFIC-GERPAAS: Завершено ===");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error($"[SpecSynchronizer] EXCEPTION: {ex}");
                return Result.Failed;
            }
        }

        private void ProcessOne(Element e)
        {
            if (e == null) return;

            string famName = ElementHelper.GetFamilyName(e);
            if (string.IsNullOrEmpty(famName))
            {
                _countErrors++;
                return;
            }

            // --- Аксессуары: пропускаем всё, КРОМЕ крышки прямых участков ---
            // Крышки лотков сидят внутри контейнера "470_DKC_S5_Accessories", но сами должны обрабатываться.
            if (famName.StartsWith("999_DKC_Accessories", StringComparison.OrdinalIgnoreCase))
            {
                _countAccessories++;
                return;
            }
            if (famName.StartsWith("470_DKC_S5_Accessories", StringComparison.OrdinalIgnoreCase)
                && !famName.Contains("S5-L5_Cover", StringComparison.OrdinalIgnoreCase))
            {
                _countAccessories++;
                return;
            }

            // --- FamilyMap: ищем базовые настройки ---
            if (!_familyMap.TryGetValue(famName, out var fm))
            {
                Log.Warn($"[FamilyMap] Нема у FamilyMap: {famName}");
                _countErrors++;
                return;
            }

            string baseArticle = fm.BaseArticle ?? "";
            string category = fm.Category ?? "";
            string additional = fm.Additional ?? "";

            // --- Количество и единицы измерения строго по GE_Категорія ---
            ApplyQuantityAndUnitByCategory(e, category);

            // --- Размеры + угол ---
            int w, h;
            double? ang;
            SizeHelper.GetWidthHeightAndAngle(e, out w, out h, out ang);

            // --- Артикул ---
            string article = ArticleBuilder.BuildArticle(baseArticle, w, h, _thicknessMm, _metal, ang);
            if (string.IsNullOrEmpty(article))
            {
                _countErrors++;
                return;
            }

            // --- Найменування из БД ---
            CatalogRow row;
            int found = _catalog.TryGetByArticleExact(article, out row);
            if (found == 0)
                Log.Warn($"[DB] Не знайдено: {article}");
            else if (found > 1)
                Log.Warn($"[DB] Дублікати ({found} записів): {article}");

            // --- Запись параметров ---
            ParamHelper.SetTextParam(e, "GE_Категорія", category);
            ParamHelper.SetTextParam(e, "GE_Артикул", article);
            ParamHelper.SetTextParam(e, "GE_Найменування", row?.spec_description ?? "");
            ParamHelper.SetTextParam(e, "GE_Додаткові", additional);

            // счётчики категорий
            if (category.StartsWith("1.", StringComparison.Ordinal)) _countTrays++;
            else if (category.StartsWith("2.", StringComparison.Ordinal)) _countFittings++;
            else if (category.StartsWith("3.", StringComparison.Ordinal)) _countAccessories++;
            else _countErrors++;

            _countProcessed++;
        }

        /// <summary>
        /// Количество и единицы измерения строго по GE_Категорія:
        /// 1.* → метры (0),  2.* → штуки (1),  3.* → штуки (1).
        /// Для крышек лотков (категория 1.*) прокидываем фактическую длину в мм в DKC_ДлинаФакт,
        /// чтобы формула спецификации (деление на 1000 мм) сработала.
        /// </summary>
        private void ApplyQuantityAndUnitByCategory(Element e, string category)
        {
            // По умолчанию — шт.
            string unit = "шт.";
            string qty = "1";

            if (!string.IsNullOrEmpty(category))
            {
                if (category.StartsWith("1.", StringComparison.Ordinal)) // Кабельні лотки
                {
                    unit = "м";
                    qty = "0";

                    // Прокинем длину для крышек лотков (которые пришли из контейнера аксессуаров)
                    // Определяем крышку по имени семейства (как и ранее)
                    string fam = ElementHelper.GetFamilyName(e) ?? "";
                    if (fam.Equals("470_DKC_S5-L5_Cover", StringComparison.OrdinalIgnoreCase) ||
                        (fam.StartsWith("470_DKC_S5_Accessories", StringComparison.OrdinalIgnoreCase)
                         && fam.Contains("S5-L5_Cover", StringComparison.OrdinalIgnoreCase)))
                    {
                        double lenMm = SizeHelper.GetCoverLengthMm(e);
                        var pLen = e.LookupParameter("DKC_ДлинаФакт");
                        if (pLen != null && !pLen.IsReadOnly && pLen.StorageType == StorageType.Double)
                        {
                            pLen.Set(lenMm);
                            Log.Info($"[PARAM] DKC_ДлинаФакт={lenMm:0} мм (кришка лотка)");
                        }
                        else
                        {
                            Log.Warn($"[PARAM] DKC_ДлинаФакт недоступен для крышки лотка");
                        }
                    }
                }
                else if (category.StartsWith("2.", StringComparison.Ordinal)) // Фитинги
                {
                    unit = "шт.";
                    qty = "1";
                }
                else if (category.StartsWith("3.", StringComparison.Ordinal)) // Монтажні вироби
                {
                    unit = "шт.";
                    qty = "1";
                }
            }

            ParamHelper.SetTextParam(e, "GE_Кількість", qty);
            ParamHelper.SetTextParam(e, "DKC_Единица измерения", unit);
        }
    }

    // --------- ВСПОМОГАТЕЛЬНЫЕ ТИПЫ (под твой проект) ---------

    public class FamilyMapRecord
    {
        public string BaseArticle { get; set; }
        public string Category { get; set; }
        public string Additional { get; set; }
    }

    public static class ElementHelper
    {
        public static string GetFamilyName(Element e)
        {
            if (e is FamilyInstance fi) return fi.Symbol?.Family?.Name ?? "";
            // системные лотки — у них семейство определяется иначе
            try { return e?.Name ?? ""; } catch { return ""; }
        }
    }

    public static class DkcComboManager
    {
        public static string GetSelectedFromComboExact(UIApplication uiapp, string tab, string panel, string key)
        {
            // Твой уже существующий способ. Здесь — заглушка на случай отсутствия.
            return null;
        }
    }

    public static class ParamHelper
    {
        public static void SetTextParam(Element e, string paramName, string value)
        {
            var p = e.LookupParameter(paramName);
            if (p == null || p.IsReadOnly) return;
            if (p.StorageType == StorageType.String) p.Set(value ?? "");
        }
    }
}
