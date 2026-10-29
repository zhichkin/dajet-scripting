
PRIVATE @counter integer = 0

WHILE @counter < 3

  SET @counter = @counter + 1

  BREAK

END

RETURN '@counter = ' + @counter