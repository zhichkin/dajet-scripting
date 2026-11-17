
DECLARE @Ссылка uuid = 'fd9f4c65-a13f-11f1-99f7-88b1112c8f1b' -- MS
--DECLARE @Ссылка uuid = '1ad9dc94-a0a4-11f1-99f7-88b1112c8f1b' -- PG

PRIVATE @Таблица array

PRINT 'INPUT = ' + @Ссылка

USE 'MS_TEST' -- 'PG_TEST'

  SELECT Наименование
       , Ссылка, Ссылка1С = UUID1C(Ссылка)
       , МногоСсылок, МногоСсылок1С = UUID1C(МногоСсылок)
       , ENUM_DB = Перечисление.ВидНоменклатуры.Товар
       , ENUM_1C = UUID1C(Перечисление.ВидНоменклатуры.Товар)
       , UUID_NEW = NEWUUID()
    INTO @Таблица
    FROM Справочник.Тестовый
   WHERE Ссылка = UUIDDB(@Ссылка)

END

RETURN @Таблица