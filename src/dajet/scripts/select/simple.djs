# STARTUP
# LONG_TASK
# SINGLETON('1234567890')

DECLARE @Код string = 'MS-07'

PRIVATE @Таблица array

USE 'MS_TEST'

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

END

PRINT JSON(@Таблица)

RETURN @Таблица