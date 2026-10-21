
-- test comment

# LONG_TASK

PRIVATE @message object

CONSUME 'kafka'
   WITH topic                 = 'test-topic'
      , group.id              = 'dajet'
      , client.id             = 'dajet'
      , bootstrap.servers     = '192.168.61.221:9092'
      , enable.auto.commit    = false
      , auto.offset.reset     = 'earliest'
      , session.timeout.ms    = 60000
      , heartbeat.interval.ms = 20000
   INTO @message

USE 'MS_TEST'

   INSERT РегистрСведений.ВходящаяОчередь
   SELECT ДатаВремя      = NOW()
        , НомерСообщения = VECTOR('so_import')
        , Отправитель    = @message.Key
        , ТипСообщения   = @message.Topic
        , ТелоСообщения  = @message.Value
END

--THROW '[ERROR][TEST] After INSERT'