DECLARE @Ссылка entity
DECLARE @Код    string = '0001'
DECLARE @Запись object

USE 'MS_TEST'
  SELECT Т.Ссылка
       , Т.Владелец
       , Identity = Т.Ссылка
       , Owner = Т.Владелец
    INTO @Запись
    FROM (SELECT Ссылка, Владелец FROM Справочник.Справочник1 WHERE Код = @Код) AS Т
   WHERE Т.Ссылка = @Ссылка
END