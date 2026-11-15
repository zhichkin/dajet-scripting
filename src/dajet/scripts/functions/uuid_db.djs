
DECLARE @Ссылка uuid = '7e2a611f-a136-11f1-99f7-88b1112c8f1b' -- MS
--DECLARE @Ссылка uuid = 'e0ec3a07-a0a3-11f1-99f7-88b1112c8f1b' -- PG
DECLARE @Строка string

PRIVATE @UUID_DB uuid
PRIVATE @UUID_1C uuid
PRIVATE @Запись object

SET @Строка = '7e2a611f-a136-11f1-99f7-' + '88b1112c8f1b' -- MS
--SET @Строка = 'e0ec3a07-a0a3-11f1-99f7-' + '88b1112c8f1b' -- PG

PRINT 'Строка = ' + @Строка

SET @UUID_DB = UUIDDB(@Строка) -- UUIDDB(@Ссылка)

USE 'MS_TEST' -- 'PG_TEST'

  SELECT Ссылка, Наименование, Ссылка1С = UUID1C(Ссылка), TEST_UUID = NEWUUID()
    INTO @Запись
    FROM Справочник.Тестовый
   WHERE Ссылка = UUIDDB(@Строка) -- @UUID_DB

END

SET @UUID_1C = UUID1C(@Запись.Ссылка)

SET @Запись.INPUT = @Ссылка
SET @Запись.OUTPUT = @Запись.Ссылка
SET @Запись.UUID_DB = @UUID_DB
SET @Запись.UUID_1C = @UUID_1C

RETURN @Запись