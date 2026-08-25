
DECLARE @ДатаВремя datetime  = '2026-01-01T12:34:56'
PRIVATE @Результат object

USE 'MS_TEST'

  SELECT НачалоГода     = DATESTART('YEAR',    @ДатаВремя)
       , НачалоКвартала = DATESTART('QUARTER', @ДатаВремя)
       , НачалоМесяца   = DATESTART('MONTH',   @ДатаВремя)
       , НачалоДня      = DATESTART('DAY',     @ДатаВремя)
       , НачалоЧаса     = DATESTART('HOUR',    @ДатаВремя)
       , НачалоМинуты   = DATESTART('MINUTE',  @ДатаВремя)
       , НачалоСекунды  = DATESTART('SECOND',  @ДатаВремя)
       , Разделитель1   = '---'
       , КонецГода      = DATEEND('YEAR',    @ДатаВремя)
       , КонецКвартала  = DATEEND('QUARTER', @ДатаВремя)
       , КонецМесяца    = DATEEND('MONTH',   @ДатаВремя)
       , КонецДня       = DATEEND('DAY',     @ДатаВремя)
       , КонецЧаса      = DATEEND('HOUR',    @ДатаВремя)
       , КонецМинуты    = DATEEND('MINUTE',  @ДатаВремя)
       , КонецСекунды   = DATEEND('SECOND',  @ДатаВремя)
       , Разделитель2   = '---'
       , МинусМесяц              = DATEADD('MONTH', -1, @ДатаВремя)
       , ПлюсМесяц               = DATEADD('MONTH',  1, @ДатаВремя)
       , НачалоПредыдущегоМесяца = DATEADD('MONTH', -1, DATESTART('MONTH', @ДатаВремя))
       , НачалоСледующегоМесяца  = DATEADD('MONTH',  1, DATESTART('MONTH', @ДатаВремя))

    INTO @Результат

END

RETURN @Результат