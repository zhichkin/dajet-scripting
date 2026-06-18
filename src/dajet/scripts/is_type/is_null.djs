
PRIVATE @Таблица array

DECLARE @НачалоПериода datetime  = '2026-06-01T00:00:00'
DECLARE @КонецПериода  datetime  = '2026-06-30T23:59:59'

USE 'MS_TEST'

  SELECT Номенклатура, Количество = CASE WHEN Регистратор IS NOT Документ.Приход THEN Количество ELSE -Количество END
    INTO @Таблица
    FROM РегистрНакопления.ОстаткиТовара
   WHERE NOT Регистратор IS NULL
     AND Регистратор IS NOT NULL
     AND NOT Регистратор IS Документ.Приход
     AND Регистратор IS NOT Документ.Приход

END

RETURN @Таблица