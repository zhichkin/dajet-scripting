
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

   FOR @Сообщение IN @ПакетСообщений

      PRINT JSON(@Сообщение)

      SET @Счётчик = @Счётчик + 1

      IF @Счётчик = 2 THEN THROW 'TEST ERROR' END
   END
END

PRINT '[CONSUME] SIMPLE = ' + @Счётчик