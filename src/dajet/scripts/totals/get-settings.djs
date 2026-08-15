
PRIVATE @Настройки object

USE 'MS_TEST'

  SELECT UseTotals           -- numeric(1,0) Использовать итоги
       , ActualPeriod        -- boolean      Использовать актуальные итоги
       , MinCalculatedPeriod -- datetime     Минимальный период хранимых итогов
       , Period              -- datetime     Максимальный период хранимых итогов
       , UseSplitter         -- boolean      Использовать разделение итогов       
    INTO @Настройки
    FROM РегистрНакопления.ОстаткиТовара.Настройки

END

RETURN @Настройки