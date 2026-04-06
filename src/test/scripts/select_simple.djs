DECLARE @Ссылка entity
DECLARE @Код    string = '0001'
DECLARE @Запись object

USE 'MS_TEST'
  SELECT T.Ссылка
       , Код = Код
       , Owner = T.Владелец
       , ВерсияДанных
       , ПометкаУдаления
       , Наименование
       , Сумма = SUM(Наименование) OVER ()
    INTO @Запись
    FROM Справочник.Справочник1 AS T
   WHERE Код    = @Код
     AND Ссылка = @Ссылка
END