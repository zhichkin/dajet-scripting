
PRIVATE @message    object
PRIVATE @headers    object
PRIVATE @Счётчик    integer
PRIVATE @Обработано integer = 0

SET @Счётчик = 1
SET @headers.version = '1.0'

WHILE @Счётчик > 0

   SET @Счётчик = 0

   USE 'MS_TEST'

      STREAM НомерСообщения
           , Отправитель, Получатель
           , ТипСообщения, ТелоСообщения
        INTO @message
        FROM РегистрСведений.ИсходящаяОчередь
       ORDER BY НомерСообщения ASC
      OFFSET @Обработано ROWS
       FETCH NEXT 1000 ROWS ONLY

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

   END -- USE

   PRINT '[STREAM] Счётчик = ' + @Счётчик

   SET @Обработано = @Обработано + @Счётчик

END -- WHILE

PRINT '[STREAM] MS_TEST > RABBIT = ' + @Обработано

RETURN '[STREAM] SUCCESS'