
PRIVATE @Счётчик integer
PRIVATE @Получатель string
PRIVATE @Сообщение object
PRIVATE @ПакетСообщений array

SET @Получатель = 'MS'

USE TRANSACTION 'PG_TEST'

   CONSUME TOP 10 НомерСообщения
         , Отправитель, Получатель
         , ТипСообщения, ТелоСообщения
         , РазмерСообщения = DATALENGTH(ТелоСообщения)
      INTO @ПакетСообщений
      FROM РегистрСведений.ИсходящаяОчередь
     WHERE Получатель = @Получатель
     ORDER BY НомерСообщения ASC

   FOR @Сообщение IN @ПакетСообщений

      PRINT JSON(@Сообщение)

      SET @Счётчик = @Счётчик + 1

      IF @Счётчик = 2 THEN THROW 'TEST ERROR' END
   END
END

PRINT '[CONSUME] SIMPLE = ' + @Счётчик