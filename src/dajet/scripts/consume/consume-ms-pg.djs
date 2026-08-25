
PRIVATE @Счётчик integer
PRIVATE @Сообщение object

USE 'MS_TEST'

   CONSUME TOP 10 НомерСообщения
         , Отправитель, Получатель
         , ТипСообщения, ТелоСообщения
      INTO @Сообщение
      FROM РегистрСведений.ИсходящаяОчередь
     ORDER BY НомерСообщения ASC

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

PRINT '[CONSUME] MS_TEST > PG_TEST = ' + @Счётчик

USE 'PG_TEST'
   SELECT COUNT(*) INTO @Счётчик FROM РегистрСведений.ВходящаяОчередь
END

PRINT '[COUNT] PG_TEST = ' + @Счётчик

RETURN '[CONSUME] SUCCESS'