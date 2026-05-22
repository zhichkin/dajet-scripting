
DECLARE @Таблица array

DECLARE @Код string  = '000000001'

USE 'PG_TEST'

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Справочник1
   WHERE Код = @Код

END

--RETURN JSON(@Таблица)