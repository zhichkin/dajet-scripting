
DECLARE @Таблица array

USE 'MS_TEST'

  WITH Товар AS
  (
    SELECT TOP 3 Код
         , Наименование
         , НомерПоПорядку = ROW_NUMBER() OVER (ORDER BY Код)
      FROM Справочник.Номенклатура
     ORDER BY НомерПоПорядку DESC
  )
  SELECT Код, Наименование, ВложенныйЗапрос.НомерПоПорядку
    INTO @Таблица
    FROM (SELECT Код, Наименование, НомерПоПорядку FROM Товар) AS ВложенныйЗапрос
END

RETURN @Таблица