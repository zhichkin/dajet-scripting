
DECLARE @Код     string = 'MS-01'
PRIVATE @Таблица array

USE 'MS_TEST'

  SELECT Номер = 1
       , Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

  UNION ALL

  SELECT 2, Ссылка, Код, Наименование
    FROM Справочник.Номенклатура
   WHERE Код = 'MS-02'

  UNION ALL

  SELECT 3, Ссылка, Код, Наименование
    FROM Справочник.Номенклатура
   WHERE Код = 'MS-03'

  UNION ALL

  SELECT 4, Ссылка, Код, Наименование
    FROM Справочник.Номенклатура

  ORDER BY Номер DESC

END

RETURN @Таблица