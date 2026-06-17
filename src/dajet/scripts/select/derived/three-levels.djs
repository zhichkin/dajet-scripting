
DECLARE @Таблица array

USE 'MS_TEST'

  SELECT Код
    INTO @Таблица
    FROM (SELECT Код
            FROM (SELECT Код
                    FROM Справочник.Номенклатура) AS Уровень_1) AS Уровень_2
END

RETURN @Таблица