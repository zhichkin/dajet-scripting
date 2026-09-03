
PRIVATE @Счётчик integer
PRIVATE @Сообщение object

WHILE TRUE

USE TRANSACTION 'MS_TEST'

   CONSUME TOP 1 НомерСообщения
         , Отправитель
         , Получатель
         , ТипСообщения, ТелоСообщения
      INTO @Сообщение
      FROM РегистрСведений.ИсходящаяОчередь
     ORDER BY НомерСообщения ASC

   PRINT JSON(@Сообщение)
   
   SET @Счётчик = @Счётчик + 1   

END -- USE + CONSUME

--IF @Счётчик = 1 THEN BREAK END

END -- WHILE

PRINT '[CONSUME] SIMPLE = ' + @Счётчик