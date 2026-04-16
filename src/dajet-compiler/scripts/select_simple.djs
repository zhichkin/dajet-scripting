DECLARE @Ссылка entity
DECLARE @Код    string = '000000001'
DECLARE @Запись object
DECLARE @Список array

USE 'MS_TEST'
  SELECT Ссылка, Владелец
       , Код, Наименование
       , ВерсияДанных, ПометкаУдаления
    INTO @Список -- @Запись
    FROM Справочник.Справочник1 AS T
   WHERE Код = @Код
  -- AND Ссылка = @Ссылка
END