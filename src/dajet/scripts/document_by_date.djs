
DECLARE @Таблица array

DECLARE @ДатаДокумента datetime = '2026-05-08T10:40:02'

USE 'PG_TEST'

  SELECT Ссылка, Дата, Номер
       , Реквизит1, Реквизит2, Реквизит3
    INTO @Таблица
    FROM Документ.Документ1
   WHERE Дата = @ДатаДокумента

END

RETURN JSON(@Таблица)