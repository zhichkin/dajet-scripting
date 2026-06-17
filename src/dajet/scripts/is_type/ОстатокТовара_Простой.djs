
PRIVATE @Таблица array

DECLARE @НачалоПериода datetime  = '2026-06-01T00:00:00'
DECLARE @КонецПериода  datetime  = '2026-06-30T23:59:59'

USE 'MS_TEST'

  SELECT Номенклатура, Партия
       , ОстатокПоКоличеству = SUM(CASE WHEN ВидДвижения = 0 THEN Количество ELSE -Количество END)
       , ОстатокПоДокументам = SUM(CASE WHEN Регистратор IS Документ.Приход THEN  Количество
                                        WHEN Регистратор IS Документ.Расход THEN -Количество ELSE 0 END)
       , Тест = SUM(CASE WHEN Партия IS Документ.Приход THEN 1 ELSE 0 END)
    INTO @Таблица
    FROM РегистрНакопления.ОстаткиТовара
   WHERE Период BETWEEN @НачалоПериода AND @КонецПериода
     AND NOT Партия IS NULL
     AND NOT Партия IS Документ.Расход
     AND NOT Номенклатура IS NULL
     AND Номенклатура IS Справочник.Номенклатура
   GROUP BY Номенклатура, Партия
   ORDER BY ОстатокПоКоличеству DESC

END

RETURN @Таблица