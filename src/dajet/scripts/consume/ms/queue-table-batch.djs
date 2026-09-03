
PRIVATE @Счётчик integer
PRIVATE @Сообщение object
PRIVATE @ПакетСообщений array

USE TRANSACTION 'MS_TEST'

   CONSUME TOP 10 НомерСообщения
         , Отправитель, Получатель
         , ТипСообщения, ТелоСообщения
      INTO @ПакетСообщений
      FROM РегистрСведений.ИсходящаяОчередь
     ORDER BY НомерСообщения ASC

   USE TRANSACTION 'PG_TEST'

      FOR @Сообщение IN @ПакетСообщений

         INSERT РегистрСведений.ВходящаяОчередь
         SELECT НомерСообщения = VECTOR('so_import')
              , ДатаВремя      = NOW()
              , Отправитель    = @Сообщение.Отправитель
              , ТипСообщения   = @Сообщение.ТипСообщения
              , ТелоСообщения  = @Сообщение.ТелоСообщения

         SET @Счётчик = @Счётчик + 1

         PRINT '[CONSUME] MS_TEST > PG_TEST = ' + @Счётчик

         IF @Счётчик = 3 THEN THROW 'TEST ERROR' END
      END
   END
END

USE 'PG_TEST'
   SELECT COUNT(*) INTO @Счётчик FROM РегистрСведений.ВходящаяОчередь
END

PRINT '[COUNT] PG_TEST = ' + @Счётчик

RETURN '[CONSUME] SUCCESS'