
DECLARE @test string = 'test'

TRY
  THROW 'boom!'
FINALLY
  PRINT 'finally'
END

RETURN @test