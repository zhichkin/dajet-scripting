
PRIVATE @Таблица array

USE 'MS_TEST'

  SELECT Товар
    INTO @Таблица
    FROM Перечисление.ВидНоменклатуры
END

-- Incorrect syntax near the keyword 'FROM'.
-- 'Товар' - существующее значение перечисления.

RETURN @Таблица