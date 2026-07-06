
DECLARE @Код string  = 'PG-001'

PRIVATE @Таблица array

USE 'PG_TEST'

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

END

RETURN @Таблица