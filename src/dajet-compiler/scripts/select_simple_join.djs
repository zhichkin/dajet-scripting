DECLARE @Ссылка entity
DECLARE @Код    string = '0001'
DECLARE @Запись object

USE 'MS_TEST'
  SELECT T1.Ссылка
       , T2.Ссылка
       , Код = T1.Код
       , Owner = T1.Владелец
       , T2.ВерсияДанных
       , T1.ПометкаУдаления
       , T1.Наименование
       , Сумма = SUM(T2.Наименование) OVER ()
    INTO @Запись
    FROM Справочник.Справочник1 AS T1
   INNER JOIN Справочник.СправочникВладелец AS T2
      ON T1.Ссылка = T2.Ссылка
   WHERE T1.Код    = @Код
     AND T2.Ссылка = @Ссылка
END