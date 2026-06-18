
PRIVATE @Таблица array

DECLARE @НачалоПериода datetime  = '2026-06-01T00:00:00'
DECLARE @КонецПериода  datetime  = '2026-06-30T23:59:59'

USE 'MS_TEST'

  WITH Остатки AS (SELECT Регистратор, Партия, Количество
                     FROM РегистрНакопления.ОстаткиТовара
                    WHERE NOT Регистратор IS NULL)
  SELECT Партия.Номер
       , Остатки.Количество
    INTO @Таблица
    FROM Остатки
   INNER JOIN Документ.Приход AS Партия
      ON Остатки.Партия = Партия.Ссылка
     AND Регистратор IS Документ.Приход
     AND NOT Остатки.Регистратор IS NULL

END

RETURN @Таблица