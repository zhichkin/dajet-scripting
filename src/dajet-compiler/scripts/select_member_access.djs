
DECLARE @Код    string  = '000000001'
DECLARE @Запись object
DECLARE @Список array

USE 'MS_TEST'
  
  SELECT Ссылка, Наименование
    INTO @Запись
    FROM Справочник.Справочник1
   WHERE Код = @Код

  SELECT Ссылка, Владелец
       , Код, Наименование
       , ПометкаУдаления
    INTO @Список
    FROM Справочник.Справочник1
   WHERE Ссылка = @Запись.Ссылка

END