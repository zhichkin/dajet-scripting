
DECLARE @Таблица array

USE 'MS_TEST'

  SELECT Максимум = MAX(Товар.НомерПоПорядку)
    INTO @Таблица
    FROM (SELECT TOP 3 Код
               , Наименование
               , НомерПоПорядку = ROW_NUMBER() OVER (ORDER BY Код)
            FROM Справочник.Номенклатура) AS Товар
END

RETURN @Таблица