
PRIVATE @test string = 'test'

TRY
  THROW 'boom!'
CATCH
  SET @test = 'catch'
END

PRINT @test

RETURN @test