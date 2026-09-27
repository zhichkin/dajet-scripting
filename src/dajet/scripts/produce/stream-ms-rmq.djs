
DECLARE @Отправитель string = 'MS_TEST'

PRIVATE @message  object
PRIVATE @Счётчик integer

USE 'MS_TEST'

   STREAM TOP 10
          Ссылка, Код, Наименование, ПометкаУдаления, Вид
     INTO @message
     FROM Справочник.Номенклатура
    ORDER BY Код ASC

   PRODUCE 'amqp://guest:guest@localhost:5672/dajet'
    SELECT AppId      = 'MS_TEST'
         , Exchange   = 'test-exchange'
         , RoutingKey = @message.Получатель
         , MessageId  = @message.НомерСообщения
         , Type       = @message.ТипСообщения
         , Body       = @message.ТелоСообщения

   SET @Счётчик = @Счётчик + 1

   --IF @Счётчик = 5 THEN THROW 'ERROR' END
END

PRINT '[STREAM] MS_TEST > RABBIT = ' + @Счётчик

RETURN '[STREAM] SUCCESS'