
PRIVATE @Буфер array
PRIVATE @Запись object

USE TRANSACTION 'MS_TEST'

  SELECT НомерСообщения
       , Отправитель, Получатель
       , ТипСообщения, ТелоСообщения
    INTO @Буфер
    FROM РегистрСведений.ИсходящаяОчередь
   ORDER BY НомерСообщения ASC

  USE TRANSACTION 'PG_TEST'
    INSERT РегистрСведений.ВходящаяОчередь
      FROM @Буфер -- TIMEOUT 10 BATCH_SIZE 333
    SELECT НомерСообщения = @Буфер.НомерСообщения -- VECTOR('so_import')
         , Отправитель    = @Буфер.Отправитель
         , ТипСообщения   = @Буфер.ТипСообщения
         , ТелоСообщения  = @Буфер.ТелоСообщения
         --, ДатаВремя      = '0001-01-01T00:00:01' -- NOW()
  END

END

USE 'PG_TEST'
  SELECT TOP 1 ДатаВремя INTO @Запись FROM РегистрСведений.ВходящаяОчередь WHERE ДатаВремя = '0001-01-01T00:00:00'
END

PRINT JSON(@Запись)

RETURN '[BULK INSERT] обмен сообщениями MS - PG'