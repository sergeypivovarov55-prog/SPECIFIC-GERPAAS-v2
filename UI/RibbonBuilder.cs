using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
using SpecificGerpaas.Core;
using SpecificGerpaas.Utils;
using System.Linq;
using System.Reflection;

namespace SpecificGerpaas
{
    public static class RibbonBuilder
    {
        public static void Build(UIControlledApplication app)
        {
            string version = Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                .Version ?? "0.0.0.0";

            string tabName = "СПП";
            try { app.CreateRibbonTab(tabName); } catch { }
            RibbonPanel panel = app.CreateRibbonPanel(tabName, $"Cпецифікації GERPAAS");

            // --- Группа из двух комбиков в "стеке" ---
            var comboThkData = new ComboBoxData("SGP_Thickness");
            var comboMetalData = new ComboBoxData("SGP_Metal");

            // Добавляем оба комбика в один стек
            DkcComboManager.Init();
            var stacked = panel.AddStackedItems(comboThkData, comboMetalData);

            //
            // --- Комбо-бокс толщин ---
            //
            var comboThk = stacked[0] as ComboBox;
            comboThk.ToolTip = "Выберіть товщину";

            var thkList = DkcComboManager.GetThkList()
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            // Если список пуст, добавляем хотя бы одно значение по умолчанию
            if (thkList.Count == 0)
            {
                thkList.Add("1,0 мм");
                Log.Warn("[RibbonBuilder] Список товщин пустий — добавлено значення 1,0 мм");
            }

            foreach (var t in thkList)
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    Log.Warn("[RibbonBuilder] Пропущено пусте значення товщини");
                    continue;
                }

                string cleanText = t.Trim();
                string cleanId = "Thk_" + cleanText.Replace(" ", "_")
                                                   .Replace("|", "_")
                                                   .Replace(",", "_")
                                                   .Replace(".", "_");

                comboThk.AddItem(new ComboBoxMemberData(cleanId, cleanText));
            }

            // Текущее значение
            var thkCur = DkcComboManager.GetThkCurrent();
            var thkItem = comboThk.GetItems()
                .FirstOrDefault(x => x.ItemText == thkCur)
                ?? comboThk.GetItems()[0];
            comboThk.Current = thkItem;

            // Обновление INI при изменении
            comboThk.CurrentChanged += (s, e) =>
            {
                var cur = comboThk.Current?.ItemText ?? "";
                if (!string.IsNullOrWhiteSpace(cur))
                    DkcComboManager.SaveThkCurrent(cur);
            };

            //
            // --- Комбо-бокс покрытий ---
            //
            var comboMetal = stacked[1] as ComboBox;
            comboMetal.ToolTip = "Выберіть покриття";

            var coatList = DkcComboManager.GetCoatList()
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            // Если список пуст, добавляем значение по умолчанию
            if (coatList.Count == 0)
            {
                coatList.Add("Сендзимир");
                Log.Warn("[RibbonBuilder] Список покриттів пустий — добавлено значення Сендзимир");
            }

            foreach (var c in coatList)
            {
                if (string.IsNullOrWhiteSpace(c))
                {
                    Log.Warn("[RibbonBuilder] Пропущено пусте значення покриття");
                    continue;
                }

                string cleanText = c.Trim();
                string cleanId = "Coat_" + cleanText.Replace(" ", "_")
                                                    .Replace("|", "_")
                                                    .Replace(",", "_")
                                                    .Replace(".", "_");

                comboMetal.AddItem(new ComboBoxMemberData(cleanId, cleanText));
            }

            // Текущее значение
            var coatCur = DkcComboManager.GetCoatCurrent();
            var coatItem = comboMetal.GetItems()
                .FirstOrDefault(x => x.ItemText == coatCur)
                ?? comboMetal.GetItems()[0];
            comboMetal.Current = coatItem;

            DkcComboManager.ThicknessCombo = comboThk;
            DkcComboManager.CoatingCombo = comboMetal;


            // Обновление INI при изменении
            comboMetal.CurrentChanged += (s, e) =>
            {
                var cur = comboMetal.Current?.ItemText ?? "";
                if (!string.IsNullOrWhiteSpace(cur))
                    DkcComboManager.SaveCoatCurrent(cur);
            };

            // --- Кнопка "Специфікація із DKC" ---
            var pbdSyncDkc = new PushButtonData(
                "SGP_SyncSpecsDkc",
                "GERPAAS",
                Assembly.GetExecutingAssembly().Location,
                "SpecificGerpaas.Commands.CmdSyncOrCreateSpecsDkc");

            var btnSyncDkc = panel.AddItem(pbdSyncDkc) as PushButton;
            btnSyncDkc.ToolTip = "Заповнити специфікацію GERPAAS із елементів DKC";

            btnSyncDkc.LargeImage = ImageLoader.FromEmbedded(
                "SpecificGerpaas.Resources.GERP32.png",
                "Resources.GERP32.png",
                "GERP32.png");

            btnSyncDkc.Image = ImageLoader.FromEmbedded(
                "SpecificGerpaas.Resources.GERP16.png",
                "Resources.GERP16.png",
                "GERP16.png");
        
            // --- Кнопка "Експорт специфікації" ---
            var pbdExp = new PushButtonData(
                "SGP_ExportSpecs",
                "Excel",
                Assembly.GetExecutingAssembly().Location,
                "SpecificGerpaas.Commands.CmdExportGerpaasSpec");

            var btnExp = panel.AddItem(pbdExp) as PushButton;
            btnExp.ToolTip = "Експортувати специфікацію GERPAAS в Excel";

            btnExp.LargeImage = ImageLoader.FromEmbedded(
                "SpecificGerpaas.Resources.Specification32.png",
                "Resources.Specification32.png",
                "Specification32.png");

            btnExp.Image = ImageLoader.FromEmbedded(
                "SpecificGerpaas.Resources.Specification16.png",
                "Resources.Specification16.png",
                "Specification16.png");
        }
    }
}
