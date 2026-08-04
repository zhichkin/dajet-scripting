
--#STARTUP

DECLARE @Код string  = 'MS-10'

PRIVATE @Таблица array

USE TRANSACTION 'MS_TEST'

  SELECT Ссылка, Код, Наименование
       , Идентификатор = NEWUUID()
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

  --IF @Код = '' THEN PRINT 'TEST IF' END

  --DELETE Справочник.Номенклатура WHERE Код = @Код

  --SLEEP 40

  --RETURN @Таблица -- Прерывает транзакцию

END -- Фиксирует транзакцию

--SLEEP 1

RETURN @Таблица --JSON(@Таблица)