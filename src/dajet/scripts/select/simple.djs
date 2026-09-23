--# STARTUP
--# LONG_TASK
--# SINGLETON 'Select product by code'

DECLARE @Код string = 'ФР-00000001'

PRIVATE @Таблица array

USE 'MS_UNF'

  SELECT Ссылка, Код, Наименование, ЭтоЭлемент --, ЭтоГруппа
    INTO @Таблица
    FROM Справочник.Номенклатура
   WHERE Код = @Код

END

--SLEEP 1

RETURN @Таблица