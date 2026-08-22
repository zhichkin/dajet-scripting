
DECLARE @Период datetime  = '2026-01-01T12:34:56'

PRIVATE @НачалоМесяца    datetime
PRIVATE @СледующийМесяц  datetime
PRIVATE @ПредыдущийМесяц datetime
PRIVATE @КонецМесяца     datetime
PRIVATE @Результат       object
PRIVATE @Таблица         array

SET @НачалоМесяца    = DATESTART('MONTH', @Период)
SET @КонецМесяца     = DATEEND  ('MONTH', @Период)
SET @СледующийМесяц  = DATEADD('MONTH',  1, @Период)
SET @ПредыдущийМесяц = DATEADD('MONTH', -1, @Период)

SET @Результат.Параметр                 = @Период
SET @Результат.НачалоМесяца             = @НачалоМесяца
SET @Результат.КонецМесяца              = @КонецМесяца
SET @Результат.СледующийМесяц           = @СледующийМесяц
SET @Результат.НачалоСледующегоМесяца   = DATEADD('MONTH',  1, DATESTART('MONTH', @Период))
SET @Результат.ПредыдущийМесяц          = @ПредыдущийМесяц
SET @Результат.НачалоПредыдующегоМесяца = DATEADD('MONTH', -1, DATESTART('MONTH', @Период))

--RETURN @Результат

USE 'PG_TEST'

  SELECT Дата, Номер
       , МинусМесяц              = DATEADD('MONTH', -1, Дата)
       , ПлюсМесяц               = DATEADD('MONTH',  1, Дата)
       , НачалоПредыдущегоМесяца = DATEADD('MONTH', -1, DATESTART('MONTH', Дата))
       , НачалоСледующегоМесяца  = DATEADD('MONTH',  1, DATESTART('MONTH', Дата))
       , НачалоГода     = DATESTART('YEAR',    Дата)
       , НачалоКвартала = DATESTART('QUARTER', Дата)
       , НачалоМесяца   = DATESTART('MONTH',   Дата)
       , НачалоДня      = DATESTART('DAY',     Дата)
       , НачалоЧаса     = DATESTART('HOUR',    Дата)
       , НачалоМинуты   = DATESTART('MINUTE',  Дата)
       , НачалоСекунды  = DATESTART('SECOND',  Дата)
       , КонецГода      = DATEEND('YEAR',    Дата)
       , КонецКвартала  = DATEEND('QUARTER', Дата)
       , КонецМесяца    = DATEEND('MONTH',   Дата)
       , КонецДня       = DATEEND('DAY',     Дата)
       , КонецЧаса      = DATEEND('HOUR',    Дата)
       , КонецМинуты    = DATEEND('MINUTE',  Дата)
       , КонецСекунды   = DATEEND('SECOND',  Дата)
    INTO @Таблица
    FROM Документ.Расход
   ORDER BY Дата ASC

END

SET @Результат.Таблица = @Таблица

RETURN @Результат