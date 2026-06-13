
DECLARE @Таблица array

USE 'MS_TEST'
  SELECT Код
    INTO @Таблица
    FROM Справочник.Номенклатура AS T
   INNER JOIN Справочник.Номенклатура AS T
      ON T.Код = T.Код
   WHERE Код = @Код
END

RETURN @Таблица