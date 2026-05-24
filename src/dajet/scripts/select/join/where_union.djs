
DECLARE @Таблица  array
DECLARE @Документ object
DECLARE @Номер    string  = '000000001'

USE 'MS_TEST'
  
  SELECT Ссылка INTO @Документ FROM Документ.Документ1 WHERE Номер = @Номер
  
  SELECT Период, Регистратор, Измерение1, Ресурс1, Реквизит1
    FROM РегистрНакопления.РегистрНакопления1
   WHERE Регистратор = @Документ.Ссылка
    
 --INNER JOIN Документ AS Шапка ON Шапка.Ссылка = ТЧ.Ссылка

END

RETURN @Таблица