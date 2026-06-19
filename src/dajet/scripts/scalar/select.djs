
DECLARE @Булево          boolean  = TRUE
DECLARE @ЦелоеЧисло      integer  = 12345
DECLARE @ДесятичноеЧисло decimal  = 12.34
DECLARE @ДатаВремя       datetime = '2026-08-01T12:34:56'
DECLARE @Строка          string   = 'Это строка'
DECLARE @БинарныеДанные  binary   = '0x00000001'
DECLARE @Идентификатор   uuid     = '643c6b9d-cacf-4048-11f1-3ce54d7b5bf7'
DECLARE @Ссылка          entity   = '{333:643c6b9d-cacf-4048-11f1-3ce54d7b5bf7}'
DECLARE @СоставнойТип    union

DECLARE @Объект object
DECLARE @Таблица array

USE 'MS_TEST'

  SELECT Перечисление.ВидНоменклатуры.Товар INTO @Ссылка

  --SELECT COUNT(*) INTO @ЦелоеЧисло FROM Справочник.Номенклатура

END

RETURN @Ссылка