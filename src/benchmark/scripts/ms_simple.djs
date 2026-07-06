
DECLARE @Код string  = 'MS-001'

PRIVATE @Таблица array

USE 'MS_TEST'

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

END

RETURN @Таблица