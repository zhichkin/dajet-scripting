
DECLARE @ПериодОтчёта datetime = '2026-03-01T00:00:00'
DECLARE @Номенклатура entity   = {76:d07472b9-7d2b-8cfd-11e5-36a68b476466}

PRIVATE @ПериодИтогов        datetime = '0001-01-01T00:00:00'
PRIVATE @ПериодТекущихИтогов datetime = '3999-11-01T00:00:00' -- Константа
PRIVATE @Итоги        array
PRIVATE @ТекущиеИтоги array
PRIVATE @Остатки      array
PRIVATE @Настройки    object
PRIVATE @Результат    object

USE 'MS_TEST'

  SELECT UseTotals           -- numeric(1,0) Использовать итоги
       , ActualPeriod        -- boolean      Использовать актуальные итоги
       , MinCalculatedPeriod -- datetime     Минимальный период хранимых итогов
       , Period              -- datetime     Максимальный период хранимых итогов
       , UseSplitter         -- boolean      Использовать разделение итогов       
    INTO @Настройки
    FROM РегистрНакопления.ОстаткиТовара.Настройки

  IF @Настройки.UseTotals = 1 THEN
    THROW 'Использование итогов выключено'
  END

  IF @ПериодОтчёта < @Настройки.MinCalculatedPeriod THEN
    SET @ПериодИтогов = @Настройки.MinCalculatedPeriod
  ELSE IF @ПериодОтчёта >= @Настройки.MinCalculatedPeriod AND @ПериодОтчёта <= @Настройки.Period THEN
         SET @ПериодИтогов = @Настройки.Period -- DATESTART(MONTH, DATEADD(MONTH, 1, @ПериодОтчёта)) -- DATEDIFF(datepart, startdate, enddate)
       ELSE IF @Настройки.ActualPeriod = TRUE THEN
              SET @ПериодИтогов = '3999-11-01T00:00:00' -- Константа
            ELSE
              SET @ПериодИтогов = '0001-01-01T00:00:00' -- Пустая дата
            END
       END
  END

  WITH ИтоговыеДвижения AS
  (
  SELECT Номенклатура, Количество = SUM(Количество)
    FROM РегистрНакопления.ОстаткиТовара.Итоги
   WHERE Период = @ПериодИтогов
   GROUP BY Номенклатура
  HAVING SUM(Количество) <> 0

  UNION ALL

  SELECT Номенклатура
       , Количество = SUM(CASE WHEN ВидДвижения = 0 THEN Количество ELSE -Количество END)
    FROM РегистрНакопления.ОстаткиТовара
   WHERE Активность = TRUE AND Период < @ПериодОтчёта
   GROUP BY Номенклатура
  HAVING SUM(CASE WHEN ВидДвижения = 0 THEN Количество ELSE -Количество END) <> 0
  )
  SELECT Номенклатура, Остаток = SUM(Количество) INTO @Остатки
    FROM ИтоговыеДвижения
   GROUP BY Номенклатура
  HAVING SUM(Количество) <> 0

END

SET @Результат.Настройки    = @Настройки
SET @Результат.ПериодИтогов = @ПериодИтогов
SET @Результат.ПериодОтчёта = @ПериодОтчёта
SET @Результат.Остатки      = @Остатки

RETURN @Результат