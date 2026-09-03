
DECLARE @Получатель string = 'PG'

PRIVATE @Счётчик integer
PRIVATE @Сообщение object

WHILE TRUE

SET @Счётчик = 0

USE TRANSACTION 'MS_TEST'

   CONSUME TOP 10 НомерСообщения
         , Отправитель, Получатель
         , ТипСообщения, ТелоСообщения
      INTO @Сообщение
      FROM РегистрСведений.ИсходящаяОчередь
     WHERE Получатель = @Получатель
     --ORDER BY НомерСообщения DESC

   USE 'PG_TEST'
      INSERT РегистрСведений.ВходящаяОчередь
      SELECT НомерСообщения = VECTOR('so_import')
           , ДатаВремя      = NOW()
           , Отправитель    = @Сообщение.Отправитель
           , ТипСообщения   = @Сообщение.ТипСообщения
           , ТелоСообщения  = @Сообщение.ТелоСообщения
   END

   SET @Счётчик = @Счётчик + 1
END

IF @Счётчик = 0 THEN BREAK END

END -- WHILE

PRINT '[CONSUME] MS_TEST > PG_TEST = ' + @Счётчик

USE 'PG_TEST'
   SELECT COUNT(*) INTO @Счётчик FROM РегистрСведений.ВходящаяОчередь
END

PRINT '[COUNT] PG_TEST = ' + @Счётчик

RETURN '[CONSUME] SUCCESS'