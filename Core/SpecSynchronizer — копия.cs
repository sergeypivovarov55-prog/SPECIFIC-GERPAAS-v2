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
    // Синхронизация параметров спецификации (вариант Б).
    // Категории и базовые артикулы берём из FamilyMap.ini.
    // Найменування — из БД (catalog_raw) по точному артикулу.
    // Количество и ед. изм. — строго по GE_Категорія:
    //   1. Кабельні лотки  → лотки + их крышки → GE_Кількість=0, Ед.=м
    //   2. З'єднувальні деталі → фитинги + крышки фитингов → GE_Кількість=1, Ед.=шт
    //   3. Монтажні вироби → GE_Кількість=1, Ед.=шт
    public class SpecSynchronizer
    {
        private readonly Document _doc;
        private readonly UIApplication _uiapp;

        // Замените тип словаря на FamilyMapRow, чтобы соответствовать возвращаемому типу FamilyMapLoader.Load()
        private Dictionary<string, FamilyMapRow> _familyMap =
            new Dictionary<string, FamilyMapRow>(StringComparer.OrdinalIgnoreCase);

        private readonly CatalogSqlite _catalog;

        private double _thicknessMm = 0.8;
        private string _metal = "Сендзимір";

        // Счётчики
        //private int _countTrays = 0;
        //private int _countFittings = 0;
        //private int _countAccessories = 0;
        private int _countErrors = 0;
        private int _countProcessed = 0;
        // --- Статистика по категориям ---
        private int _countCat1 = 0;       // Кабельні лотки
        private int _countCat2 = 0;       // З'єднувальні деталі
        private int _countCat3 = 0;       // Монтажні вироби
        private int _countCatOther = 0;   // Інші
        // накопитель сообщений об ошибках для финального окна
        private readonly System.Collections.Generic.List<string> _errors = new System.Collections.Generic.List<string>();


        public SpecSynchronizer(Document doc, UIApplication uiapp)
        {
            _doc = doc;
            _uiapp = uiapp;

            // --- Комбики (вкладка "СПП" → панель "Cпецифікації GERPAAS") ---
            // получаем текущие выбранные значения из ini через DkcComboManager
            DkcComboManager.Init();

            string thkText = DkcComboManager.GetSelectedThickness();
            string matText = DkcComboManager.GetSelectedCoating();

            _thicknessMm = ParseThicknessMm(thkText);
            _metal = matText;
            Log.Info("   ");
            Log.Info($"===== Старт з параметрами користувача: Thk={_thicknessMm} мм, Metal='{_metal}' =====");

            // --- FamilyMap.ini (рядом с DLL: ..\Data\GERP_param_map.ini) ---
            _familyMap = FamilyMapLoader.Load();

            // --- Каталог SQLite ---
            _catalog = new CatalogSqlite();
        }

        private double ParseThicknessMm(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0.8; // fallback по умолчанию

            // убираем мусор: "мм", пробелы
            raw = raw.Replace("мм", "")
                     .Replace(" ", "")
                     .Trim();

            // заменяем запятую точкой (т.к. в ComboBox выводится "1,2")
            raw = raw.Replace(",", ".");

            // пробуем распарсить
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                return val;
            }

            // если не получилось — лог и fallback
            Log.Warn($"[SpecSynchronizer] Некорректная толщина '{raw}', используется 0.8");
            return 0.8;
        }

        public Result Run()
        {
            try
            {
                Log.Info("Початок синхронизациї");

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
                string result =
                    $"Оброблено елементів: {_countProcessed}\n" +
                    $"  1. Кабельні лотки + кришки: {_countCat1}\n" +
                    $"  2. З’єднувальні деталі: {_countCat2}\n" +
                    $"  3. Монтажні вироби: {_countCat3}\n" +
                    $"  4. Інші: {_countCatOther}\n" +
                    $"Помилки: {_countErrors}";
                TaskDialog.Show("Підсумок", result);
                Log.Info(result);

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

            try
            {
                // очистка значений GE_* перед обработкой
                ResetGE(e);

                string famName = ElementHelper.GetFamilyName(e);

                // Вначале - ACCESSORIES — 3-я категорія, GE-AX-
                if (IsAccessory(famName))
                {
                    SaveAccessoryInfo(e); // збираємо деталі у accessories_raw.ini

                    // Визначення типу (для GE_Найменування)
                    string typeName = "-";
                    if (e is FamilyInstance fiAcc)
                        typeName = fiAcc.Symbol?.Name ?? "-";

                    string typeUa = TranslateAccessoryType(typeName);

                    // Запис параметрів у модель
                    ParamHelper.SetTextParam(e, "GE_Артикул", "GE-AX-");
                    ParamHelper.SetTextParam(e, "GE_Категорія", "3. Монтажні вироби");
                    ParamHelper.SetTextParam(e, "GE_Найменування", $"Аксесуар ({typeUa})");
                    ParamHelper.SetTextParam(e, "GE_Кількість", "1");
                    ParamHelper.SetTextParam(e, "DKC_Единица измерения", "шт.");

                    CountByCategory("3. Монтажні вироби");
                    _countProcessed++;
                    return;
                }

                // --- Имя семейства читаем из Dictionary<string, FamilyMapRecord> ---
                FamilyMapRow fm;

                bool fmOk = _familyMap.TryGetValue(famName, out fm);
                if (!fmOk)
                {
                    if (!IsAccessory(famName)) // аксессуары пропускаем без ошибки
                        Log.Warn($"[MAP] Немає мапінгу для сімейства '{famName}' (ElementId={e.Id})");

                    // всё равно нельзя продолжать дальше – нет мапінгу => выходим
                    return;
                }

                string baseArticle = fm.BaseArticle ?? "";
                string category = fm.Category ?? "";
                // --- Размеры + угол (для большинства правил нужны) ---
                int w, h;
                double? ang;
                SizeHelper.GetWidthHeightAndAngle(e, out w, out h, out ang);

                // 1) РЕДУКЦІЇ R / RL / RR — пріоритет №1
                if (famName.Equals("470_DKC_S5_Lightweight Reducer", StringComparison.OrdinalIgnoreCase))
                {
                    // ----- Перевірка висоти -----
                    int hReducer;
                    try { hReducer = GetIntParam(e, "DKC_ВысотаЛотка"); }
                    catch { return; }
                    if (hReducer != 100)
                    {
                        var msg = $"Редукція: висота {hReducer} мм відсутня в БД (допустимо тільки 100) (ElementId={e.Id})";
                        Log.Error("[REDUCER] " + msg);
                        _errors.Add(msg);
                        return;
                    }

                    // ----- Перевірка другої ширини -----
                    int w2;
                    try { w2 = GetIntParam(e, "DKC_ШиринаЛотка2"); }
                    catch { return; }
                    if (w2 <= 100)
                    {
                        var msg = $"Редукція: друга ширина {w2} мм недопустима (має бути > 100 мм) (ElementId={e.Id})";
                        Log.Error("[REDUCER] " + msg);
                        _errors.Add(msg);
                        return;
                    }

                    // ----- Конвертація ширин у см -----
                    int w1cm = 10;                 // перша ширина завжди 100 мм → 10 см
                    int w2cm = w2 / 10;            // друга ширина у см

                    // ----- Визначення напрямку -----
                    string rType = "R"; // Симетрична за замовчуванням
                    var fi = e as FamilyInstance;
                    if (fi != null)
                    {
                        foreach (Parameter p in fi.Parameters)
                        {
                            if (p.Definition != null && p.Definition.Name.Equals("Left", StringComparison.OrdinalIgnoreCase) && p.AsInteger() == 1)
                                rType = "RL";
                            if (p.Definition != null && p.Definition.Name.Equals("Right", StringComparison.OrdinalIgnoreCase) && p.AsInteger() == 1)
                                rType = "RR";
                        }
                    }

                    // ----- Постійна товщина -----
                    string thick = "2,0";  // FIXED

                    // ----- Покриття -----
                    string metalCode = ArticleFormat.Metal(_metal); // PG або HDG

                    // ----- Формування артикула -----
                    string reducerArticle = $"GE-KT2-{rType}-{w1cm}-{w2cm}-A100-{thick}-{metalCode}";

                    ParamHelper.SetTextParam(e, "GE_Артикул", reducerArticle);
                    ParamHelper.SetTextParam(e, "GE_Категорія", "2. З'єднувальні деталі");
                    //ParamHelper.SetTextParam(e, "GE_Кількість", "2"); // Редукція у комплекті — 2 шт.

                    // ----- Найменування з БД (якщо є) -----
                    CatalogRow row;
                    int found = _catalog.TryGetByArticleExact(reducerArticle, out row);
                    if (found == 1)
                        ParamHelper.SetTextParam(e, "GE_Найменування", row.spec_description);
                    else if (found == 0)
                        Log.Warn($"[DB] Не знайдено в каталозі: {reducerArticle}");
                    else
                        Log.Warn($"[DB] Дублікатів {found}: {reducerArticle}");

                    //Log.Info($"[REDUCER] {reducerArticle}");
                    CountByCategory("2. З'єднувальні деталі");
                    _countProcessed++;
                    return;
                }

                // 2) З’ЄДНУВАЧІ YDE / SDE
                if (famName.Equals("470_DKC_S5_Horizontal Bend_CPO0-45", StringComparison.OrdinalIgnoreCase) ||
                    famName.Equals("470_DKC_S5_Int Vertical Bend_1-89", StringComparison.OrdinalIgnoreCase) ||
                    famName.Equals("470_DKC_S5_Ext Vertical Bend_1-89", StringComparison.OrdinalIgnoreCase))
                {
                    // читаем высоту именно из параметра
                    int hYde;
                    try
                    {
                        hYde = GetIntParam(e, "DKC_ВысотаЛотка");
                    }
                    catch
                    {
                        // ошибка уже залогирована и добавлена в _errors
                        return;
                    }

                    if (hYde != 50 && hYde != 100)
                    {
                        var msg = $"З'єднувач {famName}: висота {hYde} мм відсутня в БД (ElementId={e.Id})";
                        Log.Error("[YDE/SDE] " + msg);
                        _errors.Add(msg);
                        return;
                    }

                    // толщина фиксируется по высоте (комбик игнорируем)
                    string thick = (hYde == 50) ? "1,5" : "2,0";

                    // металл — из комбика, в код
                    string metalCode = ArticleFormat.Metal(_metal);

                    // определяем какой артикул собирать
                    string ydeSdeArticle = null;
                    if (famName.Equals("470_DKC_S5_Horizontal Bend_CPO0-45", StringComparison.OrdinalIgnoreCase))
                    {
                        // всегда GE-YDE-
                        ydeSdeArticle = $"GE-YDE-{hYde}-{thick}-{metalCode}";
                    }
                    else
                    {
                        // для Int/Ext Vertical Bend — только если GE_Варіант = SDE
                        if (IsVariantSde(e))
                        {
                            ydeSdeArticle = $"GE-SDE-{hYde}-{thick}-{metalCode}";
                        }
                        // иначе не возвращаемся — элемент отработает в Default (OBF/IBF)
                    }

                    if (!string.IsNullOrEmpty(ydeSdeArticle))
                    {
                        ParamHelper.SetTextParam(e, "GE_Артикул", ydeSdeArticle);
                        ParamHelper.SetTextParam(e, "GE_Категорія", "2. З'єднувальні деталі");

                        // найменування из БД (если есть)
                        CatalogRow row;
                        int found = _catalog.TryGetByArticleExact(ydeSdeArticle, out row);
                        if (found == 1)
                            ParamHelper.SetTextParam(e, "GE_Найменування", row?.spec_description ?? "");
                        else if (found == 0)
                            Log.Warn($"[DB] Не знайдено: {ydeSdeArticle}");
                        else
                            Log.Warn($"[DB] Дублікати ({found} записів): {ydeSdeArticle}");

                        // единицы/количество по категории
                        ApplyQuantityAndUnitByCategory(e, "2. З'єднувальні деталі");

                        //Log.Info($"[YDE/SDE] {ydeSdeArticle}");
                        CountByCategory("2. З'єднувальні деталі");
                        _countProcessed++;
                        return;
                    }
                }

                // 3) ACCESSORIES — збираємо та тимчасово додаємо в специфікацію
                if (famName.Equals("999_DKC_Accessories", StringComparison.OrdinalIgnoreCase))
                {
                    SaveAccessoryInfo(e); // зберегти у accessories_raw.ini

                    // Запис у специфікаційні параметри
                    ParamHelper.SetTextParam(e, "GE_Артикул", "GE-AX-");
                    ParamHelper.SetTextParam(e, "GE_Категорія", "3. Монтажні вироби");
                    ParamHelper.SetTextParam(e, "GE_Найменування", "Невідомий аксесуар");
                    ParamHelper.SetTextParam(e, "GE_Кількість", "1");
                    ParamHelper.SetTextParam(e, "DKC_Единица измерения", "шт.");

                    CountByCategory("3. Монтажні вироби");
                    _countProcessed++;
                    return;
                }

                // 4) DEFAULT – стандартна побудова артикула
                {
                    string cat = string.IsNullOrWhiteSpace(fm.Category) || fm.Category == "-"
                        ? "4. Інші"
                        : fm.Category;

                    string finalArticle = ArticleBuilder.BuildArticle(baseArticle, w, h, _thicknessMm, _metal, ang);
                    if (string.IsNullOrEmpty(finalArticle))
                    {
                        AddError($"[ARTICLE] неможливо зібрати артикул (ElementId={e.Id})");
                        return;
                    }

                    ParamHelper.SetTextParam(e, "GE_Артикул", finalArticle);
                    ParamHelper.SetTextParam(e, "GE_Категорія", cat);

                    ApplyQuantityAndUnitByCategory(e, cat);

                    CatalogRow row;
                    int found = _catalog.TryGetByArticleExact(finalArticle, out row);
                    if (found == 1)
                        ParamHelper.SetTextParam(e, "GE_Найменування", row?.spec_description ?? "");
                    else if (found == 0)
                        Log.Warn($"[DB] не знайдено в БД: {finalArticle}");
                    else if (found > 1)
                        Log.Warn($"[DB] дублікати ({found}): {finalArticle}");

                    CountByCategory(cat);
                    _countProcessed++;
                    return;
                }

            }

            catch (Exception ex)
            {
                Log.Error($"[EX] ProcessOne: {ex.Message}");
                _countErrors++;
            }
        }

        // Количество и единицы измерения строго по GE_Категорія:
        // 1.* → метры (0),  2.* → штуки (1),  3.* → штуки (1).
        // чтобы формула спецификации (деление на 1000 мм) сработала.
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

        // --- Варианты спецификации по параметру GE_Варіант ---
        private bool IsVariantSde(Element e)
        {
            try
            {
                var s = e.LookupParameter("GE_Варіант")?.AsString();
                if (string.IsNullOrWhiteSpace(s))
                    return false;
                return string.Equals(s.Trim(), "SDE", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // визначаємо, що цей елемент є аксесуаром
        private bool IsAccessory(string familyName)
        {
            return familyName.Equals("999_DKC_Accessories", StringComparison.OrdinalIgnoreCase);
        }

        // переклад типу аксесуара з англійської на українську
        private string TranslateAccessoryType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return "Невідомий";

            typeName = typeName.Trim();

            switch (typeName.ToLower())
            {
                case "bolt":
                    return "Болт";
                case "nut":
                    return "Гайка";
                case "plate":
                case "plain":
                    return "Пластина";
                case "holder":
                    return "Тримач";
                default:
                    return typeName; // если неизвестно — оставим оригинал
            }
        }

        // очищення параметрів SPECIFIC перед обробкою елемента
        private void ResetGE(Element e)
        {
            try
            {
                ParamHelper.SetTextParam(e, "GE_Артикул", "");
                ParamHelper.SetTextParam(e, "GE_Найменування", "");
                ParamHelper.SetTextParam(e, "GE_Категорія", "");
                ParamHelper.SetTextParam(e, "GE_Кількість", "");
                ParamHelper.SetTextParam(e, "DKC_Единица измерения", ""); // очищаємо щоб не тягнувся мусор
                                                                          // GE_Варіант не чіпаємо – заповнює користувач
            }
            catch { }
        }

        private void CountByCategory(string category)
        {
             if (string.IsNullOrWhiteSpace(category))
                 return;
             if (category.StartsWith("1.", StringComparison.Ordinal))
                 _countCat1++;
             else if (category.StartsWith("2.", StringComparison.Ordinal))
                 _countCat2++;
             else if (category.StartsWith("3.", StringComparison.Ordinal))
                 _countCat3++;
             else
                 _countCatOther++;
        }

        // Безопасное чтение целочисленного параметра по имени (через AsValueString)
        private int GetIntParam(Element e, string paramName)
        {
            var p = e.LookupParameter(paramName);
            if (p == null)
            {
                var msg = $"Параметр '{paramName}' не знайдено (ElementId={e.Id})";
                Log.Error("[PARAM] " + msg);
                _errors.Add(msg);
                throw new InvalidOperationException(msg);
            }
            try
            {
                var s = p.AsValueString();
                return ArticleFormat.ParseInt(s);
            }
            catch (Exception ex)
            {
                var msg = $"'{paramName}': {ex.Message} (ElementId={e.Id})";
                Log.Error("[PARAM] " + msg);
                _errors.Add(msg);
                throw;
            }
        }

        private void SaveAccessoryInfo(Element e)
        {
            try
            {
                // путь рядом с Message.log
                string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string filePath = Path.Combine(exeDir, "accessories_raw.ini");

                // если файл новый — добавляем заголовок
                if (!File.Exists(filePath))
                {
                    File.AppendAllLines(filePath, new[]
                    {
                "; ------------------------------------------",
                $"; accessories_raw.ini  (auto-collected raw data)",
                $"; Created: {DateTime.Now}",
                "; ------------------------------------------",
                ""
            });
                }

                int id = e.Id.IntegerValue;
                string section = $"[Element_{id}]";

                // читаем файл
                var lines = File.ReadAllLines(filePath).ToList();

                // если запись для этого элемента уже есть — ничего не делаем
                if (lines.Contains(section))
                    return;

                // собираем данные
                string fam = ElementHelper.GetFamilyName(e);
                string type = (e is FamilyInstance fi) ? fi.Symbol?.Name ?? "-" : "-";

                // Bounding box
                string bbox = "-";
                BoundingBoxXYZ bb = e.get_BoundingBox(null);
                if (bb != null)
                    bbox = $"({bb.Min.X:0.##},{bb.Min.Y:0.##},{bb.Min.Z:0.##}) - ({bb.Max.X:0.##},{bb.Max.Y:0.##},{bb.Max.Z:0.##})";

                // Location
                string location = "-";
                if (e.Location is LocationPoint lp)
                    location = $"({lp.Point.X:0.##},{lp.Point.Y:0.##},{lp.Point.Z:0.##})";

                // Material
                string material = "-";
                var pMat = e.LookupParameter("Material");
                if (pMat != null)
                    material = pMat.AsValueString();

                // Comment
                string comment = "-";
                var pComment = e.LookupParameter("Комментарии");
                if (pComment != null)
                    comment = pComment.AsString();

                // Host
                string host = "-";
                if (e is FamilyInstance fi2 && fi2.Host != null)
                    host = fi2.Host.Name;

                // Level
                string level = "-";
                var pLevel = e.LookupParameter("Уровень");
                if (pLevel != null)
                    level = pLevel.AsString();

                // записываем новый блок
                lines.Add(section);
                lines.Add($"Family = {fam}");
                lines.Add($"Type = {type}");
                lines.Add($"BoundingBox = {bbox}");
                lines.Add($"Location = {location}");
                lines.Add($"Material = {material}");
                lines.Add($"Comment = {comment}");
                lines.Add($"Host = {host}");
                lines.Add($"Level = {level}");
                lines.Add("");

                File.WriteAllLines(filePath, lines);
                Log.Info($"[ACCESSORY] collected raw info (ElementId={id})");
            }
            catch (Exception ex)
            {
                Log.Error($"[ACCESSORY] SaveAccessoryInfo failed: {ex.Message} (ElementId={e.Id})");
            }
        }

        private void AddError(string message)
        {
            Log.Error(message);   // подробности только в логе
            _countErrors++;
        }
    }


// --------- ВСПОМОГАТЕЛЬНЫЕ ТИПЫ  ---------
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

    public static class ParamHelper
    {
        public static void SetTextParam(Element e, string paramName, string value)
        {
            if (e == null) return;

            // Сначала ищем параметр у экземпляра
            Parameter p = e.LookupParameter(paramName);

            // Если не найден, пробуем у типа (для GE_Кількість и подобных)
            if (p == null && e is FamilyInstance fi)
                p = fi.Symbol?.LookupParameter(paramName);

            if (p == null || p.IsReadOnly) return;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        p.Set(value ?? "");
                        break;

                    case StorageType.Integer:
                        if (int.TryParse(value, out int intVal))
                            p.Set(intVal);
                        break;

                    case StorageType.Double:
                        if (double.TryParse(value, out double dblVal))
                            p.Set(dblVal);
                        break;

                    default:
                        // Игнорируем другие типы
                        break;
                }
            }
            catch (Exception)
            {
                // Можно логировать ошибку, если нужно
                // Log.Warn($"Не удалось записать параметр {paramName} для элемента {e.Id}: {ex.Message}");
            }
        }
    }


}
