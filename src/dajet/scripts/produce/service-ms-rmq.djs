
PRIVATE @message object
PRIVATE @headers object
PRIVATE @Счётчик integer

SET @Счётчик = 0
SET @headers.version = '1.0'

WHILE TRUE

   TRY

      USE 'MS_TEST'

         STREAM TOP 5 НомерСообщения
              , Отправитель, Получатель
              , ТипСообщения, ТелоСообщения
           INTO @message
           FROM РегистрСведений.ИсходящаяОчередь
          ORDER BY НомерСообщения ASC

         PRODUCE 'amqp://guest:guest@localhost:5672'
          SELECT AppId      = @message.Отправитель
               , Headers    = @headers
               , Exchange   = 'test-exchange'
               , RoutingKey = @message.Получатель
               , MessageId  = @message.НомерСообщения
               , Type       = @message.ТипСообщения
               , Body       = @message.ТелоСообщения -- JSON(@message)

         SET @Счётчик = @Счётчик + 1

         --IF @Счётчик = 5 THEN THROW 'ERROR' END

      END -- USE

      PRINT '[STREAM] Отправлено = ' + @Счётчик

   CATCH
      PRINT '[STREAM][ERROR] ' + ERROR_MESSAGE()
   END
   
   PRINT '[STREAM] ' + NOW()

   IF @Счётчик >= 50 THEN BREAK END

   SLEEP 5

END -- WHILE

PRINT '[STREAM] MS_TEST > RABBIT = ' + @Счётчик

RETURN '[STREAM] SUCCESS'