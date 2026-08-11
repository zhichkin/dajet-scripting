
DECLARE @Отправитель string = 'PG_TEST'

PRIVATE @Объект  object
PRIVATE @Счётчик integer

USE 'PG_TEST'

   STREAM Ссылка, Код, Наименование, ПометкаУдаления, Вид
     INTO @Объект
     FROM Справочник.Номенклатура
    ORDER BY Код ASC

   USE 'MS_TEST'
      INSERT РегистрСведений.ВходящаяОчередь
      SELECT НомерСообщения = VECTOR('so_import')
           , ДатаВремя      = NOW()
           , Отправитель    = @Отправитель
           , ТипСообщения   = 'Справочник.Номенклатура'
           , ТелоСообщения  = JSON(@Объект)
   END

   SET @Счётчик = @Счётчик + 1
END

RETURN '[STREAM] PG_TEST > MS_TEST = ' + @Счётчик