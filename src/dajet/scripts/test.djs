
DECLARE @result object

DECLARE @Булево          boolean  = TRUE
DECLARE @ЦелоеЧисло      integer  = 12345
DECLARE @ДесятичноеЧисло decimal  = 12.34
DECLARE @ДатаВремя       datetime = '2026-08-01T12:34:56'
DECLARE @Строка          string   = 'Это строка'
DECLARE @ДвоичноеЧисло   binary   = 0x00000001
DECLARE @Идентификатор   uuid     = '643c6b9d-cacf-4048-11f1-3ce54d7b5bf7'
DECLARE @Ссылка          entity   = {333:643c6b9d-cacf-4048-11f1-3ce54d7b5bf7}

DECLARE @МассивUUID   array(uuid)
DECLARE @МассивСтрок  array(string) = [ "Строка 111", "Строка 222", "Строка 333" ]
DECLARE @МассивСсылок array(entity)

SET @result.Булево = @Булево
SET @result.ЦелоеЧисло = @ЦелоеЧисло
SET @result.ДесятичноеЧисло = @ДесятичноеЧисло
SET @result.ДатаВремя = @ДатаВремя
SET @result.Строка = @Строка
SET @result.Идентификатор = @Идентификатор
SET @result.Ссылка = @Ссылка
SET @result.МассивUUID = @МассивUUID
SET @result.МассивСтрок = @МассивСтрок
SET @result.МассивСсылок = @МассивСсылок

RETURN @result