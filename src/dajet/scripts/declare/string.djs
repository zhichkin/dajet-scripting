
DECLARE @result object

DECLARE @ДатаВремя     string = '2026-08-01T12:34:56'
DECLARE @Идентификатор string = '643c6b9d-cacf-4048-11f1-3ce54d7b5bf7'
DECLARE @Ссылка        string = {333:643c6b9d-cacf-4048-11f1-3ce54d7b5bf7}

SET @result.ДатаВремя     = @ДатаВремя
SET @result.Идентификатор = @Идентификатор
SET @result.Ссылка        = @Ссылка

RETURN @result