
--# STARTUP
# LONG_TASK
# SINGLETON 'Обмен данными MS - PG'

PRIVATE @Счётчик integer
PRIVATE @Сообщение object

PRINT '[longrunning-stream.djs] Started.'

WHILE TRUE

   TRY

      USE TRANSACTION 'MS_TEST'

         CONSUME TOP 10 НомерСообщения
               , Отправитель, Получатель
               , ТипСообщения, ТелоСообщения
            INTO @Сообщение
            FROM РегистрСведений.ИсходящаяОчередь
           ORDER BY НомерСообщения ASC

         USE 'PG_TEST'
            INSERT РегистрСведений.ВходящаяОчередь
            SELECT НомерСообщения = VECTOR('so_import')
                 , ДатаВремя      = NOW()
                 , Отправитель    = @Сообщение.Отправитель
                 , ТипСообщения   = @Сообщение.ТипСообщения
                 , ТелоСообщения  = @Сообщение.ТелоСообщения
         END

         SET @Счётчик = @Счётчик + 1
      END

      PRINT '[longrunning-stream.djs] Consumed ' + @Счётчик + ' messages.'

   CATCH
      PRINT '[longrunning-stream.djs] ' + ERROR_MESSAGE()
   END

   SLEEP 1

END -- WHILE

-- PRINT '[longrunning-stream.djs] Stopped. Consumed ' + @Счётчик + ' messages.'