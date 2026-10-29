
DECLARE @test integer = 0

WHILE @test < 3

  SET @test = @test + 1

  TRY
    THROW 'boom'
  CATCH
    PRINT ERROR_MESSAGE()
    BREAK
  END

END

RETURN @test