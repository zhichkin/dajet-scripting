
DECLARE @Отправитель string = 'MS_TEST'

PRIVATE @Таблица array
PRIVATE @Запись object
PRIVATE @Счётчик integer

USE 'MS_TEST'
  SELECT Ссылка, Код, Наименование
    INTO @Таблица
    FROM Справочник.Номенклатура
   ORDER BY Код DESC
END

USE 'PG_TEST'
  FOR @Запись IN @Таблица

    INSERT РегистрСведений.ВходящаяОчередь
    SELECT НомерСообщения = VECTOR('so_import')
         , ДатаВремя      = NOW()
         , Отправитель    = @Отправитель
         , ТипСообщения   = 'Справочник.Номенклатура'
         , ТелоСообщения  = JSON(@Запись)

    SET @Счётчик = @Счётчик + 1
  END
END

RETURN 'Счётчик = ' + @Счётчик