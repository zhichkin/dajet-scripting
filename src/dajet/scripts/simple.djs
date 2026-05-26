
DECLARE @Таблица array

DECLARE @Код string  = '000000001'

USE 'MS_TEST'

  SELECT Ссылка, Код, Наименование
       , Идентификатор = NEWUUID()
       , УникальныйИдентификатор
    INTO @Таблица
    FROM Справочник.Справочник1
   WHERE Код = @Код

END

RETURN JSON(@Таблица)