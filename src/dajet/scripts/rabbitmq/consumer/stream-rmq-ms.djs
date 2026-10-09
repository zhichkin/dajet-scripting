
PRIVATE @message object
PRIVATE @headers object
PRIVATE @Счётчик integer

SET @headers.version = '1.0'

CONSUME 'amqp://guest:guest@localhost:5672'
   WITH QueueName = 'test-queue', Heartbeat = 5
   INTO @message

USE 'MS_TEST'
  INSERT РегистрСведений.ВходящаяОчередь
  SELECT Отправитель    = @message.AppId
       , ДатаВремя      = NOW()
       , НомерСообщения = VECTOR('so_import')
       , ТипСообщения   = @message.Type
       , ТелоСообщения  = @message.Body
       , ТекстОшибки    = JSON(@message.Headers)
END

--THROW 'error from script'

--RETURN '[RabbitMQ] [CONSUME] End of script'