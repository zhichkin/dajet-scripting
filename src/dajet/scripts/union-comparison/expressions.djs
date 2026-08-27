
PRIVATE @Таблица array
PRIVATE @Булево  boolean = FALSE
PRIVATE @Число   decimal = 123.0
PRIVATE @Дата    datetime = '2026-08-01T00:00:00'
PRIVATE @Строка  string = 'Это строка'
PRIVATE @Ссылка  entity = {53:b188f799-2c11-1b8f-11f1-95767d5eab72}

USE 'MS_TEST'
  
  SELECT Ссылка, Код, Наименование, СоставнойТип
    INTO @Таблица
    FROM Справочник.Тестовый
   WHERE NOT СоставнойТип IS NULL
     AND СоставнойТип = TYPEOF(@Ссылка)
     AND СоставнойТип = UUIDOF(@Ссылка)
     AND NOT СоставнойТип = TYPEOF(NULL) -- Проверка _TYPE = 0x01 (Неопределено)
     
     -- [TRANSPILER] [ComparisonOperatorTransformer] Unable to compare [Column: СоставнойТип] and CASE
     -- AND СоставнойТип = CASE WHEN 1 = 1 THEN 1 ELSE 0 END -- throws exception

     -- [TRANSPILER] [ComparisonOperatorTransformer] Unable to compare [Column: СоставнойТип] and ISNULL
     -- AND СоставнойТип = ISNULL(СоставнойТип, 'NULL')
END

RETURN @Таблица

-- Недокументированная возможнось, иногда используемая DaJet Script "под капотом"
-- WHERE (_Fld77_TYPE = 0x08 AND _Fld77_RTRef = @p0) -- СоставнойТип = TYPEOF(@Ссылка)
--   AND (_Fld77_TYPE = 0x08 AND _Fld77_RRRef = @p1) -- СоставнойТип = UUIDOF(@Ссылка)