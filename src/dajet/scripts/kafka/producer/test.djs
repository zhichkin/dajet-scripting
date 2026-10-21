-- test comment

# LONG_TASK

PRIVATE @message object
PRIVATE @counter integer = 1
PRIVATE @produced integer = 0

WHILE @counter > 0

   SET @counter = 0

   USE TRANSACTION 'MS_TEST'

      STREAM НомерСообщения
           , Отправитель, Получатель
           , ТипСообщения, ТелоСообщения
        INTO @message
        FROM РегистрСведений.ИсходящаяОчередь
       ORDER BY НомерСообщения ASC
      OFFSET @produced ROWS
       FETCH NEXT 1000 ROWS ONLY

      PRODUCE 'kafka'
         WITH acks               = 'all'
            , client.id          = 'dajet'
            , bootstrap.servers  = '192.168.61.221:9092'
            , max.in.flight      = 1
            , retries            = 1
            , message.timeout.ms = 30000
            , enable.idempotence = false
       SELECT Topic = 'test-topic'
            , Key   = @message.НомерСообщения
            , Value = JSON(@message)

      SET @counter = @counter + 1

      -- IF @Счётчик = 5 THEN SLEEP 5 THROW 'TEST ERROR' END

   END -- USE

   PRINT '[STREAM] Счётчик = ' + @counter

   SET @produced = @produced + @counter

   -- IF @produced >= 30 THEN BREAK END

END -- WHILE

PRINT '[STREAM] MS_TEST > KAFKA = ' + @produced

RETURN '[STREAM] SUCCESS'