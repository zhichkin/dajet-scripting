
PRIVATE @Источник object
PRIVATE @Приёмник object

USE 'MS_TEST'

  SELECT Ссылка, Код, Наименование
    INTO @Источник
    FROM Справочник.Тестовый
   WHERE Код = 'PG-000006' -- '000000008'

  USE 'PG_TEST'
    SELECT Ссылка, Код, Наименование
      INTO @Приёмник
      FROM Справочник.Тестовый
     WHERE Ссылка = @Источник.Ссылка
  END

END

PRINT JSON(@Источник)

RETURN @Приёмник