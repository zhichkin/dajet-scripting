
DECLARE @Таблица array

DECLARE @Код string = '000000001'
DECLARE @Булево          boolean
DECLARE @ЦелоеЧисло      integer
DECLARE @БольшоеЧисло    integer(8)
DECLARE @ДесятичноеЧисло decimal
DECLARE @ДатаВремя       datetime
DECLARE @Строка          string
DECLARE @ДвоичноеЧисло   binary
DECLARE @Идентификатор   uuid
DECLARE @ПустаяСсылка    entity

USE 'MS_TEST' -- 'PG_TEST'

  SELECT Ссылка, Код, Наименование
       , Булево          = @Булево
       , ЦелоеЧисло      = @ЦелоеЧисло
       , БольшоеЧисло    = @БольшоеЧисло
       , ДесятичноеЧисло = @ДесятичноеЧисло
       , ДатаВремя       = @ДатаВремя
       , Строка          = @Строка
       , ДвоичноеЧисло   = @ДвоичноеЧисло
       , Идентификатор   = @Идентификатор
       , ПустаяСсылка    = @ПустаяСсылка
    INTO @Таблица
    FROM Справочник.Справочник1
   WHERE Код = @Код
     AND Ссылка = @ПустаяСсылка

END

RETURN JSON(@Таблица)