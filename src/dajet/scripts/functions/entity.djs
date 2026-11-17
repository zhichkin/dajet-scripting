
DECLARE @input uuid = 'fd9f4c65-a13f-11f1-99f7-88b1112c8f1b' -- MS
--DECLARE @input uuid = '1ad9dc94-a0a4-11f1-99f7-88b1112c8f1b' -- PG

PRIVATE @Ссылка entity
PRIVATE @ЕщёСсылка entity
PRIVATE @ТипСсылки integer = 76 -- 71
PRIVATE @Таблица array

SET @Ссылка = ENTITY(@ТипСсылки, UUIDDB(@input))
SET @ЕщёСсылка = ENTITY(53, UUIDDB('7d5eab72-9576-11f1-99f7-88b1112c8f1b'))
--SET @ЕщёСсылка = ENTITY(70, UUIDDB('034cdf4b-9580-11f1-99f7-88b1112c8f1b'))

PRINT 'INPUT = ' + @input
PRINT 'Ссылка = ' + @Ссылка
PRINT 'ЕщёСсылка = ' + @ЕщёСсылка

USE 'MS_TEST' -- 'PG_TEST'

  SELECT Наименование
       , Ссылка, Ссылка1С = UUID1C(Ссылка)
       , МногоСсылок, МногоСсылок1С = UUID1C(МногоСсылок)
       , ENUM_DB = Перечисление.ВидНоменклатуры.Товар
       , ENUM_1C = UUID1C(Перечисление.ВидНоменклатуры.Товар)
       , UUID_NEW = NEWUUID()
    INTO @Таблица
    FROM Справочник.Тестовый
   WHERE Ссылка = @Ссылка
     AND МногоСсылок = @ЕщёСсылка

END

RETURN @Таблица