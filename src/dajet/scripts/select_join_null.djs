
DECLARE @Код    string = 'PG-000005'
PRIVATE @Таблица array

USE 'MS_TEST' -- 'PG_TEST'

     SELECT TOP 1 Т1.Наименование
          , Т2.Номенклатура, Т2.Регистратор
       INTO @Таблица
       FROM Справочник.Тестовый AS Т1
  LEFT JOIN РегистрНакопления.ОстаткиТовара AS Т2
         ON Т1.Ссылка = Т1.Ссылка --Т2.Регистратор
        --AND Т2.Код = @Код 

END

-- MS: Data is Null. This method or property cannot be called on Null values.
-- PG: Column 'УникальныйИдентификатор' is null.

RETURN @Таблица