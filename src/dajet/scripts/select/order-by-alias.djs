
DECLARE @Таблица array

USE 'MS_TEST'

  SELECT TOP 3 Код
       , Наименование
       , НомерПоПорядку = ROW_NUMBER() OVER (ORDER BY Код)
    INTO @Таблица
    FROM Справочник.Номенклатура
   ORDER BY НомерПоПорядку DESC -- ROW_NUMBER() OVER (ORDER BY Код)

END

RETURN @Таблица