
DECLARE @Код     string = '000000001'
DECLARE @Данные  object
DECLARE @Таблица array

USE 'MS_TEST'
  
  SELECT Ссылка, Код, Наименование, ПометкаУдаления
    INTO @Данные
    FROM Справочник.Справочник1
   WHERE Код = @Код

  SELECT Ссылка, Код, Наименование, ПометкаУдаления
    INTO @Таблица
    FROM Справочник.Справочник1
   WHERE Код = @Код

END

SET @Данные.Таблица = @Таблица