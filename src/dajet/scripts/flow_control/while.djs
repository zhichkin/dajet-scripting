
PRIVATE @Счётчик integer

WHILE @Счётчик < 10

  SET @Счётчик = @Счётчик + 1

  IF @Счётчик = 5 THEN BREAK END

  PRINT 'LOOP: ' + @Счётчик

END

RETURN 'WHILE TEST'