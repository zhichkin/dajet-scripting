
DECLARE @test string = 'test'

TRY
  SET @test = 'try'
FINALLY
  RETURN 'finally'
END

RETURN @test