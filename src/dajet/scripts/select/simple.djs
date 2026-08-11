--# STARTUP
--# LONG_TASK
# SINGLETON 'Select product by code'

DECLARE @Код string = 'MS-07'

PRIVATE @Таблица array

USE 'MS_TEST'

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

END

SLEEP 1

RETURN @Таблица