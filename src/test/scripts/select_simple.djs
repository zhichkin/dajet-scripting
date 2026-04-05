DECLARE @Ссылка entity
DECLARE @Код    string = '0001'
DECLARE @Запись object

USE 'MS_TEST'
  SELECT Ссылка
       , Код = Код
       , Владелец = Владелец
       , ВерсияДанных
       , ПометкаУдаления
       , Наименование
       , Сумма = SUM(Наименование) OVER ()
    INTO @Запись
    FROM Справочник.Справочник1
   WHERE Код    = @Код
     AND Ссылка = @Ссылка
END