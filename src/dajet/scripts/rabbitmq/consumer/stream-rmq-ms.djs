
DECLARE @Отправитель string = 'MS_TEST'

PRIVATE @message object
PRIVATE @headers object
PRIVATE @Счётчик integer

SET @headers.version = '1.0'

CONSUME 'amqp://guest:guest@localhost:5672/dajet'
   WITH QueueName = 'test-queue', Heartbeat = 10
   INTO @message

USE 'MS_TEST'

   STREAM TOP 10 НомерСообщения
        , Отправитель, Получатель
        , ТипСообщения, ТелоСообщения
     INTO @message
     FROM РегистрСведений.ИсходящаяОчередь
    ORDER BY НомерСообщения ASC

   PRODUCE 'amqp://guest:guest@localhost:5672' -- /dajet
    SELECT AppId      = @message.Отправитель
         , Headers    = @headers
         , Exchange   = 'test-exchange'
         , RoutingKey = @message.Получатель
         , MessageId  = @message.НомерСообщения
         , Type       = @message.ТипСообщения
         , Body       = @message.ТелоСообщения -- JSON(@message)

   SET @Счётчик = @Счётчик + 1

   --IF @Счётчик = 5 THEN THROW 'ERROR' END
END

PRINT '[STREAM] MS_TEST > RABBIT = ' + @Счётчик

RETURN '[STREAM] SUCCESS'