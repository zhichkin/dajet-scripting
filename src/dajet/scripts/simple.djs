
DECLARE @Код string  = 'MS-10'

PRIVATE @Таблица array

USE 'MS_TEST'

  SELECT Ссылка, Код, Наименование
       , Идентификатор = NEWUUID()
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

END

--SLEEP 1

RETURN @Таблица --JSON(@Таблица)