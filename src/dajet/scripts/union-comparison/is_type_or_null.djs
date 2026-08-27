
PRIVATE @Таблица array

USE 'MS_TEST'
  
  SELECT Ссылка, Код, Наименование, СоставнойТип
    INTO @Таблица
    FROM Справочник.Тестовый
   WHERE NOT СоставнойТип IS NULL
     AND СоставнойТип IS boolean
    -- AND СоставнойТип IS decimal
    -- AND СоставнойТип IS datetime
    -- AND СоставнойТип IS string
    -- AND СоставнойТип IS Справочник.Номенклатура
    -- AND СоставнойТип IS Перечисление.ВидНоменклатуры
    -- ОднаСсылка IS Справочник.Номенклатура

END

RETURN @Таблица