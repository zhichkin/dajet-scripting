# STARTUP
# LONG_TASK
# DISPLAY ('select-simple.djs')

DECLARE @Код string = 'MS-07'

PRIVATE @Таблица array

USE 'MS_TEST'

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

END

RETURN @Таблица