
DECLARE @Отправитель string = 'MS_TEST'

PRIVATE @Счётчик        integer
PRIVATE @Документ       object
PRIVATE @ТабличнаяЧасть array

USE 'PG_TEST'

   STREAM Ссылка, Дата, Номер, Проведен, ПометкаУдаления
     INTO @Документ
     FROM Документ.Приход
    ORDER BY Дата ASC
   
   USE 'PG_TEST'
      SELECT НомерСтроки, Номенклатура, Количество
        INTO @ТабличнаяЧасть
        FROM Документ.Приход.Товары
       WHERE Ссылка = @Документ.Ссылка
       ORDER BY НомерСтроки ASC
   END

   SET @Документ.Товары = @ТабличнаяЧасть

   USE 'MS_TEST'
      INSERT РегистрСведений.ВходящаяОчередь
      SELECT НомерСообщения = VECTOR('so_import')
           , ДатаВремя      = NOW()
           , Отправитель    = @Отправитель
           , ТипСообщения   = 'Документ.Приход'
           , ТелоСообщения  = JSON(@Документ)
   END

   SET @Счётчик = @Счётчик + 1
END

RETURN '[STREAM] PG_TEST - MS_TEST = ' + @Счётчик