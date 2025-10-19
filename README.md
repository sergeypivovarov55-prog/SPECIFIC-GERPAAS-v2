# SPECIFIC-GERPAAS v2

## Опис
Плагін для Autodesk Revit 2022, що автоматизує створення специфікацій кабеленесучих систем GERPAAS на основі моделей DKC.  
Реалізує побудову артикулів, зчитування параметрів елементів, категоризацію та заповнення таблиць специфікації згідно з ДСТУ 9243.10:2023.

## Вимоги
- Autodesk Revit 2022  
- .NET Framework 4.8  
- Microsoft.Data.Sqlite 6.x  

## Установка
1. Зібрати проєкт у Visual Studio 2022.  
2. Скопіювати `SpecificGerpaas.addin` до  
   `%AppData%\Autodesk\Revit\Addins\2022\`  
3. Скопіювати зібрані DLL до однієї директорії з `.addin`.  
4. Перезапустити Revit.

## Структура
Commands/ – команди Revit API
Core/ – основна логіка (ArticleBuilder, CatalogSqlite, SizeHelper)
Data/ – GERP_param_map.ini, gerpaas.db
UI/ – Ribbon, кнопки, повідомлення
Utils/ – допоміжні функції
docs/ – AGENTS.md, ROADMAP.md, технічна документація

## Статус розробки
Активна фаза інтеграції модулів Core та Data.  
Поточна гілка — `refactor/structure`.

---

**Розробка:** Совпромпроект
