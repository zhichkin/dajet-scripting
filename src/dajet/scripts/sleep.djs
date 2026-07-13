
--# STARTUP
# LONG_TASK
# SINGLETON 'Long task pulling test'

DECLARE @Код string = 'MS-01'

PRIVATE @Таблица array

USE 'MS_TEST'

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

END

SLEEP 10

RETURN @Таблица