
DECLARE @Код string = 'MS-10'

PRIVATE @Таблица array

USE 'MS_TEST'

  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

END -- Фиксирует транзакцию

IF (@Код = 'MS-10') THEN PRINT 'TRUE' ELSE PRINT 'FALSE' END

TRY
  THROW 'test exception'
CATCH
  PRINT 'ERROR: ' + ERROR_MESSAGE()
FINALLY
  PRINT 'FINALLY BLOCK'
END

RETURN @Таблица