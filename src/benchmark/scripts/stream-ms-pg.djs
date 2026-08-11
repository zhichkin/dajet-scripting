
DECLARE @Отправитель string = 'MS_TEST'

PRIVATE @Объект  object
PRIVATE @Счётчик integer

USE 'MS_TEST'

   STREAM Ссылка, Код, Наименование, ПометкаУдаления, Вид
     INTO @Объект
     FROM Справочник.Номенклатура
    ORDER BY Код ASC

   USE 'PG_TEST'
      INSERT РегистрСведений.ВходящаяОчередь
      SELECT НомерСообщения = VECTOR('so_import')
           , ДатаВремя      = NOW()
           , Отправитель    = @Отправитель
           , ТипСообщения   = 'Справочник.Номенклатура'
           , ТелоСообщения  = JSON(@Объект)
   END

   SET @Счётчик = @Счётчик + 1
END

RETURN '[STREAM] MS_TEST > PG_TEST = ' + @Счётчик