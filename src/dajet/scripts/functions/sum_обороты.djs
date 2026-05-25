
DECLARE @Таблица array

DECLARE @Код string  = '000000001'

USE 'MS_TEST'

  SELECT Номенклатура, SUM(Количество) --Период, Регистратор, Номенклатура, Количество
    INTO @Таблица
    FROM РегистрНакопления.Обороты
   GROUP BY Номенклатура

END

RETURN JSON(@Таблица)