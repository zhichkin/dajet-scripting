
DECLARE @Ссылка uuid = 'fd9f4c65-a13f-11f1-99f7-88b1112c8f1b' -- MS
--DECLARE @Ссылка uuid = '1ad9dc94-a0a4-11f1-99f7-88b1112c8f1b' -- PG

PRIVATE @Таблица array

PRINT 'INPUT = ' + @Ссылка

USE 'MS_TEST' -- 'PG_TEST'

  SELECT Наименование = T.Наименование
       , Ссылка = T.Ссылка, Ссылка1С = T.Ссылка1С
       , МногоСсылок = T.МногоСсылок, МногоСсылок1С = T.МногоСсылок1С
       , UUID_1C = UUID1C(T.Ссылка)
       , UNION_1C = UUID1C(T.МногоСсылок)
       , ENUM_DB = UUID1C(T.ENUM_DB)
    INTO @Таблица
    FROM (SELECT Наименование
               , Ссылка, Ссылка1С = UUID1C(Ссылка)
               , МногоСсылок, МногоСсылок1С = UUID1C(МногоСсылок)
               , ENUM_DB = Перечисление.ВидНоменклатуры.Товар
            FROM Справочник.Тестовый
           WHERE Ссылка = UUIDDB(@Ссылка)) AS T

END

RETURN @Таблица