
DECLARE @Ссылка uuid = 'fd9f4c65-a13f-11f1-99f7-88b1112c8f1b' -- MS
--DECLARE @Ссылка uuid = '1ad9dc94-a0a4-11f1-99f7-88b1112c8f1b' -- PG

PRIVATE @Таблица array

PRINT 'INPUT = ' + @Ссылка

USE 'MS_TEST' --'PG_TEST'

  WITH cte AS
  (
    SELECT Наименование
         , Ссылка, Ссылка1С = UUID1C(Ссылка)
         , МногоСсылок, МногоСсылок1С = UUID1C(МногоСсылок)
         , ENUM_DB = Перечисление.ВидНоменклатуры.Товар
      FROM Справочник.Тестовый
     WHERE Ссылка = UUIDDB(@Ссылка)
  )
  SELECT Наименование
       , Ссылка, Ссылка1С
       , МногоСсылок, МногоСсылок1С
       , UUID_1C = UUID1C(Ссылка)
       , UNION_1C = UUID1C(МногоСсылок)
       , ENUM_DB = UUID1C(ENUM_DB)
    INTO @Таблица
    FROM cte

END

RETURN @Таблица