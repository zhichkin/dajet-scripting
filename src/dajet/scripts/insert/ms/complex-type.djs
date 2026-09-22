
PRIVATE @Буфер array

USE TRANSACTION 'MS_TEST'

  SELECT TOP 3
         МногоСсылок = Ссылка
       , Булево = ЭтоГруппа
    INTO @Буфер
    FROM Справочник.Номенклатура

  INSERT РегистрСведений.Тестовый
    FROM @Буфер BATCH_SIZE 10
  SELECT МногоСсылок = @Буфер.МногоСсылок
       , Булево = NOT @Буфер.Булево -- Это работает =)

END

RETURN 'Команда BULK INSERT выполнена успешно'