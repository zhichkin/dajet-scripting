DECLARE @Таблица array

DECLARE @Код string = '000000001'

USE 'MS_TEST'

  SELECT Номенклатура -- ВидДвижения, Период, Регистратор, 
       , Остаток = SUM(CASE WHEN ВидДвижения = 0 THEN Количество ELSE -Количество END)
    INTO @Таблица
    FROM РегистрНакопления.Остатки
   GROUP BY Номенклатура

END

RETURN JSON(@Таблица)