
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
      -- , ДатаВремя      = NOW()
  END

END

RETURN '[BULK INSERT] обмен сообщениями MS - PG'