
DECLARE @Идентификатор uuid = '47ed21d5-2d08-4982-b3bd-e55a78404125'
DECLARE @Ссылка entity
DECLARE @Код    string = '000000001'
DECLARE @Запись object
DECLARE @Список array

USE 'MS_TEST'
  SELECT Булево = TRUE
       , ЦелоеЧисло = 123
       , ДесятичноеЧисло = 123.45
       , ДатаВремя = '2026-08-01T12:34:56'
       , Строка = 'Это строка'
       , Идентификатор = '47ed21d5-2d08-4982-b3bd-e55a78404125' -- CASE WHEN 1 = 0 THEN '47ed21d5-2d08-4982-b3bd-e55a78404125' ELSE NULL END
       , Ссылка, Владелец
       , Код, Наименование
       , ВерсияДанных -- = CASE WHEN ВерсияДанных IS NULL THEN ВерсияДанных ELSE NULL END
       , ПометкаУдаления
       , СоставнойТип
    INTO @Список -- @Запись
    FROM Справочник.Справочник1 AS T
   WHERE Код = @Код
  -- AND Ссылка = @Ссылка
END