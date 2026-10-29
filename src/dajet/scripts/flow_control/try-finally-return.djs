
DECLARE @test string = 'test'

TRY
  RETURN 'try'
FINALLY
  SET @test = 'finally'
END

RETURN @test