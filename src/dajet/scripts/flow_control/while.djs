
PRIVATE @Счётчик integer

WHILE @Счётчик < 10

  SET @Счётчик = @Счётчик + 1

  PRINT 'LOOP: ' + @Счётчик

END

RETURN 'WHILE TEST'