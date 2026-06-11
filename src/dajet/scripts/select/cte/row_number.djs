
DECLARE @Таблица array

USE 'MS_TEST'

  WITH Товар AS
  (
    SELECT TOP 3 Код
         , Наименование
         , НомерПоПорядку = ROW_NUMBER() OVER (ORDER BY Код)
      FROM Справочник.Номенклатура
  )
  SELECT Максимум = MAX(НомерПоПорядку)
    INTO @Таблица
    FROM Товар

END

RETURN @Таблица