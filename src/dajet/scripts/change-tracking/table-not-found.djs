
PRIVATE @Изменения array

USE 'MS_TEST'

  SELECT УзелОбмена, Ссылка
    INTO @Изменения
    FROM Справочник.Номенклатура.Изменения

END

RETURN @Изменения