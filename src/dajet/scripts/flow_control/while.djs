
# STARTUP                    -- Запуск скрипта при старте хоста DaJet
# LONG_TASK                  -- Долгий скрипт (отдельный поток выполнения)
# SINGLETON 'LONG TASK TEST' -- Директива контроля единственного выполнения

PRIVATE @Счётчик integer

PRINT '[LONG TASK TEST] START'

WHILE @Счётчик < 10

  SET @Счётчик = @Счётчик + 1

  IF @Счётчик = 5 THEN BREAK END

  PRINT 'LOOP: ' + @Счётчик

  SLEEP 3

END

PRINT '[LONG TASK TEST] END'